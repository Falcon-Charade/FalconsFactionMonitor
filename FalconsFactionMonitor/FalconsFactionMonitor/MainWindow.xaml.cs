using FalconsFactionMonitor.Services;
using System;
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
            Hide();
            JournalMonitorWindow journalMonitorWindow = new JournalMonitorWindow();
            journalMonitorWindow.Show();
        }

        private void WebRetrievalServiceButton_Click(object sender, RoutedEventArgs e)
        {
            ResultTextBlock.Document.Blocks.Clear();
            ResultTextBlock.Foreground = System.Windows.Media.Brushes.Red;
            ResultTextBlock.AppendText("Web Retrieval Service is not yet implemented.");
            //ResultTextBlock.Document.Blocks.Clear();
            //ResultTextBlock.AppendText("StartingWeb Retrieval Service.");
            //Hide();
            //WebRetrievalWindow webRetrievalWindow = new WebRetrievalWindow();
            //webRetrievalWindow.Show();
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
