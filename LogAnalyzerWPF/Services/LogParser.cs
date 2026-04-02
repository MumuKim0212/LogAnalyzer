using System.Text.RegularExpressions;
using LogAnalyzerWPF.Models;

namespace LogAnalyzerWPF.Services
{
    public class LogParser
    {
        // Parses strings like: [26/03/31 09:49:18] [INFO] [immigration.OnMessage...] reconnect.nosession
        private static readonly Regex LogPattern = new Regex(@"^\[(.*?)\]\s*\[(.*?)\]\s*\[(.*?)\]\s*(.*)$", RegexOptions.Compiled);

        public static LogEntry? ParseLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return null;

            var match = LogPattern.Match(line);
            if (match.Success)
            {
                return new LogEntry
                {
                    Timestamp = match.Groups[1].Value.Trim(),
                    Level = match.Groups[2].Value.Trim().ToUpper(),
                    Handler = match.Groups[3].Value.Trim(),
                    Message = match.Groups[4].Value.Trim(),
                    RawLine = line
                };
            }

            // Based on user request, ignore lines that don't match the new format
            return null;
        }
    }
}
