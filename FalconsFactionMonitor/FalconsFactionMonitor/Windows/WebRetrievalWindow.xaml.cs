using FalconsFactionMonitor.Services;
using System;
using System.Windows;
using System.Diagnostics;
using System.IO;
using System.Configuration;
using System.Windows.Media;

namespace FalconsFactionMonitor.Windows
{
    public partial class WebRetrievalWindow : Window
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

            WebRetrievalService service = new WebRetrievalService();
            await service.WebRetrieval(FactionTextBox.Text, inaraParse: check, CSVSave: CSVSave);
        }

        private void ExitWRServiceButton_Click(object sender, RoutedEventArgs e)
        {
            new MainWindow().Show();
            Close();
        }

        private void OpenOutputButton_Click(object sender, RoutedEventArgs e)
        {
            var OutputPath = Path.Combine(GetSavePath(), "Output");
            Process.Start("explorer.exe", OutputPath);
        }

        private void CSVPathButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select Folder to Save CSV Files",
                SelectedPath = GetSavePath()
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string newPath = dialog.SelectedPath;
                if (newPath.EndsWith("Output"))
                    newPath = newPath.Substring(0, newPath.Length - 7);

                SetSavePath(newPath);
                MessageBox.Show("Save location updated successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private string GetSavePath()
        {
            string path = ConfigurationManager.AppSettings["CsvSavePath"];
            return string.IsNullOrEmpty(path)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "YourManufacturer", "YourProduct")
                : Environment.ExpandEnvironmentVariables(path);
        }

        private void SetSavePath(string newPath)
        {
            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            config.AppSettings.Settings["CsvSavePath"].Value = newPath;
            config.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection("appSettings");
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
