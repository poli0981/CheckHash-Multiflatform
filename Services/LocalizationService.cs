using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Text.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;

namespace CheckHash.Services;

public partial class LocalizationService : ObservableObject
{
    // Singleton để truy cập từ mọi nơi
    public static LocalizationService Instance { get; } = new();

    private Dictionary<string, string> _currentResources = new();

    // Danh sách ngôn ngữ hỗ trợ
    public List<LanguageItem> AvailableLanguages { get; } = new()
    {
        new("Auto (System)", "auto"), // Thêm tùy chọn Auto
        new("English", "en-US"),
        new("Tiếng Việt", "vi-VN")
    };

    [ObservableProperty] private LanguageItem _selectedLanguage;

    // Indexer: Chìa khóa vàng để Binding trong XAML. Ví dụ: {Binding L[Menu_Create]}
    public string this[string key]
    {
        get
        {
            if (_currentResources.TryGetValue(key, out var value))
                return value;
            return $"[{key}]"; // Trả về key nếu không tìm thấy (để dễ debug)
        }
    }

    public LocalizationService()
    {
        // Mặc định chọn Auto
        SelectedLanguage = AvailableLanguages[0]; 
        
        // Load ngôn ngữ dựa trên lựa chọn (nếu Auto thì detect)
        var codeToLoad = SelectedLanguage.Code == "auto" ? DetectSystemLanguageCode() : SelectedLanguage.Code;
        LoadLanguage(codeToLoad);
    }

    partial void OnSelectedLanguageChanged(LanguageItem value)
    {
        var codeToLoad = value.Code == "auto" ? DetectSystemLanguageCode() : value.Code;
        LoadLanguage(codeToLoad);
        
        // Tự động cập nhật font
        FontService.Instance.SetFontForLanguage(codeToLoad);
    }

    private string DetectSystemLanguageCode()
    {
        try 
        {
            // 1. Lấy Culture của hệ điều hành
            var systemCulture = CultureInfo.CurrentUICulture;
            
            // 2. Tìm trong danh sách app xem có cái nào khớp chính xác không (VD: máy là vi-VN -> khớp vi-VN)
            // Bỏ qua item "auto" khi tìm kiếm
            var exactMatch = AvailableLanguages
                .Skip(1) 
                .FirstOrDefault(x => x.Code.Equals(systemCulture.Name, StringComparison.OrdinalIgnoreCase));

            if (exactMatch != null) return exactMatch.Code;

            // 3. Nếu không khớp chính xác, tìm khớp theo mã 2 ký tự (VD: máy là en-GB -> khớp en-US)
            var twoLetterMatch = AvailableLanguages
                .Skip(1)
                .FirstOrDefault(x => x.Code.StartsWith(systemCulture.TwoLetterISOLanguageName));

            if (twoLetterMatch != null) return twoLetterMatch.Code;
        }
        catch 
        {
            // Ignore errors
        }

        // 4. Fallback
        return "en-US";
    }

    private void LoadLanguage(string languageCode)
    {
        // Sửa tên assembly từ HashChecker thành CheckHash
        var uri = new Uri($"avares://CheckHash/Assets/I18N/{languageCode}.json");
        
        if (AssetLoader.Exists(uri))
        {
            using var stream = AssetLoader.Open(uri);
            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();
            
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (dict != null)
            {
                _currentResources = dict;
                
                // Thông báo cập nhật cho TOÀN BỘ property binding qua Indexer
                OnPropertyChanged("Item[]"); 
            }
        }
    }
}

// Class phụ để hiển thị trong ComboBox
public record LanguageItem(string Name, string Code)
{
    public override string ToString() => Name;
}