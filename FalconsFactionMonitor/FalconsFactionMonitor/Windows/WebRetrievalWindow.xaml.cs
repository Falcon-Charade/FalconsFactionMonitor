using FalconsFactionMonitor.Services;
using System;
using System.Windows;
using System.Diagnostics;
using System.IO;
using FalconsFactionMonitor.Helpers;

namespace FalconsFactionMonitor.Windows
{
    public partial class WebRetrievalWindow : BaseWindow
    {
        public WebRetrievalWindow()
        {
            InitializeComponent();

            var writer = new RichTextBoxWriter(WebRetrievalOutputTextBlock);
            Console.SetOut(writer);
            Console.SetError(writer);
        }

        private async void StartWRServiceButton_Click(object sender, RoutedEventArgs e)
        {
            bool check = InaraCheckBox.IsChecked == true;
            bool CSVSave = CSVCheckBox.IsChecked == true;

            WebRetrievalOutputTextBlock.Document.Blocks.Clear();
            AppendLog("Program starting execution. This may take a few minutes to run.\n");
            AppendLog("\n");

            WebRetrievalService service = new();
            await service.WebRetrieval(FactionTextBox.Text, inaraParse: check, CSVSave: CSVSave);
        }

        private async void ExitWRServiceButton_Click(object sender, RoutedEventArgs e)
        {
            // Hide window
            await Animations.FadeWindowAsync(this, 1.0, 0.0, 300); // Fade out
            this.Close();
        }

        private void OpenOutputButton_Click(object sender, RoutedEventArgs e)
        {
            var OutputPath = Path.Combine(FolderInteractions.GetSavePath("CSV"), "Output");
            Process.Start("explorer.exe", OutputPath);
        }
        private void AppendLog(string message)
        {
            WebRetrievalOutputTextBlock.Dispatcher.Invoke(() =>
            {
                WebRetrievalOutputTextBlock.AppendText(message);
                WebRetrievalOutputTextBlock.ScrollToEnd();
            });
        }
    }
}
