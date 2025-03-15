using FalconsFactionMonitor.Services;
using System;
using System.Windows;
using System.Diagnostics;
using System.IO;

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
            if (InaraCheckBox.IsChecked == true)
            {
                check = true;
            }
            WebRetrievalOutputTextBlock.Document.Blocks.Clear();
            WebRetrievalOutputTextBlock.Foreground = System.Windows.Media.Brushes.White;
            WebRetrievalOutputTextBlock.AppendText("Program starting execution. This may take a few minutes to run");
            WebRetrievalService service = new WebRetrievalService();
            await service.WebRetrieval(FactionTextBox.Text, inaraParse: check);
        }

        private void ExitWRServiceButton_Click(object sender, RoutedEventArgs e)
        {
            MainWindow mainWindow = new MainWindow();
            mainWindow.Show();
            Close();
        }

        private void OpenOutputButton_Click(object sender, RoutedEventArgs e)
        {
            var OutputPath = Path.Combine(Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)?.FullName, "Output");
            Process.Start("explorer.exe", OutputPath);
        }
    }
}
