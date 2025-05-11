using FalconsFactionMonitor.Themes;
using FalconsFactionMonitor.Helpers;
using MaterialDesignColors;
using MaterialDesignThemes.Wpf;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FalconsFactionMonitor.Windows
{
    public partial class OptionsWindow : BaseWindow
    {
        private readonly PaletteHelper _paletteHelper = new PaletteHelper();
        private readonly SwatchesProvider _swatchesProvider = new SwatchesProvider();
        private ITheme _pendingTheme = null;
        private ITheme _originalTheme;
        private ITheme _previewTheme;

        public OptionsWindow()
        {
            InitializeComponent();
            _originalTheme = CloneTheme(_paletteHelper.GetTheme());  // Save current theme

            // Attach listeners
            BaseThemeComboBox.SelectionChanged += ThemeControl_Changed;
            PrimaryColorComboBox.SelectionChanged += ThemeControl_Changed;
            AccentColorComboBox.SelectionChanged += ThemeControl_Changed;
            PresetThemeComboBox.SelectionChanged += PresetThemeComboBox_SelectionChanged;

            LoadColorOptions();
            SetCurrentThemeValues();
            ApplyButton.IsEnabled = false;

            AdjustWindowSize();
        }
        private void PresetThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string selectedPreset = (PresetThemeComboBox.SelectedItem as ComboBoxItem)?.Content as string;
            CustomThemePanel.Visibility = selectedPreset == "Custom" ? Visibility.Visible : Visibility.Collapsed;

            if (selectedPreset == "Custom")
            {
                _pendingTheme = null;
                ApplyButton.IsEnabled = true;
                AdjustWindowSize();
                return;
            }

            // Build theme based on preset
            BaseTheme baseTheme;
            Color primary, accent;

            switch (selectedPreset)
            {
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
                    primary = (Color)ColorConverter.ConvertFromString("#FF8000");
                    accent = (Color)ColorConverter.ConvertFromString("#FFB000");
                    break;
                default:
                    return;
            }

            _previewTheme = AppTheme.Create(baseTheme, primary, accent);
            _paletteHelper.SetTheme(_previewTheme);
            ApplyButton.IsEnabled = IsThemeChanged();
            AdjustWindowSize();
        }

        private void AdjustWindowSize()
        {
            if (PresetThemeComboBox.SelectedItem is ComboBoxItem item && (item.Content as string) == "Custom")
            {
                this.MinHeight = 360; // Adjust as needed for full custom panel visibility
                this.Height = 360;
            }
            else
            {
                this.MinHeight = 220; // Original compact height
                this.Height = 220;
            }
        }

        private void ThemeControl_Changed(object sender, EventArgs e)
        {
            var baseTheme = BaseThemeComboBox.SelectedIndex == 0 ? BaseTheme.Light : BaseTheme.Dark;

            Color primary, accent;

            if (PrimaryColorComboBox.SelectedItem is ComboBoxItem primaryItem)
                primary = (Color)primaryItem.Tag;
            else if (PrimaryColorComboBox.SelectedItem is Swatch primarySwatch)
                primary = primarySwatch.ExemplarHue.Color;
            else
                return;

            if (AccentColorComboBox.SelectedItem is ComboBoxItem accentItem)
                accent = (Color)accentItem.Tag;
            else if (AccentColorComboBox.SelectedItem is Swatch accentSwatch)
                accent = accentSwatch.AccentExemplarHue?.Color ?? primary;
            else
                accent = primary;

            _previewTheme = AppTheme.Create(baseTheme, primary, accent);
            _paletteHelper.SetTheme(_previewTheme);  // Preview only
            ApplyButton.IsEnabled = IsThemeChanged();
        }

        private void LoadColorOptions()
        {
            var swatches = _swatchesProvider.Swatches.ToList();

            // Create separate items for each ComboBox
            var blackPrimaryItem = new ComboBoxItem { Content = "Pure Black", Tag = Colors.Black };
            var whitePrimaryItem = new ComboBoxItem { Content = "Pure White", Tag = Colors.White };

            var blackAccentItem = new ComboBoxItem { Content = "Pure Black", Tag = Colors.Black };
            var whiteAccentItem = new ComboBoxItem { Content = "Pure White", Tag = Colors.White };

            PrimaryColorComboBox.Items.Add(blackPrimaryItem);
            PrimaryColorComboBox.Items.Add(whitePrimaryItem);
            AccentColorComboBox.Items.Add(blackAccentItem);
            AccentColorComboBox.Items.Add(whiteAccentItem);

            foreach (var swatch in swatches)
            {
                PrimaryColorComboBox.Items.Add(swatch);
                AccentColorComboBox.Items.Add(swatch);
            }
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

            _previewTheme = null;
            _pendingTheme = null;

            await Animations.FadeWindowAsync(this, 1.0, 0.0, 300);
            this.Close();
        }

        private async void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _paletteHelper.SetTheme(_originalTheme);  // Revert to stored theme
            _previewTheme = null;
            _pendingTheme = null;
            // Hide window
            await Animations.FadeWindowAsync(this, 1.0, 0.0, 300); // Fade out
            this.Close();
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            var systemBase = AppTheme.GetSystemTheme() ?? BaseTheme.Light;
            var systemPrimary = AppTheme.GetPrimaryColor(systemBase);
            var systemAccent = AppTheme.GetSystemAccentColor();

            // Build theme
            var theme = AppTheme.Create(systemBase, (Color)systemPrimary, systemAccent.Value);

            // Apply preview
            _previewTheme = theme;
            _paletteHelper.SetTheme(theme);

            // Update UI controls
            ForceThemeComboBoxSelections(theme);
            PresetThemeComboBox.SelectedItem = null;
            CustomThemePanel.Visibility = Visibility.Visible;

            // Resize if necessary
            AdjustWindowSize();

            // Enable Apply if changed
            ApplyButton.IsEnabled = IsThemeChanged();
        }

        private void ForceThemeComboBoxSelections(ITheme theme)
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
            if (AccentColorComboBox.SelectedItem == null)
                AccentColorComboBox.SelectedItem = PrimaryColorComboBox.SelectedItem;

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

        private ITheme CloneTheme(ITheme original)
        {
            // Extract base theme
            var baseTheme = original.GetBaseTheme(); // This returns BaseTheme enum (Light/Dark)

            // Use PrimaryMid and SecondaryMid to reconstruct
            var primary = original.PrimaryMid.Color;
            var accent = original.SecondaryMid.Color;

            // Reuse your own theme builder
            return AppTheme.Create(baseTheme, primary, accent);
        }

    }
}
