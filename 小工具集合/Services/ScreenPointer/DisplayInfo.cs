// 文件作用：描述目标显示器的设备名、显示名称和物理边界。
using System.Windows;

namespace 小工具集合.Services.ScreenPointer;

public sealed record DisplayInfo(
    string DeviceName,
    string DisplayName,
    Rect Bounds,
    bool IsPrimary)
{
    public override string ToString()
    {
        return DisplayName;
    }
}
