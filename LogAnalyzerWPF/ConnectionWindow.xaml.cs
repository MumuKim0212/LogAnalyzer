using System.Windows;
using LogAnalyzerWPF.Models;

namespace LogAnalyzerWPF
{
    public partial class ConnectionWindow : Window
    {
        public RemoteConfig Config { get; private set; } = new RemoteConfig();

        public ConnectionWindow()
        {
            InitializeComponent();
        }

        private void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            Config.Mode = RbLinux.IsChecked == true ? ConnectionMode.LinuxSSH : ConnectionMode.WindowsAgent;
            Config.Host = TxtHost.Text.Trim();
            Config.Port = int.TryParse(TxtPort.Text, out int port) ? port : (Config.Mode == ConnectionMode.LinuxSSH ? 22 : 5000);
            Config.Username = TxtUser.Text.Trim();
            Config.PasswordOrPemPath = TxtPassOrPem.Text.Trim();
            Config.IsPemKey = ChkIsPem.IsChecked == true;
            Config.RemoteFilePath = TxtFilePath.Text.Trim();

            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
