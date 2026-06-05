// 文件作用：集中维护互动工具 key 到控件实例的映射，避免 Shell 窗口出现工具级 switch。
using System.Windows.Controls;
using 小工具集合.Views.FunLab;
using 小工具集合.Views.Generation;
using 小工具集合.Views.Translation;

namespace 小工具集合.Views.Shell;

public sealed class InteractiveToolViewFactory : IInteractiveToolViewFactory
{
    public UserControl? Create(string interactiveViewKey)
    {
        return interactiveViewKey switch
        {
            "powerChecker" => new PowerCheckerControl(),
            "timePointer" => new TimePointerControl(),
            "minesweeper" => new MinesweeperControl(),
            "qrCode" => new QrCodeControl(),
            "screenPointer" => new ScreenPointerControl(),
            "liveTranslator" => new LiveTranslatorControl(),
            _ => null
        };
    }
}
