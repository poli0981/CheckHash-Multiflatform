using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace CheckHash.Services;

public enum HashType { MD5, SHA1, SHA256, SHA384, SHA512 }

public class HashService
{
    // 1MB Buffer size giúp tối ưu tốc độ đọc cho file lớn (>GB) trên SSD/HDD hiện đại
    private const int BufferSize = 1024 * 1024; 

    public async Task<string> ComputeHashAsync(string filePath, HashType type, CancellationToken token)
    {
        // Sử dụng FileStream với Buffer lớn và SequentialScan
        // FileOptions.SequentialScan: Tối ưu cho việc đọc tuần tự từ đầu đến cuối (giảm Cache thrashing của OS)
        // FileOptions.Asynchronous: Bắt buộc để dùng async I/O thực sự
        using var stream = new FileStream(
            filePath, 
            FileMode.Open, 
            FileAccess.Read, 
            FileShare.Read, 
            bufferSize: BufferSize, 
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);
        
        byte[] hashBytes = type switch
        {
            HashType.MD5 => await MD5.HashDataAsync(stream, token),
            HashType.SHA1 => await SHA1.HashDataAsync(stream, token),
            HashType.SHA256 => await SHA256.HashDataAsync(stream, token),
            HashType.SHA384 => await SHA384.HashDataAsync(stream, token),
            HashType.SHA512 => await SHA512.HashDataAsync(stream, token),
            _ => throw new NotImplementedException()
        };

        return Convert.ToHexString(hashBytes);
    }
}