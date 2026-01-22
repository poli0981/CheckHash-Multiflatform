using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using CheckHash.Models;
using CheckHash.Services.ThemeEffects;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CheckHash.Services;

public partial class ThemeService : ObservableObject
{
    public static ThemeService Instance { get; } = new();

    [ObservableProperty] private AppThemeStyle _currentThemeStyle = AppThemeStyle.Fluent;
    [ObservableProperty] private AppThemeVariant _currentThemeVariant = AppThemeVariant.System;
    
    // Thêm tính năng chỉnh màu chữ
    [ObservableProperty] private bool _useCustomTextColor = false;
    [ObservableProperty] private Color _customTextColor = Colors.White;

    partial void OnCurrentThemeStyleChanged(AppThemeStyle value) => ApplyTheme();
    partial void OnCurrentThemeVariantChanged(AppThemeVariant value) => ApplyTheme();
    partial void OnUseCustomTextColorChanged(bool value) => ApplyTheme();
    partial void OnCustomTextColorChanged(Color value) => ApplyTheme(); // Sẽ update realtime khi kéo màu

    public void ApplyTheme()
    {
        var app = Application.Current;
        if (app == null) return;

        // 1. Apply Theme Variant (Light/Dark/System)
        var requestedVariant = CurrentThemeVariant switch
        {
            AppThemeVariant.Light => Avalonia.Styling.ThemeVariant.Light,
            AppThemeVariant.Dark => Avalonia.Styling.ThemeVariant.Dark,
            _ => Avalonia.Styling.ThemeVariant.Default
        };
        app.RequestedThemeVariant = requestedVariant;

        // 2. Get Palette
        var palette = ThemePalettes.GetPalette(CurrentThemeStyle, CurrentThemeVariant);

        // 3. Apply Palette to Application Resources
        foreach (var kvp in palette)
        {
            app.Resources[kvp.Key] = kvp.Value;
        }
        
        // 3.1 Apply Custom Text Color (Override)
        // Đã loại bỏ Custom Font Color theo yêu cầu
        
        // 4. Apply Window Effects (MicaCustom)
        if (CurrentThemeStyle == AppThemeStyle.MicaCustom)
        {
            LiquidGlassEffect.ApplyToMainWindow();
        }
        else
        {
            LiquidGlassEffect.DisableForMainWindow();
        }
    }

    // Helper để lọc danh sách Theme khả dụng dựa trên Variant hiện tại
    public List<AppThemeStyle> GetAvailableThemesForVariant(AppThemeVariant variant)
    {
        var all = Enum.GetValues(typeof(AppThemeStyle)).Cast<AppThemeStyle>().ToList();
        
        // Logic lọc:
        // - MicaCustom chỉ dành cho Dark (theo yêu cầu trước đó)
        // - Các theme khác có thể hỗ trợ cả hai hoặc tùy chỉnh
        
        bool isDark = variant == AppThemeVariant.Dark || (variant == AppThemeVariant.System && Application.Current?.PlatformSettings?.GetColorValues().ThemeVariant == PlatformThemeVariant.Dark);
        
        // Nếu muốn chính xác hơn với System, ta cần listen event thay đổi theme hệ thống, nhưng tạm thời dùng logic đơn giản
        // Giả sử System = Dark nếu không detect được
        if (variant == AppThemeVariant.System) isDark = true; 

        if (!isDark) // Light Mode
        {
            // Loại bỏ MicaCustom khỏi Light Mode
            return all.Where(t => t != AppThemeStyle.MicaCustom).ToList();
        }
        else // Dark Mode
        {
            return all;
        }
    }
}
