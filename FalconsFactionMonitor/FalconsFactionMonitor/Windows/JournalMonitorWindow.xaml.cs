using FalconsFactionMonitor.Services;
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

        private async void StartJMServiceButton_Click(object sender, RoutedEventArgs e)
        {
            JournalOutputTextBlock.Document.Blocks.Clear();
            JournalRetrievalService service = new JournalRetrievalService();
            await service.JournalRetrieval();
        }

        private void ExitJMServiceButton_Click(object sender, RoutedEventArgs e)
        {
            MainWindow mainWindow = new MainWindow();
            mainWindow.Show();
            Close();
        }
    }
}
