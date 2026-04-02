using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using LogAnalyzerWPF.Models;
using LogAnalyzerWPF.Services;

namespace LogAnalyzerWPF
{
    public partial class MainWindow : Window
    {
        private ObservableCollection<LogEntry> _logs = new ObservableCollection<LogEntry>();
        private FileSystemWatcher? _fileWatcher;
        private string _currentFilePath = string.Empty;
        private long _lastReadPosition = 0;
        
        private RemoteLogMonitor? _remoteMonitor;
        private System.Diagnostics.Process? _localProcess;

        public MainWindow()
        {
            InitializeComponent();
            LogDataGrid.ItemsSource = _logs;
            this.KeyDown += MainWindow_KeyDown;
        }

        private void MainWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.R && System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control)
            {
                BtnLocalRun_Click(this, new RoutedEventArgs());
            }
        }

        private void StopAllMonitors()
        {
            if (_fileWatcher != null)
            {
                _fileWatcher.EnableRaisingEvents = false;
                _fileWatcher.Dispose();
                _fileWatcher = null;
            }

            if (_remoteMonitor != null)
            {
                _remoteMonitor.Stop();
                _remoteMonitor.Dispose();
                _remoteMonitor = null;
            }

            if (_localProcess != null)
            {
                try { _localProcess.Kill(); } catch { }
                try { _localProcess.Dispose(); } catch { }
                _localProcess = null;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            StopAllMonitors();
            base.OnClosed(e);
        }

        private void BtnOpenFile_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog();
            openFileDialog.Filter = "Log files (*.log;*.txt)|*.log;*.txt|All files (*.*)|*.*";
            if (openFileDialog.ShowDialog() == true)
            {
                StopAllMonitors();
                LoadLogFile(openFileDialog.FileName);
            }
        }

        private void LoadLogFile(string filePath)
        {
            _currentFilePath = filePath;
            _logs.Clear();

            try
            {
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    using (var sr = new StreamReader(fs))
                    {
                        string? line;
                        while ((line = sr.ReadLine()) != null)
                        {
                            var entry = LogParser.ParseLine(line);
                            if (entry != null)
                            {
                                _logs.Add(entry);
                            }
                        }
                        _lastReadPosition = fs.Length;
                    }
                }

                string dir = Path.GetDirectoryName(filePath)!;
                string file = Path.GetFileName(filePath);
                
                _fileWatcher = new FileSystemWatcher(dir, file);
                _fileWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size;
                _fileWatcher.Changed += OnFileChanged;
                _fileWatcher.EnableRaisingEvents = true;

                if (ChkWatch.IsChecked == true && _logs.Count > 0)
                {
                    LogDataGrid.ScrollIntoView(_logs.Last());
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load file: {ex.Message}", "Error");
            }
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    using (var fs = new FileStream(_currentFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    {
                        if (fs.Length < _lastReadPosition)
                        {
                            _logs.Clear();
                            _lastReadPosition = 0;
                        }

                        fs.Seek(_lastReadPosition, SeekOrigin.Begin);
                        using (var sr = new StreamReader(fs))
                        {
                            string? line;
                            while ((line = sr.ReadLine()) != null)
                            {
                                var entry = LogParser.ParseLine(line);
                                if (entry != null)
                                {
                                    _logs.Add(entry);
                                }
                            }
                            _lastReadPosition = fs.Length;
                        }

                        if (ChkWatch.IsChecked == true && _logs.Count > 0)
                        {
                            if (string.IsNullOrWhiteSpace(TxtFilter.Text))
                            {
                                LogDataGrid.ScrollIntoView(_logs.Last());
                            }
                        }
                    }
                }
                catch 
                { 
                    // Ignore lock exceptions and retry on next change event
                }
            }, DispatcherPriority.Background);
        }

        private void BtnRemote_Click(object sender, RoutedEventArgs e)
        {
            var cw = new ConnectionWindow { Owner = this };
            if (cw.ShowDialog() == true)
            {
                StartRemoteMonitor(cw.Config);
            }
        }

        private void StartRemoteMonitor(RemoteConfig config)
        {
            StopAllMonitors();
            _logs.Clear();

            _remoteMonitor = new RemoteLogMonitor(config, 
                onLogReceived: entry => 
                {
                    Dispatcher.Invoke(() => 
                    { 
                        _logs.Add(entry);
                        if (ChkWatch.IsChecked == true && string.IsNullOrWhiteSpace(TxtFilter.Text))
                        {
                            LogDataGrid.ScrollIntoView(entry);
                        }
                    });
                }, 
                onError: errMsg => 
                {
                    Dispatcher.Invoke(() => MessageBox.Show(errMsg, "Remote Error"));
                });

            _remoteMonitor.Start();
        }

        private void BtnLocalRun_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog();
            openFileDialog.Filter = "Executables (*.exe;*.bat)|*.exe;*.bat|All files (*.*)|*.*";
            openFileDialog.Title = "Select Server Executable";
            
            if (openFileDialog.ShowDialog() == true)
            {
                StartLocalProcess(openFileDialog.FileName);
            }
        }

        private void StartLocalProcess(string exePath)
        {
            StopAllMonitors();
            _logs.Clear();

            try
            {
                var pinfo = new System.Diagnostics.ProcessStartInfo(exePath)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                pinfo.EnvironmentVariables["ASPNETCORE_ENVIRONMENT"] = "Development";

                _localProcess = new System.Diagnostics.Process { StartInfo = pinfo };
                
                _localProcess.OutputDataReceived += (s, ev) => ProcessProcessOutput(ev.Data);
                _localProcess.ErrorDataReceived += (s, ev) => ProcessProcessOutput(ev.Data);
                
                _localProcess.Start();
                _localProcess.BeginOutputReadLine();
                _localProcess.BeginErrorReadLine();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start local process: {ex.Message}", "Error");
            }
        }

        private void ProcessProcessOutput(string? data)
        {
            if (string.IsNullOrWhiteSpace(data)) return;
            
            var entry = LogParser.ParseLine(data);
            if (entry != null)
            {
                Dispatcher.Invoke(() => 
                {
                    _logs.Add(entry);
                    if (ChkWatch.IsChecked == true && string.IsNullOrWhiteSpace(TxtFilter.Text))
                    {
                        LogDataGrid.ScrollIntoView(entry);
                    }
                });
            }
        }

        private void TxtFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            string filterText = TxtFilter.Text.ToLower();
            if (string.IsNullOrWhiteSpace(filterText))
            {
                LogDataGrid.ItemsSource = _logs;
            }
            else
            {
                var filtered = _logs.Where(log => 
                    log.Message.ToLower().Contains(filterText) || 
                    log.Handler.ToLower().Contains(filterText) ||
                    log.Level.ToLower().Contains(filterText)).ToList();
                    
                LogDataGrid.ItemsSource = filtered;
            }
        }
    }
}