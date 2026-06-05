// 文件作用：为动态参数控件提供文件/目录选择桥接，避免控件生成器直接依赖主窗口。
namespace 小工具集合.Views.Controls;

public interface IToolParameterDialogService
{
    string? PickInputFile();
    string? PickOutputFile();
    string[] PickMultipleFiles();
    string? PickFolder();
}
