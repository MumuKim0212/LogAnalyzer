using System.Collections.Generic;
using System.Windows;
using LogAnalyzerWPF.Models;

namespace LogAnalyzerWPF.UI
{
    public interface IErrorTimelineChart
    {
        void UpdateData(IEnumerable<LogEntry> logs);
        UIElement GetVisualControl();
    }
}
