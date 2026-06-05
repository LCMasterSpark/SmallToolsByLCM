// 文件作用：承载批处理任务历史记录与失败项重试逻辑。
using System.Collections.Generic;
using 小工具集合.Models;
using 小工具集合.Services;

namespace 小工具集合.ViewModels;

public sealed partial class MainWindowViewModel
{
    private void TrackTaskHistory(ToolDefinition tool, ToolOperation operation, ToolRequest request, ToolResult result, DateTimeOffset startedAt)
    {
        if (!IsPausableToolId(tool.Id) || !_preferences.SaveTaskHistory)
        {
            return;
        }

        TaskHistoryRecord record = TaskHistoryService.CreateRecord(tool, operation, request, result, startedAt);
        _taskHistoryService.Add(record, _preferences.TaskHistoryLimit);
        TaskHistory.Insert(0, record);
        while (TaskHistory.Count > _preferences.TaskHistoryLimit)
        {
            TaskHistory.RemoveAt(TaskHistory.Count - 1);
        }
    }

    public async Task RetryTaskAsync(TaskHistoryRecord record)
    {
        ToolDefinition tool = ToolCatalog.FindTool(record.ToolId);
        ToolOperation operation = tool.Operations.FirstOrDefault(item => item.Id == record.OperationId) ?? tool.Operations[0];
        var request = new ToolRequest
        {
            ToolId = tool.Id,
            OperationId = operation.Id,
            Input = string.Empty,
            Parameters = TaskHistoryService.BuildRetryParameters(record)
        };
        DateTimeOffset startedAt = DateTimeOffset.Now;

        IsBusy = true;
        IsPaused = false;
        StatusText = $"正在重试：{tool.Name}";
        try
        {
            ToolResult result = await _processor.ExecuteAsync(request, new ToolExecutionContext(() => IsPaused));
            IsSuccess = result.Success;
            OutputText = result.Success ? result.Output : string.Empty;
            StatusText = result.Message;
            ToolExecutionCompleted?.Invoke(this, result.Success);
            TrackTaskHistory(tool, operation, request, result, startedAt);
        }
        finally
        {
            IsPaused = false;
            IsBusy = false;
        }
    }

}
