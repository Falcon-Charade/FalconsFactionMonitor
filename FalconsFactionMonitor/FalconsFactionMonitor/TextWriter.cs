using System;
using System.IO;
using System.Text;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace FalconsFactionMonitor
{
    internal class RichTextBoxWriter : TextWriter
    {
        private readonly RichTextBox _output;

        public RichTextBoxWriter(RichTextBox output)
        {
            _output = output;
        }

        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value)
        {
            var brush = ConvertConsoleColorToBrush(Console.ForegroundColor);
            // For single-character writes, we could fetch the color,
            // but typical console code sets color by lines. 
            // So to keep it simpler, we'll handle color in WriteLine.
            _output.Dispatcher.Invoke(() =>
            {
                // Just append the char to the last paragraph
                AppendColoredText(value.ToString(), ConvertConsoleColorToBrush(Console.ForegroundColor));
            });
        }

        public override void WriteLine(string value)
        {
            // Grab current Console.ForegroundColor
            var brush = ConvertConsoleColorToBrush(Console.ForegroundColor);

            _output.Dispatcher.Invoke(() =>
            {
                // Append text plus newline in the chosen color
                AppendColoredText(value + Environment.NewLine, brush);
                _output.ScrollToEnd();
            });
        }

        private void AppendColoredText(string text, Brush brush)
        {
            // If no paragraph exists, create one
            if (!(_output.Document.Blocks.LastBlock is Paragraph paragraph))
            {
                paragraph = new Paragraph();
                _output.Document.Blocks.Add(paragraph);
            }

            // Create a Run with the specified color
            var run = new Run(text)
            {
                Foreground = brush
            };

            paragraph.Inlines.Add(run);
        }

        // Map ConsoleColor to a WPF Brush
        private Brush ConvertConsoleColorToBrush(ConsoleColor color)
        {
            return color switch
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
}
