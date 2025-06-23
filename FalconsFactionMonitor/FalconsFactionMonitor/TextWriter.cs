using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using FalconsFactionMonitor.Themes;
using MaterialDesignThemes.Wpf;

namespace FalconsFactionMonitor
{
    internal class RichTextBoxWriter : TextWriter
    {
        private readonly RichTextBox _output;
        private readonly BaseTheme _baseTheme;
        private bool _newLine = true;
        private static readonly Regex TagRegex = new Regex(@"\[(INFO|WARN|ERROR|DEBUG|TRACE)\]", RegexOptions.IgnoreCase);

        public RichTextBoxWriter(RichTextBox output)
        {
            _output = output;
            // Load the user's preferred theme (fallback to system or Dark)
            var (registryTheme, _, _) = AppTheme.LoadThemeFromRegistry();
            _baseTheme = registryTheme
                ?? AppTheme.GetSystemTheme()
                ?? BaseTheme.Dark;
        }

        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value) => Write(value.ToString());

        public override void Write(string value)
        {
            if (string.IsNullOrEmpty(value)) return;

            var segments = ParseColoredSegments(value);

            _output.Dispatcher.Invoke(() =>
            {
                Paragraph paragraph;
                if (_newLine || !(_output.Document.Blocks.LastBlock is Paragraph))
                {
                    paragraph = new Paragraph();
                    _output.Document.Blocks.Add(paragraph);
                    _newLine = false;
                }
                else
                {
                    paragraph = (Paragraph)_output.Document.Blocks.LastBlock;
                }

                foreach (var (text, brush) in segments)
                {
                    paragraph.Inlines.Add(new Run(text) { Foreground = brush });
                }

                _output.ScrollToEnd();
            });
        }

        public override void WriteLine(string value)
        {
            if (value == null) return;

            var segments = ParseColoredSegments(value);

            _output.Dispatcher.Invoke(() =>
            {
                Paragraph paragraph;
                if (!_newLine && _output.Document.Blocks.LastBlock is Paragraph)
                {
                    paragraph = (Paragraph)_output.Document.Blocks.LastBlock;
                }
                else
                {
                    paragraph = new Paragraph();
                    _output.Document.Blocks.Add(paragraph);
                }

                foreach (var (text, brush) in segments)
                {
                    paragraph.Inlines.Add(new Run(text) { Foreground = brush });
                }

                paragraph.Inlines.Add(new LineBreak());
                _newLine = true;
                _output.ScrollToEnd();
            });
        }

        private List<(string Text, Brush Brush)> ParseColoredSegments(string text)
        {
            var list = new List<(string, Brush)>();
            int lastIndex = 0;
            var matches = TagRegex.Matches(text);

            foreach (Match m in matches)
            {
                if (m.Index > lastIndex)
                {
                    var before = text.Substring(lastIndex, m.Index - lastIndex);
                    list.Add((before, DetermineBrush(before)));
                }

                var tag = m.Value.ToUpperInvariant();
                list.Add((tag + " ", GetTagBrush(tag)));
                lastIndex = m.Index + m.Length;
            }

            if (lastIndex < text.Length)
            {
                var remaining = text.Substring(lastIndex);
                list.Add((remaining, DetermineBrush(remaining)));
            }

            return list;
        }

        private Brush GetTagBrush(string tag)
        {
            // Choose tag colors based on light or dark theme
            return _baseTheme switch
            {
                BaseTheme.Light => tag switch
                {
                    "[INFO]" => Brushes.Purple,
                    "[WARN]" => Brushes.DarkOrange,
                    "[ERROR]" => Brushes.DarkRed,
                    "[DEBUG]" => Brushes.DarkGray,
                    "[TRACE]" => Brushes.SteelBlue,
                    _ => Brushes.Black
                },
                BaseTheme.Dark => tag switch
                {
                    "[INFO]" => Brushes.Magenta,
                    "[WARN]" => Brushes.Orange,
                    "[ERROR]" => Brushes.Red,
                    "[DEBUG]" => Brushes.Gray,
                    "[TRACE]" => Brushes.LightBlue,
                    _ => Brushes.White
                },
                _ => Brushes.White
            };
        }

        private Brush DetermineBrush(string text)
        {
            // Prefix (module/faction) ends with ': '
            if (text.TrimEnd().EndsWith(":"))
                return _baseTheme == BaseTheme.Light ? Brushes.DarkSlateBlue : Brushes.LightSteelBlue;
            // Key message patterns
            // Pattern-based coloring for messages
            if (text.Contains("Using direct journal path"))
                return _baseTheme == BaseTheme.Light ? Brushes.DarkGreen : Brushes.Green;
            if (text.Contains("Monitoring journal"))
                return _baseTheme == BaseTheme.Light ? Brushes.Teal : Brushes.Cyan;
            if (text.Contains("Elite Dangerous has closed"))
                return _baseTheme == BaseTheme.Light ? Brushes.Teal : Brushes.Cyan;
            // Combined system-faction with percent
            if (Regex.IsMatch(text, @"[A-Za-z0-9\s\-']+: \d+(\.\d+)?% influence"))
                return _baseTheme == BaseTheme.Light ? Brushes.Goldenrod : Brushes.Yellow;
            // Standalone percent-only lines
            if (Regex.IsMatch(text.Trim(), @"^\d+(\.\d+)?% influence$"))
                return _baseTheme == BaseTheme.Light ? Brushes.Goldenrod : Brushes.Yellow;

            // Fallback to console color
            return ConvertConsoleColorToBrush(Console.ForegroundColor);
        }

        private Brush ConvertConsoleColorToBrush(ConsoleColor color) => color switch
        {
            ConsoleColor.Black => Brushes.Black,
            ConsoleColor.DarkBlue => Brushes.DarkBlue,
            ConsoleColor.DarkGreen => Brushes.DarkGreen,
            ConsoleColor.DarkCyan => Brushes.DarkCyan,
            ConsoleColor.DarkRed => Brushes.DarkRed,
            ConsoleColor.DarkMagenta => Brushes.DarkMagenta,
            ConsoleColor.DarkYellow => Brushes.Olive,
            ConsoleColor.Gray => Brushes.Gray,
            ConsoleColor.DarkGray => Brushes.DarkGray,
            ConsoleColor.Blue => Brushes.Blue,
            ConsoleColor.Green => Brushes.Green,
            ConsoleColor.Cyan => Brushes.Cyan,
            ConsoleColor.Red => Brushes.Red,
            ConsoleColor.Magenta => Brushes.Magenta,
            ConsoleColor.Yellow => Brushes.Yellow,
            ConsoleColor.White => Brushes.White,
            _ => Brushes.White,
        };
    }
}
