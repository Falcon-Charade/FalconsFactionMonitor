using FalconsFactionMonitor.Helpers;
using FalconsFactionMonitor.Themes;
using MaterialDesignColors;
using MaterialDesignThemes.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace FalconsFactionMonitor.Windows
{
    public partial class OptionsWindow : BaseWindow
    {
        private readonly PaletteHelper _paletteHelper = new();
        private readonly SwatchesProvider _swatchesProvider = new();
        private Theme _originalTheme;
        private Theme _previewTheme;
        private string _originalLanguage;
        private double _originalFontSize;
        private string _pendingLanguage;
        private double _pendingFontSize;


        public OptionsWindow()
        {
            InitializeComponent(); // must come first

            // Attach event handlers (already present)
            BaseThemeComboBox.SelectionChanged += ThemeControl_Changed;
            PrimaryColorComboBox.SelectionChanged += ThemeControl_Changed;
            AccentColorComboBox.SelectionChanged += ThemeControl_Changed;
            LanguageComboBox.SelectionChanged += LanguageComboBox_SelectionChanged;
            PresetThemeComboBox.SelectionChanged += PresetThemeComboBox_SelectionChanged;
            FontSizeComboBox.SelectionChanged += ControlChanged;

            LoadColorOptions();
            SetCurrentThemeValues();

            var currentTheme = _paletteHelper.GetTheme();
            _originalTheme = currentTheme != null ? CloneTheme(currentTheme) : AppTheme.Create(BaseTheme.Light, Colors.Purple, Colors.Blue);
            SetPresetThemeSelectionFromTheme(_originalTheme);

            _originalLanguage = LanguageHelper.GetLanguageFromRegistry();
            LanguageHelper.SetLanguage(_originalLanguage);
            _pendingLanguage = _originalLanguage;


            _originalFontSize = double.TryParse(AppSettings.Get("FontSize", "12"), out var size) ? size : 12;
            FontSizeComboBox.ItemsSource = new[] { "10", "12", "14", "16", "18", "20" };
            FontSizeComboBox.SelectedItem = _originalFontSize.ToString();

            // ✅ SET SELECTED LANGUAGE AFTER COMBOBOX IS LOADED
            Dispatcher.InvokeAsync(() =>
            {
                LanguageComboBox.SelectedValue = _originalLanguage;
            }, DispatcherPriority.Loaded);

            ApplyButton.IsEnabled = false;
            AdjustWindowSize();
        }

        private void PresetThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string selectedPreset = (PresetThemeComboBox.SelectedItem as ComboBoxItem)?.Content as string;
            string customLocalized = (string)FindResource("Options_ThemePreset_Custom");
            CustomThemePanel.Visibility = selectedPreset == customLocalized ? Visibility.Visible : Visibility.Collapsed;

            // Build theme based on preset
            BaseTheme baseTheme;
            Color primary, accent;


            switch (selectedPreset)
            {
                case "System Default":
                    baseTheme = AppTheme.GetSystemTheme() ?? BaseTheme.Light;
                    primary = (Color)AppTheme.GetPrimaryColor(baseTheme);
                    accent = (Color)AppTheme.GetSystemAccentColor();
                    break;
                case "Light":
                    baseTheme = BaseTheme.Light;
                    primary = (Color)ColorConverter.ConvertFromString("#7B1FA2");
                    accent = Colors.LightGray;
                    break;
                case "Dark":
                    baseTheme = BaseTheme.Dark;
                    primary = (Color)ColorConverter.ConvertFromString("#64B5F6");
                    accent = Colors.Gray;
                    break;
                case "High Contrast Light":
                    baseTheme = BaseTheme.Light;
                    primary = Colors.White;
                    accent = Colors.Black;
                    break;
                case "High Contrast Dark":
                    baseTheme = BaseTheme.Dark;
                    primary = Colors.Black;
                    accent = Colors.Yellow;
                    break;
                case "Elite: Dangerous":
                    baseTheme = BaseTheme.Dark;
                    primary = (Color)ColorConverter.ConvertFromString("#FF7000");
                    accent = (Color)ColorConverter.ConvertFromString("#FFD000");
                    break;
                case "Custom":
                    CustomThemePanel.Visibility = Visibility.Visible;
                    baseTheme = BaseThemeComboBox.SelectedIndex == 0 ? BaseTheme.Light : BaseTheme.Dark;
                    object psel = PrimaryColorComboBox.SelectedItem;
                    if (psel is Color pc)
                        primary = pc;
                    else if (psel is Swatch psw)
                        primary = psw.ExemplarHue.Color;
                    else
                        primary = Colors.Purple;

                    object asel = AccentColorComboBox.SelectedItem;
                    if (asel is Color ac)
                        accent = ac;
                    else if (asel is Swatch asw)
                        accent = asw.AccentExemplarHue?.Color ?? primary;
                    else
                        accent = primary;
                    break;
                default:
                    return;
            }

            _previewTheme = AppTheme.Create(baseTheme, primary, accent);
            _paletteHelper.SetTheme(_previewTheme);
            ApplyButton.IsEnabled = true;
            bool isCustom = selectedPreset == customLocalized;
            CustomThemePanel.IsEnabled = isCustom;
            AdjustWindowSize();
        }

        private void AdjustWindowSize()
        {
            if (PresetThemeComboBox.SelectedItem is ComboBoxItem item && (item.Content as string) == "Custom")
            {
                this.MinHeight = 420; // Adjust as needed for full custom panel visibility
            }
            else
            {
                this.MinHeight = 285; // Original compact height
            }
        }

        private void ThemeControl_Changed(object sender, EventArgs e)
        {
            var baseTheme = BaseThemeComboBox.SelectedIndex == 0
                            ? BaseTheme.Light
                            : BaseTheme.Dark;

            Color primary, accent;

            object sel = PrimaryColorComboBox.SelectedItem;
            if (sel is Color c)
                primary = c;
            else if (sel is Swatch sw)
                primary = sw.ExemplarHue.Color;
            else
                return;

            object sel2 = AccentColorComboBox.SelectedItem;
            if (sel2 is Color c2)
                accent = c2;
            else if (sel2 is Swatch sw2)
                accent = sw2.AccentExemplarHue?.Color ?? primary;
            else
                accent = primary;

            _previewTheme = AppTheme.Create(baseTheme, primary, accent);
            _paletteHelper.SetTheme(_previewTheme);  // Preview only
            ApplyButton.IsEnabled = IsThemeChanged();
        }
        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LanguageComboBox.SelectedItem is ComboBoxItem item && item.Tag is string langCode)
            {
                _pendingLanguage = langCode;

                // ① swap in the new strings
                LanguageHelper.SetLanguage(_pendingLanguage);

                // ② update any static labels
                UpdateLocalizedText();

                // ③ force the color pickers back through LoadColorOptions
                LoadColorOptions();
                SetCurrentThemeValues();

                // ④ force the selected item to re-template
                PrimaryColorComboBox.Items.Refresh();
                AccentColorComboBox.Items.Refresh();

                // ⑤ force the selected‐item ContentPresenter to re‐generate
                var primSel = PrimaryColorComboBox.SelectedItem;
                PrimaryColorComboBox.SelectedItem = null;
                PrimaryColorComboBox.SelectedItem = primSel;

                var accSel = AccentColorComboBox.SelectedItem;
                AccentColorComboBox.SelectedItem = null;
                AccentColorComboBox.SelectedItem = accSel;

                // ⑥ finally, re-evaluate enablement including language change
                ControlChanged(sender, e); // triggers ApplyButton.IsEnabled logic
            }
        }
        private void LoadColorOptions()
        {
            // ▲ PREPEND pure‐black & pure‐white with their own resource keys
            var items = new List<LocalizedSwatch>
            {
                // for the PrimaryColorComboBox
                new(Colors.Black, "Options_Custom_PrimaryColor_PureBlack"),
                new(Colors.White, "Options_Custom_PrimaryColor_PureWhite")
            };
            
            // ▲ THEN add all Material swatches
            items.AddRange(
                _swatchesProvider
                .Swatches
                .Select(s => new LocalizedSwatch(s))
                );

            // plus your black/white (you can wrap those too)
            PrimaryColorComboBox.ItemsSource = items;
            AccentColorComboBox.ItemsSource = items;
        }


        private void SetCurrentThemeValues()
        {
            ITheme theme = _paletteHelper.GetTheme();

            // Set BaseTheme ComboBox
            BaseThemeComboBox.SelectedIndex = theme.GetBaseTheme() == BaseTheme.Light ? 0 : 1;

            // Set current primary and accent colors
            var swatches = _swatchesProvider.Swatches.ToList();

            var currentPrimary = swatches.FirstOrDefault(s => s.ExemplarHue.Color == theme.PrimaryMid.Color);
            var currentAccent = swatches.FirstOrDefault(s => s.AccentExemplarHue?.Color == theme.SecondaryMid.Color);

            if (currentPrimary != null)
                PrimaryColorComboBox.SelectedItem = currentPrimary;

            if (currentAccent != null)
                AccentColorComboBox.SelectedItem = currentAccent;
        }

        private async void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            if (_previewTheme != null)
            {
                _paletteHelper.SetTheme(_previewTheme);
                AppTheme.SaveThemeToRegistry(_previewTheme.GetBaseTheme(), _previewTheme.PrimaryMid.Color, _previewTheme.SecondaryMid.Color);
                _originalTheme = CloneTheme(_previewTheme); // Track new original
            }

            if (IsLanguageFontChanged())
            {
                LanguageHelper.SetLanguageToRegistry(_pendingLanguage); // ✅ Store to registry
                AppSettings.Set("FontSize", _pendingFontSize.ToString());
                Application.Current.Resources["GlobalFontSize"] = _pendingFontSize;
                _originalLanguage = _pendingLanguage;
                _originalFontSize = _pendingFontSize;
            }


            _previewTheme = null;

            await Animations.FadeWindowAsync(this, 1.0, 0.0, 300);

            // ✅ Restore MainWindow if it was suppressed
            if (Application.Current.MainWindow is MainWindow mainWindow)
            {
                mainWindow.SuppressRestoreAfterOptions = false;
                mainWindow.Show();
                await Animations.FadeWindowAsync(mainWindow, 0.0, 1.0, 300); // Fade in MainWindow
            }
            this.Close();
        }

        private async void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _paletteHelper.SetTheme(_originalTheme);  // Revert to stored theme
            LanguageHelper.SetLanguage(_originalLanguage);
            FontSizeComboBox.SelectedItem = _originalFontSize.ToString();
            _previewTheme = null;

            // Hide window
            await Animations.FadeWindowAsync(this, 1.0, 0.0, 300); // Fade out

            // ✅ Restore MainWindow if it was suppressed
            if (Application.Current.MainWindow is MainWindow mainWindow)
            {
                mainWindow.SuppressRestoreAfterOptions = false;
                mainWindow.Show();
                await Animations.FadeWindowAsync(mainWindow, 0.0, 1.0, 300); // Fade in MainWindow
            }
            this.Close();
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            var systemBase = AppTheme.GetSystemTheme() ?? BaseTheme.Light;
            var systemPrimary = AppTheme.GetPrimaryColor(systemBase);
            var systemAccent = AppTheme.GetSystemAccentColor();

            // Build theme
            var theme = AppTheme.Create(systemBase, (Color)systemPrimary, systemAccent ?? Colors.Blue);

            // Apply preview
            _previewTheme = theme;
            _paletteHelper.SetTheme(theme);

            // Update UI controls
            ForceThemeComboBoxSelections(theme);

            // ✅ Select the "System Default" preset in the dropdown
            foreach (ComboBoxItem item in PresetThemeComboBox.Items)
            {
                if ((string)item.Content == (string)FindResource("Options_ThemePreset_SystemDefault"))
                {
                    PresetThemeComboBox.SelectedItem = item;
                    break;
                }
            }

            CustomThemePanel.Visibility = Visibility.Collapsed;

            // Resize if necessary
            AdjustWindowSize();

            // ✅ Enable Apply
            ApplyButton.IsEnabled = IsThemeChanged();
        }


        private void ForceThemeComboBoxSelections(Theme theme)
        {
            // Match primary color
            foreach (var item in PrimaryColorComboBox.Items)
            {
                if (item is ComboBoxItem comboItem && ((Color)comboItem.Tag) == theme.PrimaryMid.Color)
                {
                    PrimaryColorComboBox.SelectedItem = comboItem;
                    break;
                }
                else if (item is Swatch swatch && swatch.ExemplarHue.Color == theme.PrimaryMid.Color)
                {
                    PrimaryColorComboBox.SelectedItem = swatch;
                    break;
                }
            }

            // Match accent color
            foreach (var item in AccentColorComboBox.Items)
            {
                if (item is ComboBoxItem comboItem && ((Color)comboItem.Tag) == theme.SecondaryMid.Color)
                {
                    AccentColorComboBox.SelectedItem = comboItem;
                    break;
                }
                else if (item is Swatch swatch && swatch.AccentExemplarHue?.Color == theme.SecondaryMid.Color)
                {
                    AccentColorComboBox.SelectedItem = swatch;
                    break;
                }
            }

            // Fallback if no accent match
            AccentColorComboBox.SelectedItem ??= PrimaryColorComboBox.SelectedItem;

            BaseThemeComboBox.SelectedIndex = theme.GetBaseTheme() == BaseTheme.Light ? 0 : 1;
        }

        private bool IsThemeChanged()
        {
            if (_previewTheme == null || _originalTheme == null)
                return false;

            return _previewTheme.GetBaseTheme() != _originalTheme.GetBaseTheme() ||
                   _previewTheme.PrimaryMid.Color != _originalTheme.PrimaryMid.Color ||
                   _previewTheme.SecondaryMid.Color != _originalTheme.SecondaryMid.Color;
        }

        private static Theme CloneTheme(ITheme original)
        {
            // Extract base theme
            var baseTheme = original.GetBaseTheme(); // This returns BaseTheme enum (Light/Dark)

            // Use PrimaryMid and SecondaryMid to reconstruct
            var primary = original.PrimaryMid.Color;
            var accent = original.SecondaryMid.Color;

            // Reuse your own theme builder
            return AppTheme.Create(baseTheme, primary, accent);
        }
        private void ControlChanged(object sender, EventArgs e)
        {
            _pendingLanguage = (LanguageComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? _originalLanguage;
            _pendingFontSize = double.TryParse(FontSizeComboBox.SelectedItem?.ToString(), out var size) ? size : _originalFontSize;

            // Apply preview logic here if needed (e.g. live font size preview)
            PreviewFontSize(_pendingFontSize);

            ApplyButton.IsEnabled = IsThemeChanged() || IsLanguageFontChanged();
        }
        private bool IsLanguageFontChanged()
        {
            return _pendingLanguage != _originalLanguage || _pendingFontSize != _originalFontSize;
        }
        private void PreviewFontSize(double size)
        {
            this.FontSize = size;
            this.Dispatcher.InvokeAsync(() =>
            {
                MainContentGrid.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                MainContentGrid.UpdateLayout();
                var desiredSize = MainContentGrid.DesiredSize;

                if (desiredSize.Width > this.Width || desiredSize.Height > this.Height)
                {
                    this.Width = Math.Min(desiredSize.Width + 40, SystemParameters.WorkArea.Width);
                    this.Height = Math.Min(desiredSize.Height + 40, SystemParameters.WorkArea.Height);
                }
            });
        }
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            // Only show main window if we weren't doing a manual-restart
            if (this.Tag?.ToString() != "manual-restart" &&
                Application.Current.MainWindow is MainWindow mainWindow)
            {
                mainWindow.SuppressRestoreAfterOptions = false;
                mainWindow.Show();
            }
        }
        private void SetPresetThemeSelectionFromTheme(Theme theme)
        {
            string presetMatch;

            // Detect "System Default"
            var systemBase = AppTheme.GetSystemTheme();
            var systemPrimary = AppTheme.GetPrimaryColor(systemBase ?? BaseTheme.Light);
            var systemAccent = AppTheme.GetSystemAccentColor();

            var systemTheme = AppTheme.Create(systemBase ?? BaseTheme.Light, (Color)systemPrimary, systemAccent ?? Colors.Blue);

            if (theme.GetBaseTheme() == systemTheme.GetBaseTheme() &&
                theme.PrimaryMid.Color == systemTheme.PrimaryMid.Color &&
                theme.SecondaryMid.Color == systemTheme.SecondaryMid.Color)
            {
                presetMatch = "System Default";
            }
            else if (theme.GetBaseTheme() == BaseTheme.Light &&
                theme.PrimaryMid.Color == (Color)ColorConverter.ConvertFromString("#7B1FA2") &&
                theme.SecondaryMid.Color == Colors.LightGray)
                presetMatch = "Light";

            else if (theme.GetBaseTheme() == BaseTheme.Dark &&
                     theme.PrimaryMid.Color == (Color)ColorConverter.ConvertFromString("#64B5F6") &&
                     theme.SecondaryMid.Color == Colors.Gray)
                presetMatch = "Dark";

            else if (theme.GetBaseTheme() == BaseTheme.Light &&
                     theme.PrimaryMid.Color == Colors.White &&
                     theme.SecondaryMid.Color == Colors.Black)
                presetMatch = "High Contrast Light";

            else if (theme.GetBaseTheme() == BaseTheme.Dark &&
                     theme.PrimaryMid.Color == Colors.Black &&
                     theme.SecondaryMid.Color == Colors.Yellow)
                presetMatch = "High Contrast Dark";

            else if (theme.GetBaseTheme() == BaseTheme.Dark &&
                     theme.PrimaryMid.Color == (Color)ColorConverter.ConvertFromString("#FF7000") &&
                     theme.SecondaryMid.Color == (Color)ColorConverter.ConvertFromString("#FFD000"))
                presetMatch = "Elite: Dangerous";

            else
                presetMatch = (string)FindResource("Options_ThemePreset_Custom");

            foreach (ComboBoxItem item in PresetThemeComboBox.Items)
            {
                if ((string)item.Content == presetMatch)
                {
                    PresetThemeComboBox.SelectedItem = item;
                    break;
                }
            }

            CustomThemePanel.Visibility = presetMatch == (string)FindResource("Options_ThemePreset_Custom") ? Visibility.Visible : Visibility.Collapsed;
        }
        private void UpdateLocalizedText()
        {
            LanguageHelper.SetLanguage(_pendingLanguage);
            LanguageHelper.RefreshDynamicResources(this); // 'this' = OptionsWindow
            foreach (var child in LogicalTreeHelper.GetChildren(this).OfType<FrameworkElement>())
            {
                if (child is ComboBox comboBox)
                {
                    comboBox.Items.Refresh();
                }
                else if (child is TextBlock textBlock)
                {
                    textBlock.InvalidateVisual();
                }
            }
        }
        private void CSVPathButton_Click(object sender, RoutedEventArgs e)
        {
            if (FolderInteractions.Logic("CSV") == "Changed") 
            {
                ApplyButton.IsEnabled = true; // Enable Apply button if CSV path changed
            };
        }
        private void JournalPathButton_Click(object sender, RoutedEventArgs e)
        {
            if (FolderInteractions.Logic("Journal") == "Changed")
            {
                ApplyButton.IsEnabled = true; // Enable Apply button if Journal path changed
            };
        }
    }
}