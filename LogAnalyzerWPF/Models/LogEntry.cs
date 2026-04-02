using System.Windows.Media;

namespace LogAnalyzerWPF.Models
{
    public class LogEntry
    {
        public string Timestamp { get; set; } = string.Empty;
        public string Level { get; set; } = string.Empty;
        public string Handler { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string RawLine { get; set; } = string.Empty;

        public SolidColorBrush BackgroundColor
        {
            get
            {
                if (Level == "ERROR" || Level == "FATAL")
                {
                    return new SolidColorBrush(Color.FromRgb(60, 0, 0)); // Opaque Dark Red
                }
                else if (Level == "WARN")
                {
                    return new SolidColorBrush(Color.FromRgb(60, 60, 0)); // Opaque Dark Yellow
                }
                else if (Level == "DEBUG")
                {
                    return new SolidColorBrush(Color.FromRgb(40, 40, 40)); // Slightly lighter gray 
                }
                
                return new SolidColorBrush(Colors.Transparent);
            }
        }
    }
}
