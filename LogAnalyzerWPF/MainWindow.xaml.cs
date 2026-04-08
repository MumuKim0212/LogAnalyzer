using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LogAnalyzerWPF.Views;

namespace LogAnalyzerWPF
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            this.KeyDown += MainWindow_KeyDown;
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.R && Keyboard.Modifiers == ModifierKeys.Control)
            {
                BtnLocalRun_Click(this, new RoutedEventArgs());
            }
            if (e.Key == Key.T && Keyboard.Modifiers == ModifierKeys.Control)
            {
                BtnOpenFile_Click(this, new RoutedEventArgs());
            }
            if (e.Key == Key.W && Keyboard.Modifiers == ModifierKeys.Control)
            {
                BtnCloseTab_Click(this, new RoutedEventArgs());
            }
            if (e.Key == Key.T && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
            {
                BtnChart_Click(this, new RoutedEventArgs());
            }
        }

        private LogTabView? GetActiveTab()
        {
            if (MainTabControl == null) return null;

            if (MainTabControl.SelectedItem is TabItem tab && tab.Content is LogTabView view)
            {
                return view;
            }
            return null;
        }

        private void AddNewTab(string headerText, LogTabView content)
        {
            var tab = new TabItem
            {
                Header = headerText,
                Content = content,
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 48)),
                Foreground = System.Windows.Media.Brushes.White
            };
            MainTabControl.Items.Add(tab);
            MainTabControl.SelectedItem = tab;
        }

        private void BtnOpenFile_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog();
            openFileDialog.Filter = "Log files (*.log;*.txt)|*.log;*.txt|All files (*.*)|*.*";
            if (openFileDialog.ShowDialog() == true)
            {
                var view = new LogTabView();
                view.LoadLocalFile(openFileDialog.FileName);
                AddNewTab(Path.GetFileName(openFileDialog.FileName), view);
            }
        }

        private void BtnRemote_Click(object sender, RoutedEventArgs e)
        {
            var cw = new ConnectionWindow { Owner = this };
            if (cw.ShowDialog() == true)
            {
                var view = new LogTabView();
                view.StartRemote(cw.Config);
                AddNewTab($"[{cw.Config.Mode}] {cw.Config.Host}", view);
            }
        }

        private void BtnLocalRun_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog();
            openFileDialog.Filter = "Executables (*.exe;*.bat)|*.exe;*.bat|All files (*.*)|*.*";
            if (openFileDialog.ShowDialog() == true)
            {
                var view = new LogTabView();
                view.StartLocalProcess(openFileDialog.FileName);
                AddNewTab($"Run: {Path.GetFileName(openFileDialog.FileName)}", view);
            }
        }

        private void BtnCloseTab_Click(object sender, RoutedEventArgs e)
        {
            if (MainTabControl.SelectedItem is TabItem tab)
            {
                // Unhook temporarily to avoid cascading selection events while disposing
                MainTabControl.SelectionChanged -= MainTabControl_SelectionChanged;
                
                if (tab.Content is IDisposable disposable)
                {
                    disposable.Dispose();
                }
                MainTabControl.Items.Remove(tab);
                
                MainTabControl.SelectionChanged += MainTabControl_SelectionChanged;
                MainTabControl_SelectionChanged(MainTabControl, null!);
            }
        }

        private void BtnChart_Click(object sender, RoutedEventArgs e)
        {
            GetActiveTab()?.ToggleChart();
        }

        private void TxtFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            GetActiveTab()?.ApplyFilter(TxtFilter.Text, ChkWatch.IsChecked == true);
        }

        private void ChkWatch_Changed(object sender, RoutedEventArgs e)
        {
            GetActiveTab()?.ApplyFilter(TxtFilter.Text, ChkWatch.IsChecked == true);
        }

        private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e != null && e.Source is not TabControl) return;
            
            GetActiveTab()?.ApplyFilter(TxtFilter.Text, ChkWatch.IsChecked == true);
        }

        protected override void OnClosed(EventArgs e)
        {
            foreach (var item in MainTabControl.Items)
            {
                if (item is TabItem tab && tab.Content is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            base.OnClosed(e);
        }
    }
}