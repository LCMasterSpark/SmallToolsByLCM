// 文件作用：验证 3.1 新增更新检查、任务历史重试和 OCR 响应解析的纯逻辑。
using 小工具集合.Models;
using 小工具集合.Services;

namespace 小工具集合.Tests;

public sealed class Toolbox31ServiceTests
{
    [Fact]
    public void UpdateCheck_ParsesLatestReleaseAndExeAsset()
    {
        const string json = """
        {
          "tag_name": "v3.2",
          "html_url": "https://github.com/LCMasterSpark/SmallToolsByLCM/releases/tag/v3.2",
          "assets": [
            { "name": "LCM-Toolbox-3.2.exe", "browser_download_url": "https://example.test/toolbox.exe" }
          ]
        }
        """;

        UpdateCheckResult result = UpdateCheckService.ParseLatestReleaseJson(json, "3.1.0");

        Assert.True(result.Success);
        Assert.True(result.HasUpdate);
        Assert.Equal("v3.2", result.LatestVersion);
        Assert.Equal("https://example.test/toolbox.exe", result.DownloadUrl);
    }

    [Fact]
    public void TaskHistory_BuildRetryParametersKeepsOnlyFailedFiles()
    {
        var record = new TaskHistoryRecord
        {
            Parameters = new Dictionary<string, string>
            {
                ["inputFiles"] = "a.csv" + Environment.NewLine + "b.csv",
                ["outputDirectory"] = "out"
            },
            FailedFiles = ["b.csv"]
        };

        Dictionary<string, string> retry = TaskHistoryService.BuildRetryParameters(record);

        Assert.Equal("b.csv", retry["inputFiles"]);
        Assert.Equal("out", retry["outputDirectory"]);
    }

    [Fact]
    public void TaskHistory_CreateRecordDoesNotStoreInputText()
    {
        var tool = new ToolDefinition
        {
            Id = "csvCleaner",
            Name = "CSV 清洗",
            GroupName = "OfficeHelper",
            Description = "test",
            RequiresInput = false,
            Operations = [new ToolOperation { Id = "clean", Name = "清洗" }]
        };
        ToolOperation operation = tool.Operations[0];
        var request = new ToolRequest
        {
            ToolId = tool.Id,
            OperationId = operation.Id,
            Input = "secret text",
            Parameters = new Dictionary<string, string>
            {
                ["inputFiles"] = "a.csv",
                ["outputDirectory"] = "out",
                ["apiKey"] = "secret-key",
                ["openAiApiKey"] = "sk-secret",
                ["baiduSecret"] = "baidu-secret"
            }
        };

        TaskHistoryRecord record = TaskHistoryService.CreateRecord(tool, operation, request, ToolResult.Fail("bad"), DateTimeOffset.Now);

        Assert.Equal("csvCleaner", record.ToolId);
        Assert.Equal(["a.csv"], record.FailedFiles);
        Assert.DoesNotContain("secret", string.Join(' ', record.Parameters.Values), StringComparison.OrdinalIgnoreCase);
        Assert.False(record.Parameters.ContainsKey("apiKey"));
        Assert.False(record.Parameters.ContainsKey("openAiApiKey"));
        Assert.False(record.Parameters.ContainsKey("baiduSecret"));
    }

    [Fact]
    public void OcrSpaceParser_ReturnsParsedText()
    {
        const string json = """
        {
          "IsErroredOnProcessing": false,
          "ParsedResults": [
            { "ParsedText": "Hello OCR\r\nLCM Toolbox" }
          ]
        }
        """;

        string text = ToolProcessor.ParseOcrSpaceResponse(json);

        Assert.Contains("Hello OCR", text, StringComparison.Ordinal);
        Assert.Contains("OCR.space", text, StringComparison.Ordinal);
    }
}
