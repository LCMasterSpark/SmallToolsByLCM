// 文件作用：使用 Windows 标准对话框实现动态参数区的文件和目录选择。
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace 小工具集合.Views.Controls;

public sealed class WindowsToolParameterDialogService : IToolParameterDialogService
{
    public string? PickInputFile()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "所有文件 (*.*)|*.*",
            CheckFileExists = true
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickOutputFile()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "所有文件 (*.*)|*.*",
            AddExtension = true
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string[] PickMultipleFiles()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "常用文件|*.mp4;*.jpg;*.jpeg;*.png;*.webp;*.raw;*.enc|所有文件 (*.*)|*.*",
            Multiselect = true,
            CheckFileExists = true
        };

        return dialog.ShowDialog() == true ? dialog.FileNames : [];
    }

    public string? PickFolder()
    {
        using var dialog = new Forms.FolderBrowserDialog();
        return dialog.ShowDialog() == Forms.DialogResult.OK ? dialog.SelectedPath : null;
    }
}
