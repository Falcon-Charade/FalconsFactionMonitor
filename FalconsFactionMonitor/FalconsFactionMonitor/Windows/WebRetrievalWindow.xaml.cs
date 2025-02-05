using FalconsFactionMonitor.Services;
using System;
using System.Windows;
using FalconsFactionMonitor.Windows;

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

        private void StartWRServiceButton_Click(object sender, RoutedEventArgs e)
        {
            bool check = false;
            if (InaraCheckBox.IsChecked == true)
            {
                check = true;
            }
            WebRetrievalOutputTextBlock.Document.Blocks.Clear();
            WebRetrievalOutputTextBlock.Foreground = System.Windows.Media.Brushes.White;
            WebRetrievalService service = new WebRetrievalService();
            service.WebRetrieval(FactionTextBox.Text, inaraParse: check).Wait();
        }

        private void ExitJMServiceButton_Click(object sender, RoutedEventArgs e)
        {
            MainWindow mainWindow = new MainWindow();
            mainWindow.Show();
            Close();
        }
    }
}
