using System;
using System.Threading.Tasks;
using System.Windows;

namespace FalconsFactionMonitor.Windows
{
    public partial class MainWindow : Window
    {
        public bool SuppressRestoreAfterOptions { get; set; } = false;
        public MainWindow()
        {
            InitializeComponent();
            Console.SetOut(new RichTextBoxWriter(ResultTextBlock));
        }

        private async void JournalMonitorServiceButton_Click(object sender, RoutedEventArgs e)
        {
            ResultTextBlock.Document.Blocks.Clear();
            ResultTextBlock.Foreground = System.Windows.Media.Brushes.Fuchsia;
            ResultTextBlock.AppendText((string)FindResource("Main_Status_StartingJournal"));
            await Task.Delay(500); // Allow text to render before fade


            // Hide main window
            await Animations.FadeWindowAsync(this, 1.0, 0.0, 300); // Fade out
            this.Hide();

            // Create and show the Journal Monitor window as modal
            JournalMonitorWindow journalMonitorWindow = new() { Owner = this };
            journalMonitorWindow.ShowDialog();

            // Show main window again after Journal Monitor window is closed
            this.Show();
            await Animations.FadeWindowAsync(this, 0.0, 1.0, 300); // Fade in
        }

        private async void WebRetrievalServiceButton_Click(object sender, RoutedEventArgs e)
        {
            ResultTextBlock.Document.Blocks.Clear();
            ResultTextBlock.Foreground = System.Windows.Media.Brushes.Fuchsia;
            ResultTextBlock.AppendText((string)FindResource("Main_Status_StartingWeb"));
            await Task.Delay(500); // Allow text to render before fade


            // Hide main window
            await Animations.FadeWindowAsync(this, 1.0, 0.0, 300); // Fade out
            this.Hide();

            // Create and show the Web Retrieval window as modal
            WebRetrievalWindow webRetrievalWindow = new() { Owner = this };
            webRetrievalWindow.ShowDialog();

            // Show main window again after Web Retrieval window is closed
            this.Show();
            await Animations.FadeWindowAsync(this, 0.0, 1.0, 300); // Fade in
        }

        private async void OptionsButton_Click(object sender, RoutedEventArgs e)
        {
            ResultTextBlock.Document.Blocks.Clear();
            ResultTextBlock.Foreground = System.Windows.Media.Brushes.Fuchsia;
            ResultTextBlock.AppendText((string)FindResource("Main_Status_OpeningOptions"));
            await Task.Delay(500); // Allow text to render before fade


            // Hide main window
            await Animations.FadeWindowAsync(this, 1.0, 0.0, 300); // Fade out
            this.Hide();

            // Create and show the options window as modal
            OptionsWindow optionsWindow = new()
            {
                Owner = this,
                Tag = "manual-restart"
            };
            optionsWindow.ShowDialog();

            if (!SuppressRestoreAfterOptions)
            {
                this.Show();
                await Animations.FadeWindowAsync(this, 0.0, 1.0, 300); // Fade in
            }
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}
