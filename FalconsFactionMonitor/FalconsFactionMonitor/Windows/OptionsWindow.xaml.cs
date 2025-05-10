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

        public OptionsWindow()
        {
            InitializeComponent();
            BaseThemeComboBox.SelectionChanged += ThemeControl_Changed;
            PrimaryColorComboBox.SelectionChanged += ThemeControl_Changed;
            AccentColorComboBox.SelectionChanged += ThemeControl_Changed;
            LoadColorOptions();
            SetCurrentThemeValues();
            ApplyButton.IsEnabled = false;
        }
        private void ThemeControl_Changed(object sender, EventArgs e)
        {
            ApplyButton.IsEnabled = true;//IsThemeChanged();
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
            var baseTheme = BaseThemeComboBox.SelectedIndex == 0 ? BaseTheme.Light : BaseTheme.Dark;

            Color primaryColor;
            Color accentColor;

            if (PrimaryColorComboBox.SelectedItem is ComboBoxItem primaryItem)
                primaryColor = (Color)primaryItem.Tag;
            else if (PrimaryColorComboBox.SelectedItem is Swatch primarySwatch)
                primaryColor = primarySwatch.ExemplarHue.Color;
            else
            {
                MessageBox.Show("Please select a primary color.");
                return;
            }

            if (AccentColorComboBox.SelectedItem is ComboBoxItem accentItem)
                accentColor = (Color)accentItem.Tag;
            else if (AccentColorComboBox.SelectedItem is Swatch accentSwatch)
                accentColor = accentSwatch.AccentExemplarHue?.Color ?? primaryColor;
            else
                accentColor = primaryColor;

            var theme = AppTheme.Create(baseTheme, primaryColor, accentColor);
            _paletteHelper.SetTheme(theme);
            AppTheme.SaveThemeToRegistry(baseTheme, primaryColor, accentColor);

            // Hide window
            await Animations.FadeWindowAsync(this, 1.0, 0.0, 300); // Fade out
            this.Close();
        }



        private async void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // Hide window
            await Animations.FadeWindowAsync(this, 1.0, 0.0, 300); // Fade out
            this.Close();
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            var paletteHelper = new PaletteHelper();
            var systemTheme = AppTheme.GetSystemTheme();
            var primarycolor = AppTheme.GetPrimaryColor(systemTheme ?? BaseTheme.Light);
            var accent = AppTheme.GetSystemAccentColor();

            // Apply the theme
            var theme = AppTheme.Create(systemTheme.Value, (System.Windows.Media.Color)primarycolor, accent.Value);
            paletteHelper.SetTheme(theme);

            // Clear saved theme
            Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(@"HKEY_CURRENT_USER\Software\FalconCharade\FalconsFactionMonitor\Theme", false);

            // Force UI to reflect current applied theme
            ForceThemeComboBoxSelections(theme);

            // Refresh Apply button state
            ThemeControl_Changed(null, null);
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
            var currentTheme = _paletteHelper.GetTheme();
            var baseTheme = BaseThemeComboBox.SelectedIndex == 0 ? BaseTheme.Light : BaseTheme.Dark;
            if (currentTheme.GetBaseTheme() != baseTheme)
                return true;
            var primarySwatch = PrimaryColorComboBox.SelectedItem as Swatch;
            if (currentTheme.PrimaryMid.Color != primarySwatch?.ExemplarHue.Color)
                return true;
            else if (primarySwatch == null)
                return false;
            var accentSwatch = AccentColorComboBox.SelectedItem as Swatch;
            var accentColor = accentSwatch?.AccentExemplarHue?.Color ?? primarySwatch?.ExemplarHue?.Color;
            if (currentTheme.SecondaryMid.Color != accentColor)
                return true;


            return currentTheme.GetBaseTheme() != baseTheme ||
                   currentTheme.PrimaryMid.Color != primarySwatch.ExemplarHue.Color ||
                   currentTheme.SecondaryMid.Color != accentColor;
        }

    }
}
