using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CheckHash.Services;
using CheckHash.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Velopack;

namespace CheckHash.ViewModels;

public partial class UpdateViewModel : ObservableObject
{
    private readonly UpdateService _updateService = UpdateService.Instance;

    [ObservableProperty] private string _currentVersionText;
    [ObservableProperty] private bool _isChecking;
    [ObservableProperty] private bool _isUpdateAvailable;
    [ObservableProperty] private LocalizationProxy _localization = new(LocalizationService.Instance);

    [ObservableProperty] private int _selectedChannelIndex;
    [ObservableProperty] private string _statusMessage;
    [ObservableProperty] private int _downloadProgress;
    [ObservableProperty] private bool _isDownloading;

    public UpdateViewModel()
    {
        CurrentVersionText = string.Format(L["Lbl_CurrentVersion"], _updateService.CurrentVersion);
        StatusMessage = L["Lbl_Status_Ready"];

        LocalizationService.Instance.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == "Item[]")
            {
                CurrentVersionText = string.Format(L["Lbl_CurrentVersion"], _updateService.CurrentVersion);
                Localization = new LocalizationProxy(LocalizationService.Instance);
            }
        };
    }

    private LocalizationService L => LocalizationService.Instance;
    private LoggerService Logger => LoggerService.Instance;

    async partial void OnSelectedChannelIndexChanged(int value)
    {
        if (value == 1) // Developer Channel
        {
            var accepted = await ShowDisclaimer();
            if (!accepted)
            {
                SelectedChannelIndex = 0;
                return;
            }

            Logger.Log("Switched to Developer Channel.", LogLevel.Warning);
        }
        else
        {
            Logger.Log("Switched to Stable Channel.");
        }

        await CheckUpdate();
    }

    private async Task<bool> ShowDisclaimer()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (desktop.MainWindow == null) return false;

            var dialog = new DisclaimerWindow();
            await dialog.ShowDialog(desktop.MainWindow);
            return dialog.IsAccepted;
        }

        return false;
    }

    [RelayCommand]
    private async Task CheckUpdate()
    {
        Logger.Log("Checking for updates...");
        IsChecking = true;
        StatusMessage = L["Status_Checking"];
        IsUpdateAvailable = false;

        try
        {
            var isDev = SelectedChannelIndex == 1;
            var updateInfo = await _updateService.CheckForUpdatesAsync(isDev);

            if (updateInfo != null)
            {
                IsUpdateAvailable = true;
                StatusMessage = L["Status_NewVersion"];

                var version = updateInfo.TargetFullRelease.Version.ToString();
                Logger.Log($"New version found: {version}", LogLevel.Success);

                var notes = await _updateService.GetReleaseNotesAsync(version);

                var result = await MessageBoxHelper.ShowConfirmationAsync(L["Msg_UpdateTitle"],
                    string.Format(L["Msg_UpdateContent"], version, notes), L["Btn_Install"], L["Btn_No"]);

                if (result)
                {
                    await InstallUpdate(updateInfo);
                }
            }
            else
            {
                StatusMessage = L["Status_Latest"];
                Logger.Log("Application is up to date.");
                await MessageBoxHelper.ShowAsync(L["Msg_Result_Title"], L["Msg_NoUpdate"]);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = string.Format(L["Status_CheckError"], ex.Message);
            Logger.Log($"Update check failed: {ex.Message}", LogLevel.Error);
        }
        finally
        {
            if (!IsDownloading)
            {
                IsChecking = false;
            }
        }
    }

    private async Task InstallUpdate(UpdateInfo info)
    {
        StatusMessage = L["Status_Installing"];
        IsChecking = true;
        IsDownloading = true;
        DownloadProgress = 0;
        Logger.Log("Downloading update...");
        try
        {
            await _updateService.DownloadUpdatesAsync(info, progress =>
            {
                DownloadProgress = progress;
            });

            StatusMessage = "Update downloaded. Restarting...";
            _updateService.ApplyUpdatesAndRestart(info);
        }
        catch (Exception ex)
        {
            StatusMessage = string.Format(L["Status_InstallError"], ex.Message);
            IsChecking = false;
            IsDownloading = false;
            Logger.Log($"Install failed: {ex.Message}", LogLevel.Error);
        }
    }
}