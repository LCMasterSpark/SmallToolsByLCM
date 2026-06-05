// 文件作用：按互动工具 key 创建对应 UserControl，让主窗口不直接依赖每个互动工具类型。
using System.Windows.Controls;

namespace 小工具集合.Views.Shell;

public interface IInteractiveToolViewFactory
{
    UserControl? Create(string interactiveViewKey);
}
