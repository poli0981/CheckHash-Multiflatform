using CommunityToolkit.Mvvm.ComponentModel;

namespace CheckHash.Services;

public partial class PreferencesService : ObservableObject
{
    public static PreferencesService Instance { get; } = new();

    [ObservableProperty] private bool _isHashMaskingEnabled = false; // Mặc định tắt
}