using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Styling;
using System;

namespace CheckHash.Services.Animation;

public static class LiquidGlassAnimation
{
    // Class này chứa các định nghĩa Animation nếu cần dùng code-behind hoặc tham khảo.
    // Tuy nhiên, trong Avalonia, Animation thường được định nghĩa tốt nhất trong XAML (Styles).
    
    // Ví dụ về cấu hình Transition chuẩn cho Liquid Glass:
    // - Duration: 0.2s - 0.3s
    // - Easing: CubicEaseOut hoặc CircularEaseOut (mượt mà, tự nhiên)
    
    public static TimeSpan DefaultDuration = TimeSpan.FromSeconds(0.25);
    public static Easing DefaultEasing = new CubicEaseOut();
}
