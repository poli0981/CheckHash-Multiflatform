using Avalonia;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CheckHash.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    // Navigation
    [ObservableProperty] private object _currentPage;
    [ObservableProperty] private bool _isPaneOpen = true; // Sidebar mở/đóng
    
    // Các trang con (Cache lại để không phải new nhiều lần)
    public CreateHashViewModel CreateHashVM { get; } = new();
    public CheckHashViewModel CheckHashVM { get; } = new();

    public MainWindowViewModel()
    {
        // Trang mặc định
        CurrentPage = CreateHashVM;
    }

    [RelayCommand]
    private void TriggerPane() => IsPaneOpen = !IsPaneOpen;

    [RelayCommand]
    private void NavigateToCreate() => CurrentPage = CreateHashVM;

    [RelayCommand]
    private void NavigateToCheck() => CurrentPage = CheckHashVM;

    // Toggle Dark/Light Mode
    [RelayCommand]
    private void ToggleTheme()
    {
        var app = Application.Current;
        if (app is not null)
        {
            var theme = app.RequestedThemeVariant;
            app.RequestedThemeVariant = (theme == ThemeVariant.Dark) ? ThemeVariant.Light : ThemeVariant.Dark;
        }
    }
}