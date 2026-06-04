using System.Windows;
using Forms = System.Windows.Forms;

namespace 小工具集合.Services.ScreenPointer;

public sealed class ScreenService
{
    public IReadOnlyList<DisplayInfo> GetDisplays()
    {
        return Forms.Screen.AllScreens
            .Select((screen, index) => new DisplayInfo(
                screen.DeviceName,
                $"{(screen.Primary ? "主屏幕" : "屏幕")} {index + 1}  {screen.Bounds.Width}x{screen.Bounds.Height}",
                new Rect(screen.Bounds.Left, screen.Bounds.Top, screen.Bounds.Width, screen.Bounds.Height),
                screen.Primary))
            .ToList();
    }

    public DisplayInfo GetTargetDisplay(string? deviceName)
    {
        IReadOnlyList<DisplayInfo> displays = GetDisplays();
        return displays.FirstOrDefault(display => display.DeviceName == deviceName)
            ?? displays.FirstOrDefault(display => display.IsPrimary)
            ?? displays[0];
    }
}
