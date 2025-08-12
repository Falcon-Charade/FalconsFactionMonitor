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
        private string _originalUsername;
        private string _originalPassword;
        private string _pendingUsername;
        private string _pendingPassword;


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

            // Retrieve original Login from registry
            _originalUsername = RegistryHelper.Get("UserId", "");
            _originalPassword = RegistryHelper.Get("Password", "");
            _pendingUsername = _originalUsername;
            _pendingPassword = _originalPassword;

            // Prefer saved theme from registry; fallback to current palette; then to default
            var(rb, rp, rs) = AppTheme.LoadThemeFromRegistry();
            if (rb.HasValue && rp.HasValue && rs.HasValue)
            {
                _originalTheme = AppTheme.Create(rb.Value, rp.Value, rs.Value);
            }
            else
            {
                var currentTheme = _paletteHelper.GetTheme();
                _originalTheme = currentTheme != null
                    ? CloneTheme(currentTheme)
                    : AppTheme.Create(BaseTheme.Light, Colors.Purple, Colors.Blue);
            }
            
            LoadColorOptions();
            SetCurrentThemeValues(_originalTheme);
            SetPresetThemeSelectionFromTheme(_originalTheme);

            _originalLanguage = LanguageHelper.GetLanguageFromRegistry();
            LanguageHelper.SetLanguage(_originalLanguage);
            _pendingLanguage = _originalLanguage;


            _originalFontSize = double.TryParse(RegistryHelper.Get("FontSize", "12"), out var size) ? size : 12;
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

        private void SetCustomPanelState(bool isCustom)
        {
            CustomThemePanel.Visibility = isCustom ? Visibility.Visible : Visibility.Collapsed;
            CustomThemePanel.IsEnabled = isCustom;

            // harden children too, in case XAML has local IsEnabled bindings
            PrimaryColorComboBox.IsEnabled = isCustom;
            AccentColorComboBox.IsEnabled = isCustom;
        }

        // ---- helper extraction so "Custom" works with more selection shapes ----
        private Color GetPrimaryFrom(object sel)
        {
            // tolerate null (startup / template churn)
            if (sel is null)
                return (_paletteHelper.GetTheme() ?? AppTheme.Create(BaseTheme.Light, Colors.Purple, Colors.Blue)).PrimaryMid.Color;

            // our wrapper
            if (sel is LocalizedSwatch ls) return ls.PrimaryColor;
            // direct color
            if (sel is Color c) return c;
            // Material swatch
            if (sel is Swatch sw) return sw.ExemplarHue.Color;
            // Some XAMLs provide ComboBoxItem (Tag or Content may hold data)
            if (sel is ComboBoxItem cbi)
            {
                if (cbi.Tag is Color tc) return tc;
                if (cbi.Tag is Swatch tsw) return tsw.ExemplarHue.Color;
                if (cbi.Tag is LocalizedSwatch tls) return tls.PrimaryColor;
                if (cbi.Content is Color cc) return cc;
                if (cbi.Content is Swatch csw) return csw.ExemplarHue.Color;
                if (cbi.Content is LocalizedSwatch cls) return cls.PrimaryColor;
                
                // attempt parse if content is a hex string like "#FF7000"
                if (cbi.Content is string s && s.StartsWith("#"))
                {
                    var parsed = (Color?)ColorConverter.ConvertFromString(s);
                    if (parsed.HasValue) return parsed.Value;
                }
            }
            // fallback to current theme primary to avoid exceptions
            return (_paletteHelper.GetTheme() ?? AppTheme.Create(BaseTheme.Light, Colors.Purple, Colors.Blue)).PrimaryMid.Color;
        }

        private Color GetAccentFrom(object sel, Color fallbackPrimary)
        {
            if (sel is null) return fallbackPrimary;
            if (sel is LocalizedSwatch ls) return ls.AccentColor ?? fallbackPrimary;
            if (sel is Color c) return c;
            if (sel is Swatch sw) return sw.AccentExemplarHue?.Color ?? fallbackPrimary;
            if (sel is ComboBoxItem cbi)
            {
                if (cbi.Tag is Color tc) return tc;
                if (cbi.Tag is Swatch tsw) return tsw.AccentExemplarHue?.Color ?? fallbackPrimary;
                if (cbi.Tag is LocalizedSwatch tls) return tls.AccentColor ?? fallbackPrimary;
                if (cbi.Content is Color cc) return cc;
                if (cbi.Content is Swatch csw) return csw.AccentExemplarHue?.Color ?? fallbackPrimary;
                if (cbi.Content is LocalizedSwatch cls) return cls.AccentColor ?? fallbackPrimary;
                if (cbi.Content is string s && s.StartsWith("#"))
                {
                    var parsed = (Color?)ColorConverter.ConvertFromString(s);
                    if (parsed.HasValue) return parsed.Value;
                }
            }
            return fallbackPrimary;
        }
private void CredentialControl_Changed(object sender, RoutedEventArgs e)
        {
            _pendingUsername = UsernameTextBox.Text;
            _pendingPassword = PasswordBox.Password;
            ApplyButton.IsEnabled = IsThemeChanged() || IsLanguageFontChanged() || AreCredentialsChanged();
        }

        private bool AreCredentialsChanged()
        {
            return _pendingUsername != _originalUsername || _pendingPassword != _originalPassword;
        }

        private void PresetThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string selectedPreset = (PresetThemeComboBox.SelectedItem as ComboBoxItem)?.Content as string;
            string L(string key) => (string)FindResource(key);
            string customLocalized = L("Options_ThemePreset_Custom");

            // Build theme based on preset
            BaseTheme baseTheme;
            Color primary, accent;


            switch (selectedPreset)
            {
                case var _ when selectedPreset == L("Options_ThemePreset_SystemDefault"):
                    baseTheme = AppTheme.GetSystemTheme() ?? BaseTheme.Light;
                    primary = (Color)AppTheme.GetPrimaryColor(baseTheme);
                    accent = (Color)AppTheme.GetSystemAccentColor();
                    break;
                case var _ when selectedPreset == L("Options_ThemePreset_Light"):
                    baseTheme = BaseTheme.Light;
                    primary = (Color)ColorConverter.ConvertFromString("#7B1FA2");
                    accent = Colors.LightGray;
                    break;
                case var _ when selectedPreset == L("Options_ThemePreset_Dark"):
                    baseTheme = BaseTheme.Dark;
                    primary = (Color)ColorConverter.ConvertFromString("#64B5F6");
                    accent = Colors.Gray;
                    break;
                case var _ when selectedPreset == L("Options_ThemePreset_HighContrastLight"):
                    baseTheme = BaseTheme.Light;
                    primary = Colors.White;
                    accent = Colors.Black;
                    break;
                case var _ when selectedPreset == L("Options_ThemePreset_HighContrastDark"):
                    baseTheme = BaseTheme.Dark;
                    primary = Colors.Black;
                    accent = Colors.Yellow;
                    break;
                case var _ when selectedPreset == L("Options_ThemePreset_EliteDangerous"):
                    baseTheme = BaseTheme.Dark;
                    primary = (Color)ColorConverter.ConvertFromString("#FF7000");
                    accent = (Color)ColorConverter.ConvertFromString("#FFD000");
                    break;
                case var _ when selectedPreset == customLocalized:
                    baseTheme = BaseThemeComboBox.SelectedIndex == 0 ? BaseTheme.Light : BaseTheme.Dark;
                    primary = GetPrimaryFrom(PrimaryColorComboBox.SelectedItem);
                    accent = GetAccentFrom(AccentColorComboBox.SelectedItem, primary);
                    break;
                default:
                    // If we can't match (shouldn't happen), keep panel state and bail gracefully.
                    ApplyButton.IsEnabled = IsThemeChanged();
                    SetCustomPanelState(selectedPreset == customLocalized);
                    AdjustWindowSize();
                    return;
            }

            _previewTheme = AppTheme.Create(baseTheme, primary, accent);
            _paletteHelper.SetTheme(_previewTheme);
            ApplyButton.IsEnabled = true;
            bool isCustom = selectedPreset == (string)FindResource("Options_ThemePreset_Custom");
            SetCustomPanelState(selectedPreset == customLocalized);
            AdjustWindowSize();
        }

        private void AdjustWindowSize()
        {
            var customLocalized = (string)FindResource("Options_ThemePreset_Custom");
            if (PresetThemeComboBox.SelectedItem is ComboBoxItem item && (item.Content as string) == customLocalized)
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

            // tolerate LocalizedSwatch, Color, Swatch, ComboBoxItem, or null for both selectors
            var primary = GetPrimaryFrom(PrimaryColorComboBox.SelectedItem);
            var accent = GetAccentFrom(AccentColorComboBox.SelectedItem, primary);

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
                // keep whatever theme the user is previewing; else original; else current
                var themeForSelection = _previewTheme ?? _originalTheme ?? _paletteHelper.GetTheme();
                if (themeForSelection != null)
                {
                    SetCurrentThemeValues(themeForSelection);
                    SetPresetThemeSelectionFromTheme(themeForSelection); // reselect preset with localized label
                    SetCustomPanelState(((PresetThemeComboBox.SelectedItem as ComboBoxItem)?.Content as string)
                        == (string)FindResource("Options_ThemePreset_Custom"));
                }
                    SetCurrentThemeValues(themeForSelection);

                // Update preset combo to localized labels and re-apply panel enable/visibility
                if (themeForSelection != null)
                    SetPresetThemeSelectionFromTheme(themeForSelection);
                
                var customLocalized = (string)FindResource("Options_ThemePreset_Custom");
                SetCustomPanelState(((PresetThemeComboBox.SelectedItem as ComboBoxItem)?.Content as string) == customLocalized);


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

        private void SetCurrentThemeValues(ITheme theme)
        {
            if (theme is null) return;

            // Set BaseTheme ComboBox
            BaseThemeComboBox.SelectedIndex = theme.GetBaseTheme() == BaseTheme.Light? 0 : 1;

            // Find matching LocalizedSwatch items in the bound ItemsSource
            var primaryItem = PrimaryColorComboBox.Items
                .OfType<LocalizedSwatch>()
                .FirstOrDefault(i => i.PrimaryColor == theme.PrimaryMid.Color);

            var accentItem = AccentColorComboBox.Items
                .OfType<LocalizedSwatch>()
                .FirstOrDefault(i => (i.AccentColor ?? i.PrimaryColor) == theme.SecondaryMid.Color);

            if (primaryItem != null) PrimaryColorComboBox.SelectedItem = primaryItem;
            if (accentItem  != null) AccentColorComboBox.SelectedItem  = accentItem;
            AccentColorComboBox.SelectedItem ??= PrimaryColorComboBox.SelectedItem;
        }

        private async void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            if (_previewTheme != null)
            {
                _paletteHelper.SetTheme(_previewTheme);
                AppTheme.SaveThemeToRegistry(_previewTheme.GetBaseTheme(), _previewTheme.PrimaryMid.Color, _previewTheme.SecondaryMid.Color);
                _originalTheme = CloneTheme(_previewTheme); // Track new original
            }

            if (AreCredentialsChanged())
            {
                RegistryHelper.Set("UserId", _pendingUsername);
                RegistryHelper.Set("Password", _pendingPassword);
                _originalUsername = _pendingUsername;
                _originalPassword = _pendingPassword;
            }

            if (IsLanguageFontChanged())
            {
                LanguageHelper.SetLanguageToRegistry(_pendingLanguage); // ✅ Store to registry
                RegistryHelper.Set("FontSize", _pendingFontSize.ToString());
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
            // Reset Account to default
            UsernameTextBox.Text = "ProgramUser";
            PasswordBox.Password = "Password1";

            // Reset theme to system defaults
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
            // Match primary
            foreach (var item in PrimaryColorComboBox.Items.OfType<LocalizedSwatch>())
            {
                if (item.PrimaryColor == theme.PrimaryMid.Color)
                {
                    PrimaryColorComboBox.SelectedItem = item;
                    break;
                }
            }
            
            // Match accent
            foreach (var item in AccentColorComboBox.Items.OfType<LocalizedSwatch>())
            {
                if ((item.AccentColor ?? item.PrimaryColor) == theme.SecondaryMid.Color)
                {
                    AccentColorComboBox.SelectedItem = item;
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
        private void SetPresetThemeSelectionFromTheme(ITheme theme)
        {
            // Use localized labels so selection works in all languages
            string L(string key) => (string)FindResource(key);
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
                presetMatch = L("Options_ThemePreset_SystemDefault");
            }
            else if (theme.GetBaseTheme() == BaseTheme.Light &&
                theme.PrimaryMid.Color == (Color)ColorConverter.ConvertFromString("#7B1FA2") &&
                theme.SecondaryMid.Color == Colors.LightGray)
                presetMatch = L("Options_ThemePreset_Light");

            else if (theme.GetBaseTheme() == BaseTheme.Dark &&
                     theme.PrimaryMid.Color == (Color)ColorConverter.ConvertFromString("#64B5F6") &&
                     theme.SecondaryMid.Color == Colors.Gray)
                presetMatch = L("Options_ThemePreset_Dark");

            else if (theme.GetBaseTheme() == BaseTheme.Light &&
                     theme.PrimaryMid.Color == Colors.White &&
                     theme.SecondaryMid.Color == Colors.Black)
                presetMatch = L("Options_ThemePreset_HighContrastLight");

            else if (theme.GetBaseTheme() == BaseTheme.Dark &&
                     theme.PrimaryMid.Color == Colors.Black &&
                     theme.SecondaryMid.Color == Colors.Yellow)
                presetMatch = L("Options_ThemePreset_HighContrastDark");

            else if (theme.GetBaseTheme() == BaseTheme.Dark &&
                     theme.PrimaryMid.Color == (Color)ColorConverter.ConvertFromString("#FF7000") &&
                     theme.SecondaryMid.Color == (Color)ColorConverter.ConvertFromString("#FFD000"))
                presetMatch = L("Options_ThemePreset_EliteDangerous");

            else
                presetMatch = L("Options_ThemePreset_Custom");

            foreach (ComboBoxItem item in PresetThemeComboBox.Items)
            {
                if ((string)item.Content == presetMatch)
                {
                    PresetThemeComboBox.SelectedItem = item;
                    break;
                }
            }

            bool isCustom = presetMatch == (string)FindResource("Options_ThemePreset_Custom");
            SetCustomPanelState(isCustom);
            AdjustWindowSize();
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