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
