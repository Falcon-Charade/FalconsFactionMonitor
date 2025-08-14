using MaterialDesignThemes.Wpf;
using Microsoft.Data.SqlClient;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using Ookii.Dialogs.Wpf;
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
            set 
            { 
                _isFactionSearch = value; 
                OnPropertyChanged(); 
                OnPropertyChanged(nameof(IsSystemSearch)); 
                SearchCommand.RaiseCanExecuteChanged();
                ReportCommand.RaiseCanExecuteChanged();
            }
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
                ReportCommand.RaiseCanExecuteChanged();
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
                ReportCommand.RaiseCanExecuteChanged();
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
                ReportCommand.RaiseCanExecuteChanged();
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
                ReportCommand.RaiseCanExecuteChanged();
            }
        }

        // Influence range filters
        private double? _minInfluence;
        public double? MinInfluence
        {
            get => _minInfluence;
            set 
            { 
                _minInfluence = value;
                RaisePropertyChanged();
                SearchCommand.RaiseCanExecuteChanged();
                ReportCommand.RaiseCanExecuteChanged();
            }
        }

        private double? _maxInfluence;
        public double? MaxInfluence
        {
            get => _maxInfluence;
            set 
            {
                _maxInfluence = value;
                RaisePropertyChanged();
                SearchCommand.RaiseCanExecuteChanged();
                ReportCommand.RaiseCanExecuteChanged();
            }
        }

        // OxyPlot model
        private PlotModel _plotModel;
        public PlotModel PlotModel
        {
            get => _plotModel;
            private set { _plotModel = value; OnPropertyChanged(); }
        }

        // Registry storage for chosen report folder
        private const string ReportFolderRegistryName = "ReportSaveDirectory";

        private string _reportSaveDirectory;
        public string ReportSaveDirectory
        {
            get => _reportSaveDirectory;
            private set { _reportSaveDirectory = value; OnPropertyChanged(); }
        }

        // Commands for the dropdown menu
        public RelayCommand SetReportFolderCommand { get; }
        public RelayCommand OpenReportFolderCommand { get; }

        // Commands
        public RelayCommand SearchCommand { get; }
        public RelayCommand ReportCommand { get; }


        // Connection string
        private readonly string _connectionString = DatabaseConnectionBuilder.BuildConnectionString();

        public InfluenceHistoryViewModel()
        {
            // Initialize commands with CanExecute logic
            SearchCommand = new RelayCommand(OnSearch, CanSearch);
            ReportCommand = new RelayCommand(OnReport, CanReport);
            BuildPromptModel();

            // Load the previously chosen folder (or default)
            LoadReportSaveDirectory();

            // Wire up the Report Options menu commands
            SetReportFolderCommand = new RelayCommand(OnSetReportFolder);
            OpenReportFolderCommand = new RelayCommand(OnOpenReportFolder);
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
        private bool CanReport()
        {
            // Keep simple for now: require a non-empty search text.
            return !string.IsNullOrWhiteSpace(SearchText);
        }

        private void OnReport()
        {
            try
            {
                // Reuse the same SQL + params as the chart search
                var sqlFile = IsFactionSearch ? "Services\\Queries\\FactionSearch.sql" : "Services\\Queries\\SystemSearch.sql";
                var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, sqlFile);

                var entries = new List<InfluenceEntry>();
                var sql = File.ReadAllText(path);

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
                            Name = reader.GetString(1),                   // Name = System (when faction search) OR Faction (when system search)
                            Value = Convert.ToDouble(reader.GetDecimal(2)) // Influence
                        });
                    }
                }

                // Respect influence filters (optional but consistent with the chart)
                if (MinInfluence.HasValue)
                    entries = entries.Where(e => e.Value >= MinInfluence.Value).ToList();
                if (MaxInfluence.HasValue)
                    entries = entries.Where(e => e.Value <= MaxInfluence.Value).ToList();

                // === Compute latest & previous per Name to derive Trend and Note ===
                var latestWithTrendAndNote = entries
                    .GroupBy(e => e.Name)
                    .Select(g =>
                    {
                        var two = g.OrderByDescending(e => e.LoggedAt).Take(2).ToList();
                        var latest = two[0];
                        var prev = two.Count > 1 ? two[1] : null;

                        string trend =
                            prev == null ? "Unknown" :
                            latest.Value > prev.Value ? "Upward" :
                            latest.Value < prev.Value ? "Downward" :
                            "Unknown";

                        // Determine SystemName / FactionName pair for native check
                        string systemName, factionName;
                        if (IsFactionSearch)
                        {
                            // Searching by faction => Name = System, SearchText = Faction
                            systemName = latest.Name;
                            factionName = SearchText;
                        }
                        else
                        {
                            // Searching by system => Name = Faction, SearchText = System
                            systemName = SearchText;
                            factionName = latest.Name;
                        }

                        bool isNative = IsFactionNativeToSystem(systemName, factionName);

                        string note =
                            latest.Value > 65.0 ? "At risk of expansion" :
                            (latest.Value < 15.0 && !isNative) ? "At risk of retreat" :
                            string.Empty;

                        return new
                        {
                            latest.Name,
                            Influence = latest.Value,
                            Trend = trend,
                            Note = note
                        };
                    })
                    .OrderBy(x => x.Name)
                    .ToList();

                // === Write sectioned CSV ===
                Directory.CreateDirectory(ReportSaveDirectory);

                var mode = IsFactionSearch ? "Faction" : "System";
                var safeSearch = string.Concat((SearchText ?? "Unnamed")
                    .Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
                var fileName = $"InfluenceLatest_{mode}_{safeSearch}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                var fullPath = Path.Combine(ReportSaveDirectory, fileName);


                // Split into the three sections based on Note
                var expansionList = latestWithTrendAndNote.Where(r => r.Note == "At risk of expansion").ToList();
                var retreatList = latestWithTrendAndNote.Where(r => r.Note == "At risk of retreat").ToList();
                var neutralList = latestWithTrendAndNote.Where(r => string.IsNullOrEmpty(r.Note)).ToList();

                using (var sw = new StreamWriter(fullPath, false))
                {
                    void WriteSection(string title, IEnumerable<dynamic> list)
                    {
                        sw.WriteLine(title);
                        sw.WriteLine("----");
                        sw.WriteLine("Name,Influence,Trend,Note");
                        foreach (var row in list)
                        {
                            sw.WriteLine(
                                $"\"{row.Name}\"," +
                                $"{row.Influence.ToString("0.##", CultureInfo.InvariantCulture)}," +
                                $"\"{row.Trend}\"," +
                                $"\"{row.Note}\"");
                        }
                        sw.WriteLine("----");
                        sw.WriteLine(); // blank line after section
                    }

                    WriteSection("At risk of expansion", expansionList);
                    WriteSection("At risk of retreat", retreatList);
                    WriteSection("Present with no notes", neutralList);
                }

                MessageBox.Show($"Report created:\n{fullPath}", "Report Generated",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Trace.TraceError($"Error generating latest-data report for '{SearchText}': {ex}");
                MessageBox.Show($"Error generating report: {ex.Message}", "Report Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool IsFactionNativeToSystem(string systemName, string factionName)
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                using (var cmd = new SqlCommand("dbo.GetNativeInformation", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@SystemName", systemName ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@FactionName", factionName ?? (object)DBNull.Value);

                    conn.Open();
                    var result = cmd.ExecuteScalar(); // expects NativeSystemID when native; null when not
                    return result != null && result != DBNull.Value;
                }
            }
            catch (Exception ex)
            {
                // If the check fails, treat as non-native conservatively (and log)
                Trace.TraceError($"Native check failed for System='{systemName}', Faction='{factionName}': {ex}");
                return false;
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
        private void LoadReportSaveDirectory()
        {
            var defaultPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "FalconsFactionMonitor", "Reports");

            try
            {
                var saved = RegistryHelper.Get(ReportFolderRegistryName, null) as string;
                ReportSaveDirectory = string.IsNullOrWhiteSpace(saved) ? defaultPath : saved;
            }
            catch
            {
                ReportSaveDirectory = defaultPath;
            }
        }

        private void SaveReportSaveDirectory(string path)
        {
            try
            {
                RegistryHelper.Set(ReportFolderRegistryName, path);
            }
            catch (Exception ex)
            {
                Trace.TraceError($"Failed to save report folder to registry: {ex}");
            }
        }

        private void OnSetReportFolder()
        {
            try
            {
                var dlg = new VistaFolderBrowserDialog
                {
                    Description = "Select report save folder",
                    UseDescriptionForTitle = true,
                    ShowNewFolderButton = true,
                    SelectedPath = Directory.Exists(ReportSaveDirectory)
                        ? ReportSaveDirectory
                        : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                };

                if (dlg.ShowDialog() == true && !string.IsNullOrWhiteSpace(dlg.SelectedPath))
                {
                    ReportSaveDirectory = dlg.SelectedPath;
                    Directory.CreateDirectory(ReportSaveDirectory);
                    SaveReportSaveDirectory(ReportSaveDirectory);

                    MessageBox.Show($"Report save location set to:\n{ReportSaveDirectory}",
                        "Report Folder Updated", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError($"Error setting report folder: {ex}");
                MessageBox.Show($"Error setting report folder: {ex.Message}",
                    "Folder Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnOpenReportFolder()
        {
            try
            {
                if (!Directory.Exists(ReportSaveDirectory))
                    Directory.CreateDirectory(ReportSaveDirectory);

                Process.Start(new ProcessStartInfo("explorer.exe", ReportSaveDirectory) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Trace.TraceError($"Error opening report folder '{ReportSaveDirectory}': {ex}");
                MessageBox.Show($"Error opening folder: {ex.Message}",
                    "Folder Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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