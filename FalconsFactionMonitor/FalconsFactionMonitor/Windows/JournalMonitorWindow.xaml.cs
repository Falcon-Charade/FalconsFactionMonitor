using FalconsFactionMonitor.Services;
using FalconsFactionMonitor.Windows;
using System;
using System.Windows;

namespace FalconsFactionMonitor.Windows
{
    public partial class JournalMonitorWindow : Window
    {
        public JournalMonitorWindow()
        {
            InitializeComponent();
            Console.SetOut(new RichTextBoxWriter(JournalOutputTextBlock));
        }

        private void StartJMServiceButton_Click(object sender, RoutedEventArgs e)
        {
            JournalOutputTextBlock.Document.Blocks.Clear();
            JournalRetrievalService service = new JournalRetrievalService();
            service.JournalRetrieval().Wait();
        }

        private void ExitJMServiceButton_Click(object sender, RoutedEventArgs e)
        {
            MainWindow mainWindow = new MainWindow();
            mainWindow.Show();
            Close();
        }
    }
}
