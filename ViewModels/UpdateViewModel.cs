using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CheckHash.Services;
using Avalonia.Controls.ApplicationLifetimes;
using System.Threading.Tasks;

namespace CheckHash.ViewModels;

public partial class UpdateViewModel : ObservableObject
{
    // Public để Binding từ View
    public LocalizationService Localization => LocalizationService.Instance;
    
    // Private shortcut để dùng trong code
    private LocalizationService L => LocalizationService.Instance;

    private readonly UpdateService _updateService = new();
    
    [ObservableProperty] private string _currentVersionText;
    [ObservableProperty] private string _statusMessage;
    [ObservableProperty] private bool _isChecking;
    [ObservableProperty] private bool _isUpdateAvailable;
    
    // Dropdown chọn kênh (0: Stable, 1: Dev)
    [ObservableProperty] private int _selectedChannelIndex; 

    public UpdateViewModel()
    {
        CurrentVersionText = string.Format(L["Lbl_CurrentVersion"], _updateService.CurrentVersion);
        StatusMessage = L["Lbl_Status_Ready"];
    }

    async partial void OnSelectedChannelIndexChanged(int value)
    {
        if (value == 1) // Chọn Dev
        {
            // Hiện Disclaimer
            var accepted = await ShowDisclaimer();
            if (!accepted)
            {
                // Nếu không đồng ý, quay về Stable
                SelectedChannelIndex = 0; 
                return;
            }
        }
        // Tự động check khi đổi kênh
        await CheckUpdate();
    }

    private async Task<bool> ShowDisclaimer()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Kiểm tra MainWindow null
            if (desktop.MainWindow == null) return false;

            var dialog = new CheckHash.Views.DisclaimerWindow();
            await dialog.ShowDialog(desktop.MainWindow);
            return dialog.IsAccepted;
        }
        return false;
    }

    [RelayCommand]
    private async Task CheckUpdate()
    {
        IsChecking = true;
        StatusMessage = L["Status_Checking"];
        IsUpdateAvailable = false;

        try
        {
            bool isDev = SelectedChannelIndex == 1;
            var updateInfo = await _updateService.CheckForUpdatesAsync(isDev);

            if (updateInfo != null)
            {
                IsUpdateAvailable = true;
                StatusMessage = L["Status_NewVersion"];
                
                var version = updateInfo.TargetFullRelease.Version.ToString();
                
                // Fetch Release Notes từ GitHub
                var notes = await _updateService.GetReleaseNotesAsync(version);
                
                await MessageBoxHelper.ShowAsync(L["Msg_UpdateTitle"], 
                    string.Format(L["Msg_UpdateContent"], version, notes));
                
                await InstallUpdate(updateInfo);
            }
            else
            {
                StatusMessage = L["Status_Latest"];
                await MessageBoxHelper.ShowAsync(L["Msg_Result_Title"], L["Msg_NoUpdate"]);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = string.Format(L["Status_CheckError"], ex.Message);
        }
        finally
        {
            IsChecking = false;
        }
    }

    private async Task InstallUpdate(Velopack.UpdateInfo info)
    {
        StatusMessage = L["Status_Installing"];
        IsChecking = true; // Block UI
        try
        {
            await _updateService.DownloadAndInstallAsync(info);
            // App sẽ tự restart sau dòng này
        }
        catch (Exception ex)
        {
            StatusMessage = string.Format(L["Status_InstallError"], ex.Message);
            IsChecking = false;
        }
    }
}