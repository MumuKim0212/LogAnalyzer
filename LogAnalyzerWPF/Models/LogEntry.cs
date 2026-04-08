using System.ComponentModel;
using System.Windows.Media;

namespace LogAnalyzerWPF.Models
{
    public class LogEntry : INotifyPropertyChanged
    {
        private bool _isBookmarked = false;

        public string Timestamp { get; set; } = string.Empty;
        public string Level { get; set; } = string.Empty;
        public string Handler { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string RawLine { get; set; } = string.Empty;

        public bool IsBookmarked
        {
            get => _isBookmarked;
            set
            {
                if (_isBookmarked != value)
                {
                    _isBookmarked = value;
                    OnPropertyChanged(nameof(IsBookmarked));
                    OnPropertyChanged(nameof(BookmarkText));
                    OnPropertyChanged(nameof(BackgroundColor));
                }
            }
        }

        public string BookmarkText => IsBookmarked ? "★" : "";

        public SolidColorBrush BackgroundColor
        {
            get
            {
                if (IsBookmarked)
                {
                    return new SolidColorBrush(Color.FromRgb(40, 80, 40)); // Dark Green for bookmarks
                }

                if (Level == "ERROR" || Level == "FATAL")
                {
                    return new SolidColorBrush(Color.FromRgb(60, 0, 0)); 
                }
                else if (Level == "WARN")
                {
                    return new SolidColorBrush(Color.FromRgb(60, 60, 0)); 
                }
                else if (Level == "DEBUG")
                {
                    return new SolidColorBrush(Color.FromRgb(40, 40, 40)); 
                }
                
                return new SolidColorBrush(Colors.Transparent); // Default #1E1E1E via DataGrid
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
