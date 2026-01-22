using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CheckHash.Services;
using Avalonia.Platform.Storage;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CheckHash.ViewModels;

public partial class CheckHashViewModel : ObservableObject
{
    public LocalizationService Localization => LocalizationService.Instance;
    private LocalizationService L => LocalizationService.Instance;
    private readonly HashService _hashService = new();

    // Danh sách file cần check
    public ObservableCollection<FileItem> Files { get; } = new();

    // Thuật toán CHUNG cho đợt check này (để tìm file sidecar tương ứng)
    public ObservableCollection<HashType> AlgorithmList { get; } = new(Enum.GetValues<HashType>());
    [ObservableProperty] private HashType _globalAlgorithm = HashType.SHA256;

    // Progress Bar
    [ObservableProperty] private double _progressValue;
    [ObservableProperty] private double _progressMax = 100;
    [ObservableProperty] private bool _isChecking;

    public string TotalFilesText => string.Format(L["Lbl_TotalFiles"], Files.Count);

    [RelayCommand]
    private void ClearList()
    {
        Files.Clear();
        ProgressValue = 0;
        OnPropertyChanged(nameof(TotalFilesText));
        VerifyAllCommand.NotifyCanExecuteChanged();
    }

    private bool CanVerify => Files.Count > 0 && !IsChecking;

    [RelayCommand(CanExecute = nameof(CanVerify))]
    private async Task VerifyAll()
    {
        IsChecking = true;
        ProgressMax = Files.Count;
        ProgressValue = 0;
        var queue = Files.ToList();
        int cancelled = 0;
        
        foreach (var file in queue)
        {
            if (!Files.Contains(file)) continue;
            
            await VerifyItemLogic(file);
            ProgressValue++;
            
            if (file.Status == L["Status_Cancelled"]) cancelled++;
        }
        
        IsChecking = false;
        
        // Đếm lại kết quả để hiện thông báo
        int match = Files.Count(f => f.IsMatch == true);
        int failCount = Files.Count(f => f.IsMatch == false);
        
        await MessageBoxHelper.ShowAsync(L["Msg_Result_Title"], 
            string.Format(L["Msg_CheckResult"], Files.Count, match, failCount, cancelled));
    }
    
    [RelayCommand]
    private async Task AddFilesToCheck(Avalonia.Controls.Window window)
    {
        try
        {
            var result = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions 
            { 
                AllowMultiple = true,
                Title = L["Dialog_SelectCheckFiles"]
            });

            foreach (var file in result)
            {
                var path = file.Path.LocalPath;
                var info = new FileInfo(path);
                // TrimStart('.') để loại bỏ dấu chấm, ví dụ ".sha256" -> "sha256"
                var ext = Path.GetExtension(path).TrimStart('.').ToUpper();
                
                // Thử parse extension thành HashType
                var isHashFile = Enum.TryParse<HashType>(ext, true, out var detectedAlgo);
                
                if (isHashFile)
                {
                    var dir = Path.GetDirectoryName(path);
                    if (dir == null) continue; // Bỏ qua nếu không xác định được thư mục

                    var sourcePath = Path.Combine(dir, Path.GetFileNameWithoutExtension(path));
                    
                    var item = new FileItem
                    {
                        FileName = Path.GetFileName(sourcePath), // Tên file gốc
                        FilePath = sourcePath,
                        ExpectedHash = await ReadHashFromFile(path), // Đọc luôn hash
                        Status = File.Exists(sourcePath) ? L["Status_ReadyFromHash"] : L["Status_MissingOriginal"],
                        SelectedAlgorithm = detectedAlgo // Tự set thuật toán theo đuôi file
                    };
                    
                    // Nếu file gốc tồn tại, lấy size
                    if (File.Exists(sourcePath))
                    {
                        var sourceInfo = new FileInfo(sourcePath);
                        item.FileSize = FileItem.FormatSize(sourceInfo.Length);
                    }
                    else
                    {
                        item.IsMatch = false; // Đánh dấu lỗi ngay
                    }
                    
                    Files.Add(item);
                }
                else
                {
                    if (!Files.Any(f => f.FilePath == path))
                    {
                        Files.Add(new FileItem
                        {
                            FileName = file.Name,
                            FilePath = path,
                            FileSize = FileItem.FormatSize(info.Length),
                            Status = L["Status_Waiting"],
                            ExpectedHash = "" // Để trống cho phép nhập tay
                        });
                    }
                }
            }
            
            OnPropertyChanged(nameof(TotalFilesText));
            VerifyAllCommand.NotifyCanExecuteChanged();
        }
        catch (Exception ex)
        {
            await MessageBoxHelper.ShowAsync(L["Msg_Error"], ex.Message);
        }
    }

    // Hàm phụ trợ đọc file hash (thường file hash có dạng: "HASH  filename" hoặc chỉ "HASH")
    private async Task<string> ReadHashFromFile(string path)
    {
        try 
        {
            var content = await File.ReadAllTextAsync(path);
            // Lấy chuỗi ký tự Hex đầu tiên tìm thấy
            var match = Regex.Match(content, @"[a-fA-F0-9]{32,128}");
            return match.Success ? match.Value : content.Trim();
        }
        catch { return ""; }
    }
    
    [RelayCommand]
    private void RemoveFile(FileItem item)
    {
        item.Cts?.Cancel();
        if (Files.Contains(item))
        {
            Files.Remove(item);
            OnPropertyChanged(nameof(TotalFilesText));
            VerifyAllCommand.NotifyCanExecuteChanged();
        }
    }
    
    [RelayCommand]
    private async Task VerifySingle(FileItem item)
    {
        if (item.IsProcessing)
        {
            item.Cts?.Cancel();
            return;
        }
        // Thay vì gọi ProcessItemAsync (chỉ tính hash), ta gọi VerifyItemLogic (tính + so sánh)
        await VerifyItemLogic(item);
    }
    
    
    private async Task ProcessItemAsync(FileItem item)
    {
        item.IsProcessing = true;
        item.Status = string.Format(L["Status_Processing"], item.SelectedAlgorithm);
        item.ProcessDuration = "";
        
        // Tạo Token hủy mới
        item.Cts = new CancellationTokenSource();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Truyền Token vào Service
            item.ResultHash = await _hashService.ComputeHashAsync(item.FilePath, item.SelectedAlgorithm, item.Cts.Token);
            item.Status = L["Status_Done"];
        }
        catch (OperationCanceledException)
        {
            item.Status = L["Status_Cancelled"];
        }
        catch (Exception)
        {
            item.Status = L["Msg_Error"];
        }
        finally
        {
            stopwatch.Stop();
            // Format thời gian: nếu < 1s hiện ms, > 1s hiện giây
            item.ProcessDuration = stopwatch.Elapsed.TotalSeconds < 1 
                ? $"{stopwatch.ElapsedMilliseconds}ms" 
                : $"{stopwatch.Elapsed.TotalSeconds:F2}s";

            item.IsProcessing = false;
            item.Cts?.Dispose();
            item.Cts = null;
        }
    }
    
    private async Task VerifyItemLogic(FileItem file)
    {
        file.IsProcessing = true;
        file.IsMatch = null;
        file.ProcessDuration = "";
        file.Cts = new CancellationTokenSource();
        var sw = Stopwatch.StartNew();
        try
        {
            // Bước 1: Check file gốc
            if (!File.Exists(file.FilePath))
            {
                file.Status = L["Status_LostOriginal"];
                file.IsMatch = false;
                return; // Thoát luôn
            }
            
            if (string.IsNullOrWhiteSpace(file.ExpectedHash))
            {
                // Chưa nhập tay -> Thử tìm file sidecar
                var globalExt = GlobalAlgorithm.ToString().ToLower();
                var sidecarPath = $"{file.FilePath}.{globalExt}";
                
                if (File.Exists(sidecarPath))
                {
                    file.ExpectedHash = await ReadHashFromFile(sidecarPath);
                }
            }

            if (string.IsNullOrWhiteSpace(file.ExpectedHash))
            {
                file.Status = L["Status_MissingHash"];
                file.IsMatch = false;
            }
            else
            {
                file.Status = L["Status_Waiting"]; // Tạm dùng Waiting hoặc Processing
                
                var algoToCheck = GlobalAlgorithm;

                var actualHash = await _hashService.ComputeHashAsync(file.FilePath, algoToCheck, file.Cts.Token);
                if (string.Equals(actualHash, file.ExpectedHash.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    file.Status = L["Status_Valid"];
                    file.IsMatch = true;
                }
                else
                {
                    file.Status = L["Status_Invalid"];
                    file.IsMatch = false;
                }
            }
        }
        catch (OperationCanceledException)
        {
            file.Status = L["Status_Cancelled"];
            file.IsMatch = null;
        }
        catch (Exception)
        {
            file.Status = L["Msg_Error"];
        }
        finally
        {
            sw.Stop();
            file.ProcessDuration = sw.Elapsed.TotalSeconds < 1 ? $"{sw.ElapsedMilliseconds}ms" : $"{sw.Elapsed.TotalSeconds:F2}s";
            file.IsProcessing = false;
            file.Cts?.Dispose();
            file.Cts = null;
        }
    }
    
    [RelayCommand]
    private async Task BrowseHashFile(FileItem item)
    {
        var window = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop ? desktop.MainWindow : null;
        if (window == null) return;

        // Mở hộp thoại chọn file
        var result = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = string.Format(L["Dialog_SelectHashFile"], item.FileName),
            AllowMultiple = false
        });

        if (result.Count > 0)
        {
            var hashFilePath = result[0].Path.LocalPath;
            var hashFileName = Path.GetFileName(hashFilePath);
            
            // Bỏ check tên file hash phải chứa tên file gốc, vì người dùng có thể chọn file hash bất kỳ
            
            if (!hashFileName.Contains(item.FileName, StringComparison.OrdinalIgnoreCase))
            {
                await MessageBoxHelper.ShowAsync(L["Msg_WrongHashFile"], 
                    string.Format(L["Msg_WrongHashFileContent"], item.FileName, hashFileName));
                return;
            }

            // 2. Nếu khớp -> Đọc và điền vào ô
            try 
            {
                item.ExpectedHash = await ReadHashFromFile(hashFilePath);
                item.Status = L["Status_HashFileLoaded"];
            }
            catch (Exception)
            {
                await MessageBoxHelper.ShowAsync(L["Msg_Error"], L["Msg_ReadHashError"]);
            }
        }
    }
}
