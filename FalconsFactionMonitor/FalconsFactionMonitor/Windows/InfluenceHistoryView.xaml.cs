using FalconsFactionMonitor.Helpers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace FalconsFactionMonitor.Windows
{
    public partial class InfluenceHistoryView : Window
    {
        public InfluenceHistoryView()
        {
            InitializeComponent();
            DataContext = new InfluenceHistoryViewModel();
        }

        private void ReportOptionsButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.ContextMenu != null)
            {
                btn.ContextMenu.PlacementTarget = btn;
                btn.ContextMenu.Placement = PlacementMode.Bottom;
                btn.ContextMenu.IsOpen = true;
            }
        }
    }
}