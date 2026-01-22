using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CheckHash.Services;
using Avalonia.Platform.Storage;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO.Compression;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using System.Diagnostics;

namespace CheckHash.ViewModels;

public partial class CreateHashViewModel : ObservableObject
{
    public LocalizationService Localization => LocalizationService.Instance;
    private LocalizationService L => LocalizationService.Instance;
    private readonly HashService _hashService = new();
    public string TotalFilesText => string.Format(L["Lbl_TotalFiles"], Files.Count);

    public ObservableCollection<FileItem> Files { get; } = new();

    public ObservableCollection<HashType> AlgorithmList { get; } = new(Enum.GetValues<HashType>());

    private bool CanComputeAll => Files.Count > 0;

    [RelayCommand]
    private async Task AddFiles(Avalonia.Controls.Window window)
    {
        var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        { 
            AllowMultiple = true,
            Title = L["Dialog_SelectFiles"]
        });

        foreach (var file in files)
        {
            if (!Files.Any(f => f.FilePath == file.Path.LocalPath))
            {
                // Lấy thông tin file size
                var info = new FileInfo(file.Path.LocalPath);

                Files.Add(new FileItem
                {
                    FileName = file.Name,
                    FilePath = file.Path.LocalPath,
                    FileSize = FileItem.FormatSize(info.Length), // <--- Hiển thị size
                    SelectedAlgorithm = HashType.SHA256
                });
            }
        }

        OnPropertyChanged(nameof(TotalFilesText));
        ComputeAllCommand.NotifyCanExecuteChanged();
    }


    [RelayCommand]
    private async Task CompressFiles(Avalonia.Controls.Window window)
    {
        if (Files.Count == 0) return;

        var fileSave = await window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = L["Dialog_SaveZip"],
            SuggestedFileName = $"Archive_{DateTime.Now:yyyyMMdd_HHmmss}.zip",
            DefaultExtension = "zip",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("ZIP Archive")
                {
                    Patterns = new[] { "*.zip" },
                    MimeTypes = new[] { "application/zip" }
                }
            }
        });

        if (fileSave != null)
        {
            try
            {
                var zipPath = fileSave.Path.LocalPath;

                await Task.Run(() =>
                {
                    if (File.Exists(zipPath)) File.Delete(zipPath);

                    using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
                    foreach (var item in Files)
                    {
                        // Chỉ nén file hash nếu đã tính toán xong
                        if (!string.IsNullOrEmpty(item.ResultHash))
                        {
                            var ext = item.SelectedAlgorithm.ToString().ToLower();
                            var hashFileName = $"{item.FileName}.{ext}";
                            
                            // Tạo entry trong zip với nội dung là hash
                            var entry = archive.CreateEntry(hashFileName);
                            using var entryStream = entry.Open();
                            using var writer = new StreamWriter(entryStream);
                            writer.Write(item.ResultHash);
                        }
                        else
                        {
                            // Nếu chưa tính hash, có thể tự động tính hoặc bỏ qua
                            // Ở đây ta bỏ qua để tránh logic phức tạp trong luồng nén
                        }
                    }
                });

                foreach (var f in Files) f.Status = L["Status_Compressed"];
            }
            catch (Exception)
            {
                // Xử lý lỗi
            }
        }
    }

    [RelayCommand]
    private void ClearList()
    {
        Files.Clear();
        OnPropertyChanged(nameof(TotalFilesText));
        ComputeAllCommand.NotifyCanExecuteChanged(); // List rỗng -> Disable nút
    }

    [RelayCommand(CanExecute = nameof(CanComputeAll))]
    private async Task ComputeAll()
    {
        int success = 0; int fail = 0; int cancelled = 0;

        // QUAN TRỌNG: Dùng .ToList() để tạo bản sao danh sách.
        // Vì nếu user bấm Xóa trong lúc vòng lặp đang chạy, Files.Remove sẽ gây lỗi crash vòng lặp.
        var queue = Files.ToList();

        foreach (var file in queue)
        {
            // Kiểm tra lại xem file còn trong list chính không (có thể đã bị xóa thủ công)
            if (!Files.Contains(file)) continue;

            await ProcessItemAsync(file);

            if (file.Status == L["Status_Done"]) success++;
            else if (file.Status == L["Status_Cancelled"]) cancelled++;
            else fail++;
        }
        
        Debug.WriteLine($"Cancelled: {cancelled}"); // Sử dụng biến để suppress warning

        await MessageBoxHelper.ShowAsync(L["Msg_Result_Title"], 
            string.Format(L["Msg_Result_Content"], success, fail));
    }
    [RelayCommand]
    private async Task SaveHashFile(FileItem item)
    {
        if (string.IsNullOrEmpty(item.ResultHash)) return;

        // Lưu đuôi file theo thuật toán của item đó
        var ext = item.SelectedAlgorithm.ToString().ToLower();

        var window =
            Application.Current?.ApplicationLifetime is
                IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;
        if (window == null) return;

        var file = await window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = L["Dialog_SaveHash"],
            SuggestedFileName = $"{item.FileName}.{ext}",
            DefaultExtension = ext
        });

        if (file != null)
        {
            await using var stream = await file.OpenWriteAsync();
            using var writer = new StreamWriter(stream);
            await writer.WriteAsync(item.ResultHash);
            item.Status = L["Status_Saved"];
        }
    }

    [RelayCommand]
    private async Task CopyToClipboard(string hash)
    {
        if (string.IsNullOrEmpty(hash)) return;

        // Lấy Clipboard từ ApplicationLifetime (Hỗ trợ cả Windows/Mac)
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime
            {
                MainWindow: { } window
            })
        {
            var clipboard = window.Clipboard;
            if (clipboard != null)
            {
                await clipboard.SetTextAsync(hash);
                // Có thể hiện thông báo nhỏ "Copied" nếu muốn (Optional)
            }
        }
    }
    
    private async Task ProcessItemAsync(FileItem item)
    {
        item.IsProcessing = true;
        item.Status = string.Format(L["Status_Processing"], item.SelectedAlgorithm);
        item.ProcessDuration = ""; // Reset thời gian
        
        // Tạo Token hủy mới
        item.Cts = new CancellationTokenSource();
        var stopwatch = Stopwatch.StartNew(); // Bắt đầu bấm giờ

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
        catch (Exception ex)
        {
            item.Status = string.Format(L["Status_Error"], ex.Message);
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
    
    
    [RelayCommand]
    private async Task ComputeSingle(FileItem item)
    {
        if (item.IsProcessing)
        {
            item.Cts?.Cancel();
            return;
        }
        await ProcessItemAsync(item);
    }

    // 1. Tính năng mới: Xóa 1 file khỏi list
    [RelayCommand]
    private void RemoveFile(FileItem item)
    {
        // Nếu đang chạy -> Hủy tác vụ
        item.Cts?.Cancel();

        if (Files.Contains(item))
        {
            Files.Remove(item);
            OnPropertyChanged(nameof(TotalFilesText));
            ComputeAllCommand.NotifyCanExecuteChanged();
        }
    }
}
