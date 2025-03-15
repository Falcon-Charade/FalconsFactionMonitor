using System;
using System.Threading;
using System.Windows;

namespace FalconsFactionMonitor.Windows
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Console.SetOut(new RichTextBoxWriter(ResultTextBlock));
        }

        private void JournalMonitorServiceButton_Click(object sender, RoutedEventArgs e)
        {
            ResultTextBlock.Document.Blocks.Clear();
            ResultTextBlock.Foreground = System.Windows.Media.Brushes.Fuchsia;
            ResultTextBlock.AppendText("Starting Journal Monitor Service.");
            Thread.Sleep(1000);
            Hide();
            JournalMonitorWindow journalMonitorWindow = new JournalMonitorWindow();
            journalMonitorWindow.Show();
        }

        private void WebRetrievalServiceButton_Click(object sender, RoutedEventArgs e)
        {
            ResultTextBlock.Document.Blocks.Clear();
            ResultTextBlock.AppendText("Starting Web Retrieval Service.");
            Thread.Sleep(1000);
            Hide();
            WebRetrievalWindow webRetrievalWindow = new WebRetrievalWindow();
            webRetrievalWindow.Show();
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
