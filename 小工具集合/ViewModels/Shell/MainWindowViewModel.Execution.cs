// 文件作用：承载主工具执行流程和运行时参数注入入口。
using System.Collections.Generic;
using 小工具集合.Models;
using 小工具集合.Services;

namespace 小工具集合.ViewModels;

public sealed partial class MainWindowViewModel
{
    public async Task ExecuteAsync()
    {
        // 将运行时参数摊平成字典，避免 ToolProcessor 依赖 WPF 控件或绑定对象。
        var parameters = Parameters.ToDictionary(parameter => parameter.Definition.Id, parameter => parameter.Value);
        parameters["networkEnabled"] = _preferences.IsNetworkEnabled ? "true" : "false";
        parameters["apiKey"] = _preferences.OcrSpaceApiKey;
        // 全局设置注入热点：普通工具只关心参数字典，不直接读 ViewModel。
        // 新增跨工具配置时优先在这里注入，再在处理器里做默认值兜底。
        InjectTranslationSettings(parameters);
        var request = new ToolRequest
        {
            ToolId = SelectedTool.Id,
            OperationId = SelectedOperation.Id,
            Input = HasInput ? InputText : string.Empty,
            Parameters = parameters
        };
        DateTimeOffset startedAt = DateTimeOffset.Now;

        IsBusy = true;
        IsPaused = false;
        StatusText = "正在处理...";

        try
        {
            ToolResult result = await _processor.ExecuteAsync(request, new ToolExecutionContext(() => IsPaused));
            IsSuccess = result.Success;
            OutputText = result.Success ? result.Output : string.Empty;
            StatusText = result.Message;
            ToolExecutionCompleted?.Invoke(this, result.Success);
            TrackTaskHistory(SelectedTool, SelectedOperation, request, result, startedAt);
        }
        finally
        {
            IsPaused = false;
            IsBusy = false;
            OnPropertyChanged(nameof(HasQueuedWork));
            OnPropertyChanged(nameof(HasActiveOrQueuedWork));
        }
    }
}
