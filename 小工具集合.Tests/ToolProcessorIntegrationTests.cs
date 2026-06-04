// 文件作用：覆盖 ToolProcessor 中不依赖真实外部服务的端到端工具执行路径。
using System.Text.RegularExpressions;
using 小工具集合.Models;
using 小工具集合.Services;

namespace 小工具集合.Tests;

public sealed class ToolProcessorIntegrationTests
{
    private readonly ToolProcessor processor = new();

    [Fact]
    public void Base64_EncodesAndDecodesUtf8Text()
    {
        ToolResult encoded = Execute("base64", "encode", "LCMasterSpark 你好");
        Assert.True(encoded.Success);
        Assert.False(string.IsNullOrWhiteSpace(encoded.Output));

        ToolResult decoded = Execute("base64", "decode", encoded.Output);
        Assert.True(decoded.Success);
        Assert.Equal("LCMasterSpark 你好", decoded.Output);
    }

    [Fact]
    public void UrlTool_EncodesDecodesAndAnalyzesUrl()
    {
        ToolResult encoded = Execute("url", "encode", "a b&c=1");
        Assert.True(encoded.Success);
        Assert.Contains("a+b", encoded.Output, StringComparison.Ordinal);
        Assert.Contains("%26", encoded.Output, StringComparison.Ordinal);

        ToolResult decoded = Execute("url", "decode", "a%20b%26c%3D1");
        Assert.True(decoded.Success);
        Assert.Equal("a b&c=1", decoded.Output);

        ToolResult analyzed = Execute("url", "analyze", "https://example.com:8443/a/b?x=1&x=2&name=%E5%BC%A0#top");
        Assert.True(analyzed.Success);
        Assert.Contains("example.com", analyzed.Output, StringComparison.Ordinal);
        Assert.Contains("8443", analyzed.Output, StringComparison.Ordinal);
        Assert.Contains("x = 1", analyzed.Output, StringComparison.Ordinal);
        Assert.Contains("name = 张", analyzed.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void TextDiff_ReturnsUnifiedStyleDiff()
    {
        ToolResult result = Execute("textDiff", "diff", "one\ntwo", new Dictionary<string, string>
        {
            ["newText"] = "one\nthree"
        });

        Assert.True(result.Success);
        Assert.Contains("--- 原文本", result.Output, StringComparison.Ordinal);
        Assert.Contains("+++ 新文本", result.Output, StringComparison.Ordinal);
        Assert.Contains("- two", result.Output, StringComparison.Ordinal);
        Assert.Contains("+ three", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void RandomNumber_WithUniqueRangeReturnsEveryValueOnce()
    {
        ToolResult result = Execute("randomNumber", "generate", string.Empty, new Dictionary<string, string>
        {
            ["min"] = "1",
            ["max"] = "3",
            ["count"] = "3",
            ["unique"] = "true"
        });

        Assert.True(result.Success);
        string numbersLine = result.Output
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Last();
        int[] values = Regex.Matches(numbersLine, @"\d+")
            .Select(match => int.Parse(match.Value))
            .Order()
            .ToArray();
        Assert.Equal([1, 2, 3], values);
    }

    [Fact]
    public void DiceRoller_RejectsInvalidExpression()
    {
        ToolResult result = Execute("diceRoller", "roll", "totally-not-dice");

        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
    }

    [Fact]
    public void AesGcm_RoundTripsTextWithPassword()
    {
        var parameters = new Dictionary<string, string> { ["password"] = "correct horse battery staple" };

        ToolResult encrypted = Execute("aesGcm", "encrypt", "secret payload", parameters);
        Assert.True(encrypted.Success);
        Assert.NotEqual("secret payload", encrypted.Output);

        ToolResult decrypted = Execute("aesGcm", "decrypt", encrypted.Output, parameters);
        Assert.True(decrypted.Success);
        Assert.Equal("secret payload", decrypted.Output);
    }

    private ToolResult Execute(
        string toolId,
        string operationId,
        string input,
        IReadOnlyDictionary<string, string>? parameters = null)
    {
        return processor.Execute(new ToolRequest
        {
            ToolId = toolId,
            OperationId = operationId,
            Input = input,
            Parameters = parameters ?? new Dictionary<string, string>()
        });
    }
}
