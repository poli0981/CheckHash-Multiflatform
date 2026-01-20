using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CheckHash.Services;
using Avalonia.Platform.Storage;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CheckHash.ViewModels;

public partial class CheckHashViewModel : ObservableObject
{
    private readonly HashService _hashService = new();

    // Danh sách file cần check
    public ObservableCollection<FileItem> Files { get; } = new();

    // Thuật toán CHUNG cho đợt check này (để tìm file sidecar tương ứng)
    public ObservableCollection<HashType> AlgorithmList { get; } = new(Enum.GetValues<HashType>());
    [ObservableProperty] private HashType _globalAlgorithm = HashType.SHA256;

    // Progress Bar
    [ObservableProperty] private double _progressValue = 0;
    [ObservableProperty] private double _progressMax = 100;
    [ObservableProperty] private bool _isChecking;

    public string TotalFilesText => $"Tổng số file: {Files.Count}";

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

        foreach (var file in Files)
        {
            await VerifyItemLogic(file);
            ProgressValue++;
        }
        
        IsChecking = false;
        
        // Đếm lại kết quả để hiện thông báo
        int match = Files.Count(f => f.IsMatch == true);
        int fail = Files.Count(f => f.IsMatch == false);
        
        await MessageBoxHelper.ShowAsync("Kết quả", 
            $"Đã kiểm tra {Files.Count} file.\n✅ Khớp: {match}\n❌ Lệch/Lỗi: {fail}");
    }
    
    [RelayCommand]
    private async Task AddFilesToCheck(Avalonia.Controls.Window window)
    {
        var result = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions 
        { 
            AllowMultiple = true,
            Title = "Chọn file (Gốc hoặc File .hash)" 
        });

        foreach (var file in result)
        {
            var path = file.Path.LocalPath;
            
            // Logic Smart-Detect: Xác định đây là File Gốc hay File Hash?
            // Kiểm tra đuôi file xem có trùng với tên thuật toán không (vd: .md5, .sha256)
            var ext = Path.GetExtension(path).TrimStart('.').ToUpper();
            var isHashFile = Enum.TryParse<HashType>(ext, true, out var detectedAlgo);

            if (isHashFile)
            {
                // TRƯỜNG HỢP 1: User chọn file .sha256, .md5...
                // -> File gốc là tên file bỏ đi phần đuôi
                var sourcePath = Path.Combine(Path.GetDirectoryName(path)!, Path.GetFileNameWithoutExtension(path));
                
                var item = new FileItem
                {
                    FileName = Path.GetFileName(sourcePath), // Tên file gốc
                    FilePath = sourcePath,
                    ExpectedHash = await ReadHashFromFile(path), // Đọc luôn hash
                    Status = File.Exists(sourcePath) ? "Sẵn sàng (Từ file .hash)" : "Thiếu file gốc!",
                    SelectedAlgorithm = detectedAlgo // Tự set thuật toán theo đuôi file
                };
                
                // Nếu thiếu file gốc, đánh dấu đỏ
                if (!File.Exists(sourcePath)) item.IsMatch = false; 
                
                Files.Add(item);
            }
            else
            {
                // TRƯỜNG HỢP 2: User chọn File Gốc (như cũ)
                // -> Để trống ExpectedHash để user tự paste hoặc app tự tìm sau
                if (!Files.Any(f => f.FilePath == path))
                {
                    Files.Add(new FileItem
                    {
                        FileName = file.Name,
                        FilePath = path,
                        Status = "Chờ hash/nhập mã...",
                        ExpectedHash = "" // Để trống cho phép nhập tay
                    });
                }
            }
        }
        
        OnPropertyChanged(nameof(TotalFilesText));
        VerifyAllCommand.NotifyCanExecuteChanged();
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
        await VerifyItemLogic(item);
    }
    
    private async Task VerifyItemLogic(FileItem file)
    {
        file.IsProcessing = true;
        file.IsMatch = null;

        try
        {
            // Bước 1: Check file gốc
            if (!File.Exists(file.FilePath))
            {
                file.Status = "Mất file gốc!";
                file.IsMatch = false;
                return; // Thoát luôn
            }

            // Bước 2: Xác định Hash Mong Đợi (FIX BUG TẠI ĐÂY)
            // Logic cũ: Cứ tìm file sidecar, không thấy là báo lỗi -> Sai.
            // Logic mới: Kiểm tra xem đã có ExpectedHash (nhập tay) chưa? Nếu chưa mới đi tìm file.
            
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

            // Bước 3: Kiểm tra lại lần cuối
            if (string.IsNullOrWhiteSpace(file.ExpectedHash))
            {
                file.Status = "Thiếu mã Hash (Nhập hoặc cần file .hash)";
                file.IsMatch = false;
            }
            else
            {
                // Bước 4: Tính toán & So sánh
                file.Status = "Đang tính...";
                
                // Nếu item chưa có thuật toán riêng, dùng Global
                var algoToCheck = file.SelectedAlgorithm; 
                // Lưu ý: Nếu logic add file của bạn chưa set SelectedAlgorithm cho file gốc, 
                // hãy dùng GlobalAlgorithm tại đây.
                if(algoToCheck == HashType.SHA256 && GlobalAlgorithm != HashType.SHA256) 
                     algoToCheck = GlobalAlgorithm;

                var actualHash = await _hashService.ComputeHashAsync(file.FilePath, algoToCheck, CancellationToken.None);

                if (string.Equals(actualHash, file.ExpectedHash.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    file.Status = "HỢP LỆ";
                    file.IsMatch = true;
                }
                else
                {
                    file.Status = "SAI LỆCH!";
                    file.IsMatch = false;
                }
            }
        }
        catch
        {
            file.Status = "Lỗi hệ thống";
            file.IsMatch = false;
        }
        finally
        {
            file.IsProcessing = false;
        }
    }
}