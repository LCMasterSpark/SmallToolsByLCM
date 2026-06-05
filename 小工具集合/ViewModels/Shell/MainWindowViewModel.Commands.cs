// 文件作用：承载更新检查、暂停和命令状态刷新逻辑。
using System.Collections.Generic;
using 小工具集合.Models;
using 小工具集合.Services;

namespace 小工具集合.ViewModels;

public sealed partial class MainWindowViewModel
{
    private async Task CheckUpdateAsync()
    {
        StatusText = "正在检查 GitHub Release...";
        string currentVersion = typeof(MainWindowViewModel).Assembly.GetName().Version?.ToString(3) ?? "3.1.0";
        UpdateCheckResult result = await _updateCheckService.CheckLatestAsync(currentVersion);
        UpdateStatusText = result.Success
            ? $"{result.Message}{Environment.NewLine}当前版本：{result.CurrentVersion}{Environment.NewLine}最新版本：{result.LatestVersion}{Environment.NewLine}Release：{result.ReleaseUrl}{Environment.NewLine}EXE：{result.DownloadUrl}"
            : result.Message;
        StatusText = result.Message;
    }

    private void TogglePause()
    {
        if (!IsBusy || !IsPausableTool)
        {
            return;
        }

        IsPaused = !IsPaused;
        StatusText = IsPaused ? "已暂停，当前文件处理完成后会停在下一个文件前。" : "继续处理...";
    }

    private static bool ToolMatchesSearch(ToolGroup group, ToolDefinition tool, string query)
    {
        return string.IsNullOrWhiteSpace(query)
            || group.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
            || tool.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
            || tool.Description.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPausableToolId(string toolId)
    {
        return ToolProcessor.IsPausableTool(toolId);
    }

    private void RaiseCommandStates()
    {
        _executeCommand.RaiseCanExecuteChanged();
        _retryTaskCommand.RaiseCanExecuteChanged();
        if (PauseCommand is RelayCommand pauseCommand)
        {
            pauseCommand.RaiseCanExecuteChanged();
        }
    }
}
