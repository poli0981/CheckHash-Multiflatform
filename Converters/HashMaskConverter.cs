using System;
using System.Globalization;
using System.Collections.Generic;
using Avalonia.Data.Converters;

namespace CheckHash.Converters;

public class HashMaskConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        // values[0]: Hash string gốc
        // values[1]: IsHashMaskingEnabled (Global Setting)
        // values[2]: IsRevealed (Local Item State)

        if (values.Count < 3) return "";

        var hash = values[0] as string ?? "";
        var isMaskingEnabled = values[1] as bool? ?? false;
        var isRevealed = values[2] as bool? ?? false;

        // Nếu tham số là "MaskOnly" -> Chỉ trả về chuỗi đã che (dùng cho TextBlock đè lên)
        // Logic này dùng cho CheckHashView khi muốn hiển thị TextBlock che đè lên TextBox
        if (parameter as string == "MaskOnly")
        {
             if (string.IsNullOrEmpty(hash)) return "";
             if (hash.Length <= 8) return new string('*', hash.Length);
             return $"{hash[..4]}{new string('*', hash.Length - 8)}{hash[^4..]}";
        }

        if (string.IsNullOrEmpty(hash)) return "";

        // Nếu không bật tính năng che -> hiện nguyên
        if (!isMaskingEnabled) return hash;

        // Nếu bật che, nhưng user bấm hiện -> hiện nguyên
        if (isRevealed) return hash;

        // Còn lại -> Che (hiện 4 ký tự đầu, 4 ký tự cuối, giữa là sao)
        if (hash.Length <= 8) return new string('*', hash.Length);
        
        return $"{hash[..4]}{new string('*', hash.Length - 8)}{hash[^4..]}";
    }
}