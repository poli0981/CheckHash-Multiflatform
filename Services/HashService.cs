using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace CheckHash.Services;

public enum HashType { MD5, SHA1, SHA256, SHA384, SHA512 }

public class HashService
{
    // Tính toán Hash bất đồng bộ
    public async Task<string> ComputeHashAsync(string filePath, HashType type, CancellationToken token)
    {
        using var stream = File.OpenRead(filePath);
        
        byte[] hashBytes = type switch
        {
            HashType.MD5 => await MD5.HashDataAsync(stream, token),
            HashType.SHA1 => await SHA1.HashDataAsync(stream, token),
            HashType.SHA256 => await SHA256.HashDataAsync(stream, token),
            HashType.SHA384 => await SHA384.HashDataAsync(stream, token),
            HashType.SHA512 => await SHA512.HashDataAsync(stream, token),
            _ => throw new System.NotImplementedException()
        };

        return Convert.ToHexString(hashBytes); // .NET hiện đại dùng cái này nhanh hơn BitConverter
    }
}