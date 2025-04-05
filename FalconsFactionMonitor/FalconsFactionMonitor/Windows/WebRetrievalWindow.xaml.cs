using FalconsFactionMonitor.Services;
using System;
using System.Windows;
using System.Diagnostics;
using System.IO;
using System.Configuration;

namespace FalconsFactionMonitor.Windows
{
    /// <summary>
    /// Interaction logic for WebRetrievalWindow.xaml
    /// </summary>
    public partial class WebRetrievalWindow : Window
    {
        public WebRetrievalWindow()
        {
            InitializeComponent();
            Console.SetOut(new RichTextBoxWriter(WebRetrievalOutputTextBlock));
        }

        private async void StartWRServiceButton_Click(object sender, RoutedEventArgs e)
        {
            bool check = false;
            bool CSVSave = false;
            if (CSVCheckBox.IsChecked == true)
            {
                CSVSave = true;
            }
            if (InaraCheckBox.IsChecked == true)
            {
                check = true;
            }
            WebRetrievalOutputTextBlock.Document.Blocks.Clear();
            WebRetrievalOutputTextBlock.Foreground = System.Windows.Media.Brushes.White;
            WebRetrievalOutputTextBlock.AppendText("Program starting execution. This may take a few minutes to run.");
            WebRetrievalOutputTextBlock.AppendText(" \r");
            WebRetrievalService service = new WebRetrievalService();
            await service.WebRetrieval(FactionTextBox.Text, inaraParse: check, CSVSave: CSVSave);
        }

        private void ExitWRServiceButton_Click(object sender, RoutedEventArgs e)
        {
            MainWindow mainWindow = new MainWindow();
            mainWindow.Show();
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
                SelectedPath = GetSavePath() // Default to current path
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string newPath = dialog.SelectedPath;

                // Update App.config with the new path
                SetSavePath(newPath);

                // Update the displayed path
                MessageBox.Show("Save location updated successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // Get save path from App.config
        public string GetSavePath()
        {
            string path = ConfigurationManager.AppSettings["CsvSavePath"];

            if (!string.IsNullOrEmpty(path))
            {
                path = Environment.ExpandEnvironmentVariables(path); // Resolve %LOCALAPPDATA%
            }

            return string.IsNullOrEmpty(path) ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "YourManufacturer", "YourProduct") : path;
        }

        // Update save path in App.config
        public void SetSavePath(string newPath)
        {
            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            config.AppSettings.Settings["CsvSavePath"].Value = newPath;
            config.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection("appSettings");
        }
    }
}
