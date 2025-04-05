using FalconsFactionMonitor.Services;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace FalconsFactionMonitor.Windows
{
    public partial class JournalMonitorWindow : Window
    {
        public JournalMonitorWindow()
        {
            InitializeComponent();
            RichTextBoxWriter writer = new RichTextBoxWriter(JournalOutputTextBlock);
            Console.SetOut(writer);
            Console.SetError(writer);
        }

        private async void StartJMServiceButton_Click(object sender, RoutedEventArgs e)
        {
            JournalOutputTextBlock.Document.Blocks.Clear();
            JournalRetrievalService service = new JournalRetrievalService();

            await Task.Run(() => service.JournalRetrieval());
        }

        private void ExitJMServiceButton_Click(object sender, RoutedEventArgs e)
        {
            MainWindow mainWindow = new MainWindow();
            mainWindow.Show();
            Close();
        }
    }
}
