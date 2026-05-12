using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using LogAnalyzerWPF.Models;
using LogAnalyzerWPF.Services;
using LogAnalyzerWPF.UI;

namespace LogAnalyzerWPF.Views
{
    public partial class LogTabView : UserControl, IDisposable
    {
        private ObservableCollection<LogEntry> _logs = new ObservableCollection<LogEntry>();
        private FileSystemWatcher? _fileWatcher;
        private string _currentFilePath = string.Empty;
        private long _lastReadPosition = 0;
        
        private RemoteLogMonitor? _remoteMonitor;
        private System.Diagnostics.Process? _localProcess;
        
        private IErrorTimelineChart _chart;
        private bool _autoscroll = true;
        private string _currentFilter = "";

        public LogTabView()
        {
            InitializeComponent();
            LogDataGrid.ItemsSource = _logs;
            
            // Abstract chart strategy injected correctly
            _chart = new DummyTimelineChart();
            ChartContainer.Child = _chart.GetVisualControl();
        }

        public void ApplyFilter(string filterText, bool autoscroll)
        {
            _currentFilter = filterText.ToLower();
            _autoscroll = autoscroll;

            if (string.IsNullOrWhiteSpace(_currentFilter))
            {
                LogDataGrid.ItemsSource = _logs;
            }
            else
            {
                var filtered = _logs.Where(log => 
                    log.Message.ToLower().Contains(_currentFilter) || 
                    log.Handler.ToLower().Contains(_currentFilter) ||
                    log.Level.ToLower().Contains(_currentFilter)).ToList();
                LogDataGrid.ItemsSource = filtered;
            }

            if (_autoscroll && LogDataGrid.Items.Count > 0)
            {
                LogDataGrid.ScrollIntoView(LogDataGrid.Items[^1]);
            }
        }

        public void ToggleChart()
        {
            ChartContainer.Visibility = ChartContainer.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        }

        private void LogDataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F2)
            {
                if (LogDataGrid.SelectedItem is LogEntry entry)
                {
                    entry.IsBookmarked = !entry.IsBookmarked;
                }
                e.Handled = true;
            }
            else if (e.Key == Key.F3)
            {
                JumpToNextBookmark();
                e.Handled = true;
            }
        }

        private void JumpToNextBookmark()
        {
            int selectedIndex = LogDataGrid.SelectedIndex;
            var items = LogDataGrid.Items;
            if (items.Count == 0) return;
            
            for (int i = selectedIndex + 1; i < items.Count; i++)
            {
                if (items[i] is LogEntry e && e.IsBookmarked)
                {
                    LogDataGrid.SelectedIndex = i;
                    LogDataGrid.ScrollIntoView(items[i]);
                    return;
                }
            }
            
            for (int i = 0; i < selectedIndex; i++)
            {
                if (items[i] is LogEntry e && e.IsBookmarked)
                {
                    LogDataGrid.SelectedIndex = i;
                    LogDataGrid.ScrollIntoView(items[i]);
                    return;
                }
            }
        }

        private void SafeAddLog(LogEntry entry)
        {
            Dispatcher.Invoke(() => 
            {
                _logs.Add(entry);
                if (_autoscroll && string.IsNullOrWhiteSpace(_currentFilter))
                {
                    LogDataGrid.ScrollIntoView(entry);
                }
                _chart.UpdateData(_logs);
            });
        }

        public void LoadLocalFile(string filePath)
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
                            if (entry != null) _logs.Add(entry);
                        }
                        _lastReadPosition = fs.Length;
                    }
                }

                _chart.UpdateData(_logs);
                string dir = Path.GetDirectoryName(filePath)!;
                _fileWatcher = new FileSystemWatcher(dir, Path.GetFileName(filePath));
                _fileWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size;
                _fileWatcher.Changed += OnFileChanged;
                _fileWatcher.EnableRaisingEvents = true;
                
                if (_autoscroll && _logs.Count > 0) LogDataGrid.ScrollIntoView(_logs.Last());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load file: {ex.Message}");
            }
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    using var fs = new FileStream(_currentFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    if (fs.Length < _lastReadPosition) { _logs.Clear(); _lastReadPosition = 0; }

                    fs.Seek(_lastReadPosition, SeekOrigin.Begin);
                    using var sr = new StreamReader(fs);
                    string? line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        var entry = LogParser.ParseLine(line);
                        if (entry != null) SafeAddLog(entry);
                    }
                    _lastReadPosition = fs.Length;
                }
                catch { }
            }, DispatcherPriority.Background);
        }

        public void StartRemote(RemoteConfig config)
        {
            _remoteMonitor = new RemoteLogMonitor(
                config,
                onLogReceived: SafeAddLog,
                onError: errMsg => Dispatcher.Invoke(() =>
                {
                    // 오류도 그리드에 표시 + 팝업
                    SafeAddLog(new LogEntry
                    {
                        Timestamp = DateTime.Now.ToString("HH:mm:ss"),
                        Level = "ERROR",
                        Handler = "Connection",
                        Message = errMsg
                    });
                    MessageBox.Show(errMsg, "Remote Error");
                }),
                onStatus: statusMsg => Dispatcher.Invoke(() =>
                {
                    // 연결 진행 상황을 그리드에 INFO 항목으로 표시
                    SafeAddLog(new LogEntry
                    {
                        Timestamp = DateTime.Now.ToString("HH:mm:ss"),
                        Level = "INFO",
                        Handler = "Connection",
                        Message = statusMsg
                    });
                }));
            _remoteMonitor.Start();
        }

        public void StartLocalProcess(string exePath)
        {
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
                _localProcess.OutputDataReceived += (s, ev) => 
                {
                    if (!string.IsNullOrWhiteSpace(ev.Data))
                    {
                        var entry = LogParser.ParseLine(ev.Data);
                        if (entry != null) SafeAddLog(entry);
                    }
                };
                _localProcess.ErrorDataReceived += (s, ev) => 
                {
                    if (!string.IsNullOrWhiteSpace(ev.Data))
                    {
                        var entry = LogParser.ParseLine(ev.Data);
                        if (entry != null) SafeAddLog(entry);
                    }
                };
                
                _localProcess.Start();
                _localProcess.BeginOutputReadLine();
                _localProcess.BeginErrorReadLine();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start local process: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (_fileWatcher != null) { _fileWatcher.EnableRaisingEvents = false; _fileWatcher.Dispose(); }
            if (_remoteMonitor != null) { _remoteMonitor.Stop(); _remoteMonitor.Dispose(); }
            if (_localProcess != null) { try { _localProcess.Kill(); } catch { } try { _localProcess.Dispose(); } catch { } }
        }
    }
}
