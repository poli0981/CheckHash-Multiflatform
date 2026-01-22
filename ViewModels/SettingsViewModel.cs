using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CheckHash.Services;
using CheckHash.Models;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Media;

namespace CheckHash.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    public LocalizationService Localization => LocalizationService.Instance;
    public FontService Font => FontService.Instance;
    public PreferencesService Prefs => PreferencesService.Instance;
    public ThemeService Theme => ThemeService.Instance;

    // Danh sách Theme Style động, thay đổi khi Variant thay đổi
    [ObservableProperty] private List<AppThemeStyle> _filteredThemeStyles;

    public SettingsViewModel()
    {
        // Khởi tạo danh sách ban đầu
        UpdateFilteredThemes();
        
        // Lắng nghe thay đổi của ThemeVariant để cập nhật danh sách Style
        Theme.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(Theme.CurrentThemeVariant))
            {
                UpdateFilteredThemes();
            }
        };
    }

    private void UpdateFilteredThemes()
    {
        FilteredThemeStyles = Theme.GetAvailableThemesForVariant(Theme.CurrentThemeVariant);
        
        // Nếu Style hiện tại không còn nằm trong danh sách mới (ví dụ đang MicaCustom mà chuyển sang Light), reset về Fluent
        if (!FilteredThemeStyles.Contains(Theme.CurrentThemeStyle))
        {
            Theme.CurrentThemeStyle = AppThemeStyle.Fluent;
        }
    }
    
    [RelayCommand]
    private void ResetAppearance()
    {
        Font.ResetSettings();
        Theme.CurrentThemeStyle = AppThemeStyle.Fluent;
        Theme.CurrentThemeVariant = AppThemeVariant.System;
        ResetTextColor();
    }

    [RelayCommand]
    private void ResetTextColor()
    {
        Theme.UseCustomTextColor = false;
        Theme.CustomTextColor = Colors.White;
    }

    // Helper commands cho Toggle Button (Thay thế ComboBox Variant)
    [RelayCommand]
    private void SetVariantSystem() => Theme.CurrentThemeVariant = AppThemeVariant.System;
    
    [RelayCommand]
    private void SetVariantLight() => Theme.CurrentThemeVariant = AppThemeVariant.Light;
    
    [RelayCommand]
    private void SetVariantDark() => Theme.CurrentThemeVariant = AppThemeVariant.Dark;
}
