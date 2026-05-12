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
        private readonly Action<string>? _onStatus;
        private CancellationTokenSource? _cts;
        private Task? _monitorTask;
        private SshClient? _sshClient;
        private SshCommand? _sshCommand;
        private TcpClient? _tcpClient;

        public RemoteLogMonitor(RemoteConfig config, Action<LogEntry> onLogReceived, Action<string> onError, Action<string>? onStatus = null)
        {
            _config = config;
            _onLogReceived = onLogReceived;
            _onError = onError;
            _onStatus = onStatus;
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
                if (_config.IsPemKey)
                {
                    if (!File.Exists(_config.PasswordOrPemPath))
                    {
                        _onError($"[SSH] PEM 키 파일을 찾을 수 없습니다: {_config.PasswordOrPemPath}");
                        return;
                    }
                    _onStatus?.Invoke($"[SSH] PEM 키 인증 방식으로 {_config.Host}:{_config.Port} 연결 시도 중...");
                    var pk = new PrivateKeyFile(_config.PasswordOrPemPath);
                    connInfo = new ConnectionInfo(_config.Host, _config.Port, _config.Username, new PrivateKeyAuthenticationMethod(_config.Username, pk));
                }
                else
                {
                    _onStatus?.Invoke($"[SSH] 비밀번호 인증 방식으로 {_config.Host}:{_config.Port} 연결 시도 중...");
                    connInfo = new ConnectionInfo(_config.Host, _config.Port, _config.Username, new PasswordAuthenticationMethod(_config.Username, _config.PasswordOrPemPath));
                }

                _sshClient = new SshClient(connInfo);
                _sshClient.Connect();

                if (!_sshClient.IsConnected)
                {
                    _onError("[SSH] 연결 후 IsConnected가 false입니다. 인증에 실패했을 수 있습니다.");
                    return;
                }

                _onStatus?.Invoke($"[SSH] 연결 성공! 원격 파일 모니터링 시작: {_config.RemoteFilePath}");

                // 'tail -f' runs indefinitely
                _sshCommand = _sshClient.CreateCommand($"tail -f \"{_config.RemoteFilePath}\"");
                var asyncResult = _sshCommand.BeginExecute();

                _onStatus?.Invoke($"[SSH] 'tail -f' 명령 실행 중. 새 로그를 기다리는 중...");

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

                if (!token.IsCancellationRequested)
                    _onStatus?.Invoke("[SSH] 원격 스트림이 종료되었습니다.");
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested)
                    _onError($"[SSH 오류] {ex.GetType().Name}: {ex.Message}");
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
                _onStatus?.Invoke($"[TCP] Windows Agent에 연결 시도 중: {_config.Host}:{_config.Port}");
                _tcpClient = new TcpClient();
                _tcpClient.Connect(_config.Host, _config.Port);
                var stream = _tcpClient.GetStream();

                _onStatus?.Invoke($"[TCP] 연결 성공! 파일 경로 전송: {_config.RemoteFilePath}");

                // Agent Protocol: Client sends the requested file path ending with newline
                var requestBytes = Encoding.UTF8.GetBytes(_config.RemoteFilePath + "\n");
                stream.Write(requestBytes, 0, requestBytes.Length);
                stream.Flush();

                _onStatus?.Invoke("[TCP] 파일 경로 전송 완료. 에이전트 응답 대기 중...");

                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    while (!token.IsCancellationRequested)
                    {
                        var line = reader.ReadLine();
                        if (line == null)
                        {
                            _onStatus?.Invoke("[TCP] 서버가 연결을 종료했습니다.");
                            break;
                        }

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
                if (!token.IsCancellationRequested)
                    _onError($"[TCP 오류] {ex.GetType().Name}: {ex.Message}");
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
