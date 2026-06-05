// 文件作用：承载工具选择、清空和动态参数运行时值重建逻辑。
using System.Collections.Generic;
using 小工具集合.Models;
using 小工具集合.Services;

namespace 小工具集合.ViewModels;

public sealed partial class MainWindowViewModel
{
    public void Clear()
    {
        InputText = string.Empty;
        OutputText = string.Empty;
        RebuildParameters();

        StatusText = "已清空";
        IsSuccess = true;
        OnPropertyChanged(nameof(HasQueuedWork));
        OnPropertyChanged(nameof(HasActiveOrQueuedWork));
    }

    public void SelectToolItem(ToolBrowserItem item)
    {
        if (item.Group != SelectedGroup)
        {
            SelectedGroup = item.Group;
        }

        SelectedTool = item.Tool;
        OnPropertyChanged(nameof(VisibleToolItems));
    }

    private void RebuildParameters()
    {
        // 切换工具时重建运行时参数集合，UI 层会据此重新生成对应控件。
        Parameters.Clear();
        foreach (ToolParameterDefinition definition in SelectedTool.Parameters)
        {
            // 高频编辑点：全局默认输出目录、翻译默认语言/引擎在这里覆盖 catalog 默认值。
            // 其他工具参数仍以 ToolCatalog 的 DefaultValue 为准。
            string value = definition.Id == "outputDirectory" && !string.IsNullOrWhiteSpace(_preferences.DefaultOutputDirectory)
                ? _preferences.DefaultOutputDirectory
                : definition.Id == "provider"
                    ? _translationSettings.DefaultProvider
                : definition.Id == "sourceLanguage"
                    ? _translationSettings.DefaultSourceLanguage
                : definition.Id == "targetLanguage"
                    ? _translationSettings.DefaultTargetLanguage
                : definition.DefaultValue;
            Parameters.Add(new ToolParameterValue
            {
                Definition = definition,
                Value = value
            });
        }
    }

}
