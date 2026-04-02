namespace LogAnalyzerWPF.Models
{
    public enum ConnectionMode
    {
        LinuxSSH,
        WindowsAgent
    }

    public class RemoteConfig
    {
        public ConnectionMode Mode { get; set; } = ConnectionMode.LinuxSSH;
        public string Host { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 22;
        public string Username { get; set; } = "root";
        public string PasswordOrPemPath { get; set; } = "";
        public bool IsPemKey { get; set; } = false;
        public string RemoteFilePath { get; set; } = "/var/log/server.txt";
    }
}
