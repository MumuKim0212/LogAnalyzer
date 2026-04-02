using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Renci.SshNet;
using LogAnalyzerWPF.Models;

namespace LogAnalyzerWPF.Services
{
    public class RemoteLogMonitor : IDisposable
    {
        private readonly RemoteConfig _config;
        private readonly Action<LogEntry> _onLogReceived;
        private readonly Action<string> _onError;
        private CancellationTokenSource? _cts;
        private Task? _monitorTask;
        private SshClient? _sshClient;
        private SshCommand? _sshCommand;
        private TcpClient? _tcpClient;

        public RemoteLogMonitor(RemoteConfig config, Action<LogEntry> onLogReceived, Action<string> onError)
        {
            _config = config;
            _onLogReceived = onLogReceived;
            _onError = onError;
        }

        public void Start()
        {
            _cts = new CancellationTokenSource();
            if (_config.Mode == ConnectionMode.LinuxSSH)
            {
                _monitorTask = Task.Run(() => MonitorSshAsync(_cts.Token));
            }
            else
            {
                _monitorTask = Task.Run(() => MonitorTcpAsync(_cts.Token));
            }
        }

        public void Stop()
        {
            _cts?.Cancel();
            DisposeConnections();
        }

        private void MonitorSshAsync(CancellationToken token)
        {
            try
            {
                ConnectionInfo connInfo;
                if (_config.IsPemKey && File.Exists(_config.PasswordOrPemPath))
                {
                    var pk = new PrivateKeyFile(_config.PasswordOrPemPath);
                    connInfo = new ConnectionInfo(_config.Host, _config.Port, _config.Username, new PrivateKeyAuthenticationMethod(_config.Username, pk));
                }
                else
                {
                    connInfo = new ConnectionInfo(_config.Host, _config.Port, _config.Username, new PasswordAuthenticationMethod(_config.Username, _config.PasswordOrPemPath));
                }

                _sshClient = new SshClient(connInfo);
                _sshClient.Connect();

                // 'tail -f' runs indefinitely
                _sshCommand = _sshClient.CreateCommand($"tail -f \"{_config.RemoteFilePath}\"");
                var asyncResult = _sshCommand.BeginExecute();

                using (var reader = new StreamReader(_sshCommand.OutputStream, Encoding.UTF8))
                {
                    while (!token.IsCancellationRequested && !reader.EndOfStream)
                    {
                        var line = reader.ReadLine();
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            var entry = LogParser.ParseLine(line);
                            if (entry != null)
                            {
                                _onLogReceived(entry);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested) _onError($"SSH Error: {ex.Message}");
            }
            finally
            {
                DisposeConnections();
            }
        }

        private void MonitorTcpAsync(CancellationToken token)
        {
            try
            {
                _tcpClient = new TcpClient();
                // Connect with timeout would be better, but blocking connect is simplest
                _tcpClient.Connect(_config.Host, _config.Port);
                var stream = _tcpClient.GetStream();
                
                // Agent Protocol: Client sends the requested file path ending with newline
                var requestBytes = Encoding.UTF8.GetBytes(_config.RemoteFilePath + "\n");
                stream.Write(requestBytes, 0, requestBytes.Length);
                stream.Flush();

                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    while (!token.IsCancellationRequested)
                    {
                        var line = reader.ReadLine();
                        if (line == null) break; // Server closed connection

                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            var entry = LogParser.ParseLine(line);
                            if (entry != null)
                            {
                                _onLogReceived(entry);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested) _onError($"TCP Agent Error: {ex.Message}");
            }
            finally
            {
                DisposeConnections();
            }
        }

        private void DisposeConnections()
        {
            try { _sshCommand?.CancelAsync(); _sshCommand?.Dispose(); } catch { }
            try { _sshClient?.Disconnect(); _sshClient?.Dispose(); } catch { }
            try { _tcpClient?.Close(); _tcpClient?.Dispose(); } catch { }
        }

        public void Dispose()
        {
            Stop();
            _cts?.Dispose();
        }
    }
}
