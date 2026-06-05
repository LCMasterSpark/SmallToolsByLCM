// 文件作用：维护批处理任务历史、失败项重试数据和 app-state.json 中的 tasks section。
using 小工具集合.Models;

namespace 小工具集合.Services;

public sealed class TaskHistoryService
{
    private readonly AppStateService appStateService = new();

    public IReadOnlyList<TaskHistoryRecord> Load()
    {
        return appStateService.LoadTasks().Records
            .OrderByDescending(record => record.StartedAt)
            .ToList();
    }

    public void Add(TaskHistoryRecord record, int historyLimit)
    {
        TaskHistoryState state = appStateService.LoadTasks();
        state.Records.Insert(0, record);
        int limit = Math.Clamp(historyLimit, 1, 500);
        if (state.Records.Count > limit)
        {
            state.Records = state.Records.Take(limit).ToList();
        }

        appStateService.SaveTasks(state);
    }

    public void Clear()
    {
        appStateService.SaveTasks(new TaskHistoryState());
    }

    public static TaskHistoryRecord CreateRecord(
        ToolDefinition tool,
        ToolOperation operation,
        ToolRequest request,
        ToolResult result,
        DateTimeOffset startedAt)
    {
        List<string> files = ExtractInputFiles(request.Parameters);
        string outputDirectory = request.Parameters.TryGetValue("outputDirectory", out string? output)
            ? output
            : string.Empty;
        var failedFiles = result.Success ? [] : files.ToList();

        return new TaskHistoryRecord
        {
            ToolId = tool.Id,
            OperationId = operation.Id,
            ToolName = tool.Name,
            StartedAt = startedAt,
            FinishedAt = DateTimeOffset.Now,
            OutputDirectory = outputDirectory,
            InputFiles = files,
            FailedFiles = failedFiles,
            Parameters = SanitizeParameters(request.Parameters),
            SuccessCount = result.Success ? files.Count : 0,
            FailureCount = result.Success ? 0 : files.Count,
            ErrorMessage = result.Success ? string.Empty : result.Message
        };
    }

    public static Dictionary<string, string> BuildRetryParameters(TaskHistoryRecord record)
    {
        var parameters = new Dictionary<string, string>(record.Parameters, StringComparer.Ordinal);
        if (record.FailedFiles.Count > 0)
        {
            parameters["inputFiles"] = string.Join(Environment.NewLine, record.FailedFiles);
        }

        return parameters;
    }

    private static List<string> ExtractInputFiles(IReadOnlyDictionary<string, string> parameters)
    {
        if (!parameters.TryGetValue("inputFiles", out string? value) || string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split([Environment.NewLine, "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static Dictionary<string, string> SanitizeParameters(IReadOnlyDictionary<string, string> parameters)
    {
        // 任务历史只为“失败项重试”服务，不能变成密钥仓库。
        // 翻译、OCR、加密类工具会把 Key/Secret 注入参数，这里统一过滤后再落盘。
        return parameters
            .Where(pair => !IsSensitiveParameter(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
    }

    private static bool IsSensitiveParameter(string key)
    {
        return key.Contains("key", StringComparison.OrdinalIgnoreCase)
            || key.Contains("secret", StringComparison.OrdinalIgnoreCase)
            || key.Contains("token", StringComparison.OrdinalIgnoreCase)
            || key.Contains("password", StringComparison.OrdinalIgnoreCase)
            || key.Contains("apiKey", StringComparison.OrdinalIgnoreCase);
    }
}
