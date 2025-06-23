using MaterialDesignThemes.Wpf;
using Microsoft.Data.SqlClient;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace FalconsFactionMonitor.Helpers
{
    public class InfluenceHistoryViewModel : INotifyPropertyChanged
    {
        protected void RaisePropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private readonly PaletteHelper _paletteHelper = new();

        // Search mode flags
        private bool _isFactionSearch = true;
        public bool IsFactionSearch
        {
            get => _isFactionSearch;
            set { _isFactionSearch = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsSystemSearch)); SearchCommand.RaiseCanExecuteChanged(); }
        }
        public bool IsSystemSearch
        {
            get => !_isFactionSearch;
            set
            {
                _isFactionSearch = !value;
                OnPropertyChanged(); // IsSystemSearch changed
                OnPropertyChanged(nameof(IsFactionSearch));
                SearchCommand.RaiseCanExecuteChanged();
            }
        }

        // Search input
        private string _searchText;
        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();
                SearchCommand.RaiseCanExecuteChanged();
            }
        }

        // Date filters
        private DateTime? _startDate;
        public DateTime? StartDate
        {
            get => _startDate;
            set
            {
                _startDate = value;
                OnPropertyChanged();
                SearchCommand.RaiseCanExecuteChanged();
            }
        }
        private DateTime? _endDate;
        public DateTime? EndDate
        {
            get => _endDate;
            set
            {
                _endDate = value;
                OnPropertyChanged();
                SearchCommand.RaiseCanExecuteChanged();
            }
        }

        // Influence range filters
        private double? _minInfluence;
        public double? MinInfluence
        {
            get => _minInfluence;
            set { _minInfluence = value; RaisePropertyChanged(); SearchCommand.RaiseCanExecuteChanged(); }
        }

        private double? _maxInfluence;
        public double? MaxInfluence
        {
            get => _maxInfluence;
            set { _maxInfluence = value; RaisePropertyChanged(); SearchCommand.RaiseCanExecuteChanged(); }
        }

        // OxyPlot model
        private PlotModel _plotModel;
        public PlotModel PlotModel
        {
            get => _plotModel;
            private set { _plotModel = value; OnPropertyChanged(); }
        }

        // Command
        public RelayCommand SearchCommand { get; }

        // Connection string
        private readonly string _connectionString = DatabaseConnectionBuilder.BuildConnectionString();

        public InfluenceHistoryViewModel()
        {
            // Initialize command with CanExecute logic
            SearchCommand = new RelayCommand(OnSearch, CanSearch);
            BuildPromptModel();
        }

        // CanExecute: require non-empty search text and valid date range
        private bool CanSearch()
        {
            if (string.IsNullOrWhiteSpace(SearchText)) return false;
            if (MinInfluence.HasValue && (MinInfluence < 0 || MinInfluence > 100)) return false;
            if (MaxInfluence.HasValue && (MaxInfluence < 0 || MaxInfluence > 100)) return false;
            if (MinInfluence.HasValue && MaxInfluence.HasValue && MinInfluence > MaxInfluence) return false;
            return true;
        }

        private void OnSearch()
        {
            var sqlFile = IsFactionSearch ? "Services\\Queries\\FactionSearch.sql" : "Services\\Queries\\SystemSearch.sql";
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, sqlFile);

            try
            {
                var sql = File.ReadAllText(path);
                var entries = new List<InfluenceEntry>();

                using (var conn = new SqlConnection(_connectionString))
                using (var cmd = new SqlCommand(sql, conn))
                {
                    conn.Open();
                    if (IsFactionSearch)
                        cmd.Parameters.AddWithValue("@FactionName", SearchText);
                    else
                        cmd.Parameters.AddWithValue("@SystemName", SearchText);

                    cmd.Parameters.AddWithValue("@StartDate", (object)StartDate ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@EndDate", (object)EndDate ?? DBNull.Value);

                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        entries.Add(new InfluenceEntry
                        {
                            LoggedAt = reader.GetDateTimeOffset(0),
                            Name = reader.GetString(1),
                            Value = Convert.ToDouble(reader.GetDecimal(2))
                        });
                    }
                }

                if (entries.Count == 0)
                {
                    BuildNoDataModel();
                }
                else
                {
                    if (MinInfluence.HasValue)
                        entries = entries
                          .Where(e => e.Value >= MinInfluence.Value)
                          .ToList();   // ← materialise back into a List

                    if (MaxInfluence.HasValue)
                        entries = entries
                          .Where(e => e.Value <= MaxInfluence.Value)
                          .ToList();
                    BuildModel(entries);
                }
            }
            catch (Exception ex)
            {
                // Log error via Trace
                Trace.TraceError($"Error loading influence history for '{SearchText}' (Dates: {StartDate} - {EndDate}): {ex}");
                // Notify user
                MessageBox.Show($"Error loading data: {ex.Message}", "Data Load Error", MessageBoxButton.OK, MessageBoxImage.Error);
                BuildPromptModel();
            }
        }

        private void BuildModel(IEnumerable<InfluenceEntry> entries)
        {
            var title = IsFactionSearch
                ? $"{SearchText} Influence by System"
                : $"{SearchText} Influence by Faction";

            var model = new PlotModel 
            { 
                Title = title
            };
            if (_paletteHelper.GetTheme().GetBaseTheme() == BaseTheme.Dark)
            {
                model.Background = OxyColors.Black;
                model.TextColor = OxyColors.White;
                model.SubtitleColor = OxyColors.LightGray;
                model.SelectionColor = OxyColors.LightGray; // For selected series
            }
            else
            {
                model.Background = OxyColors.White;
                model.TextColor = OxyColors.Black;
                model.SubtitleColor = OxyColors.DarkGray;
                model.SelectionColor = OxyColors.DarkGray; // For selected series
            }
            ;

            var legend = new Legend
            {
                LegendPlacement = LegendPlacement.Outside,
                LegendPosition = LegendPosition.RightTop,
                LegendOrientation = LegendOrientation.Vertical,
                LegendBorderThickness = 0
            };
            model.Legends.Add(legend);

            model.Axes.Add(new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                StringFormat = "yyyy-MM-dd",
                Title = "Date",
                IntervalType = DateTimeIntervalType.Days,
                MinorIntervalType = DateTimeIntervalType.Days
            });
            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "Influence (%)",
                Minimum = 0,
                Maximum = 100
            });

            // Guard against empty entries
            if (entries == null || !entries.Any())
            {
                BuildNoDataModel();
                return;
            }
            var groups = entries.GroupBy(e => e.Name);
            foreach (var grp in groups)
            {
                var series = new LineSeries
                {
                    Title = grp.Key,
                    MarkerType = MarkerType.Circle
                };
                foreach (var pt in grp.OrderBy(e => e.LoggedAt))
                {
                    var dt = pt.LoggedAt.UtcDateTime;
                    series.Points.Add(new DataPoint(DateTimeAxis.ToDouble(dt), pt.Value));
                }
                model.Series.Add(series);
            }

            PlotModel = model;
        }

        private void BuildPromptModel()
        {
            var empty = new PlotModel { Title = "Enter search criteria and click Search" };
            if (_paletteHelper.GetTheme().GetBaseTheme() == BaseTheme.Dark)
            {
                empty.Background = OxyColors.Black;
            }
            else
            {
                empty.Background = OxyColors.White;
            };
            empty.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Title = "Influence (%)" });
            empty.Axes.Add(new CategoryAxis { Position = AxisPosition.Bottom, Title = "Time" });
            PlotModel = empty;
        }

        private void BuildNoDataModel()
        {
            var nodata = new PlotModel { Title = "No data found for the given criteria" };
            nodata.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Title = "Influence (%)" });
            nodata.Axes.Add(new CategoryAxis { Position = AxisPosition.Bottom, Title = "Time" });
            PlotModel = nodata;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class InfluenceEntry
    {
        public DateTimeOffset LoggedAt { get; set; }
        public string Name { get; set; }
        public double Value { get; set; }
    }

    public class RelayCommand(Action execute, Func<bool> canExecute = null) : ICommand
    {
        private readonly Action _execute = execute;
        private readonly Func<bool> _canExecute = canExecute;
        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter) => _canExecute?.Invoke() ?? true;
        public void Execute(object parameter) => _execute();
        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}