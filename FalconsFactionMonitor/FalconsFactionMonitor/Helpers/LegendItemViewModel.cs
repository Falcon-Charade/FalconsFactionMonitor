using OxyPlot;
using OxyPlot.Series;
using System.ComponentModel;
using System.Runtime.CompilerServices;

public class LegendItemViewModel : INotifyPropertyChanged
{
    readonly LineSeries _series;
    readonly PlotModel _model;

    public LegendItemViewModel(LineSeries series, PlotModel model)
    {
        _series = series;
        _model = model;
    }

    public string Title => _series.Title;

    public bool IsVisible
    {
        get => _series.IsVisible;
        set
        {
            if (_series.IsVisible == value) return;
            _series.IsVisible = value;
            _model.InvalidatePlot(true);  // refresh
            RaisePropertyChanged();
        }
    }

    // INotifyPropertyChanged impl…
    public event PropertyChangedEventHandler PropertyChanged;
    void RaisePropertyChanged([CallerMemberName] string n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
