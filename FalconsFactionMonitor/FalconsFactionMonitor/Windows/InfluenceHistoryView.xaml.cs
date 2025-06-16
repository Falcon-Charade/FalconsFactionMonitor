using System.Windows;
using FalconsFactionMonitor.Helpers;

namespace FalconsFactionMonitor.Windows
{
    public partial class InfluenceHistoryView : Window
    {
        public InfluenceHistoryView()
        {
            InitializeComponent();
            DataContext = new InfluenceHistoryViewModel();
        }
    }
}