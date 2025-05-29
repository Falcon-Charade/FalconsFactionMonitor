using FalconsFactionMonitor.Helpers;
using FalconsFactionMonitor.Services;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace FalconsFactionMonitor.Windows
{
    public partial class JournalMonitorWindow : BaseWindow
    {
        public JournalMonitorWindow()
        {
            InitializeComponent();
            RichTextBoxWriter writer = new(JournalOutputTextBlock);
            Console.SetOut(writer);
            Console.SetError(writer);
        }

        private async void StartJMServiceButton_Click(object sender, RoutedEventArgs e)
        {
            JournalOutputTextBlock.Document.Blocks.Clear();
            JournalRetrievalService service = new();

            await Task.Run(() => service.JournalRetrieval());
        }

        private async void ExitJMServiceButton_Click(object sender, RoutedEventArgs e)
        {
            // Hide window
            await Animations.FadeWindowAsync(this, 1.0, 0.0, 300); // Fade out
            this.Close();
        }

        private void ViewJournalFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var JournalPath = Path.Combine("C:\\","Users",Environment.UserName, "Saved Games","Frontier Developments","Elite Dangerous");
            Process.Start("explorer.exe", JournalPath);
        }
    }
}
