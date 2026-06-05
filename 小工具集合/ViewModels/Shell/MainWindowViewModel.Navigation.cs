// 文件作用：承载收藏、最近使用和工具搜索匹配逻辑。
using System.Collections.Generic;
using 小工具集合.Models;
using 小工具集合.Services;

namespace 小工具集合.ViewModels;

public sealed partial class MainWindowViewModel
{
    private void ToggleFavorite()
    {
        if (_preferences.FavoriteToolIds.Contains(SelectedTool.Id, StringComparer.Ordinal))
        {
            _preferences.FavoriteToolIds.RemoveAll(id => id == SelectedTool.Id);
        }
        else
        {
            _preferences.FavoriteToolIds.Insert(0, SelectedTool.Id);
        }

        SavePreferences();
        OnPropertyChanged(nameof(FavoriteToolItems));
        OnPropertyChanged(nameof(HasFavoriteTools));
        OnPropertyChanged(nameof(IsSelectedToolFavorite));
    }

    private void AddRecentTool(string toolId)
    {
        _preferences.RecentToolIds.RemoveAll(id => id == toolId);
        _preferences.RecentToolIds.Insert(0, toolId);
        if (_preferences.RecentToolIds.Count > 8)
        {
            _preferences.RecentToolIds.RemoveRange(8, _preferences.RecentToolIds.Count - 8);
        }
    }

    private void ClearRecentTools()
    {
        _preferences.RecentToolIds.Clear();
        SavePreferences();
        OnPropertyChanged(nameof(RecentToolItems));
        OnPropertyChanged(nameof(HasRecentTools));
        StatusText = "最近使用已清空。";
    }

    private void ClearTaskHistory()
    {
        _taskHistoryService.Clear();
        TaskHistory.Clear();
        StatusText = "任务历史已清空。";
    }

    private IReadOnlyList<ToolBrowserItem> BuildPinnedToolItems(IEnumerable<string> toolIds)
    {
        return toolIds
            .Select(ToolCatalog.FindTool)
            .Where(tool => tool is not null)
            .Select(tool => new ToolBrowserItem
            {
                Tool = tool,
                Group = Groups.First(group => group.Tools.Contains(tool))
            })
            .ToList();
    }

}
