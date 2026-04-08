using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LogAnalyzerWPF.Models;

namespace LogAnalyzerWPF.UI
{
    public class DummyTimelineChart : IErrorTimelineChart
    {
        private TextBlock _dummyText;

        public DummyTimelineChart()
        {
            _dummyText = new TextBlock
            {
                Text = "Chart Library Placeholder (LiveCharts2 will be injected later)",
                Foreground = Brushes.Gray,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(20)
            };
        }

        public void UpdateData(IEnumerable<LogEntry> logs)
        {
            _dummyText.Text = $"Chart Area: Tracking {System.Linq.Enumerable.Count(logs)} logs...";
        }

        public UIElement GetVisualControl()
        {
            return _dummyText;
        }
    }
}
