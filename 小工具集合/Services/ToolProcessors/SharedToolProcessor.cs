// 文件作用：提供 ToolProcessor partial 之间复用的参数、进程和文本辅助方法。
using System.Globalization;
using System.IO;
using 小工具集合.Models;

namespace 小工具集合.Services;

public sealed partial class ToolProcessor
{
    private static string[] GetInputFiles(ToolRequest request)
    {
        string raw = GetParameter(request, "inputFiles");
        string[] files = raw.Split(["\r\n", "\n", ";"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(path => path.Trim('"'))
            .Where(File.Exists)
            .ToArray();
        if (files.Length == 0)
        {
            throw new InvalidOperationException("请先添加至少一个有效文件。");
        }

        return files;
    }

    private static string EnsureOutputDirectory(ToolRequest request, string firstInputFile)
    {
        string outputDirectory = GetParameter(request, "outputDirectory").Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            outputDirectory = Path.GetDirectoryName(firstInputFile) ?? Environment.CurrentDirectory;
        }

        Directory.CreateDirectory(outputDirectory);
        return outputDirectory;
    }

    private static IEnumerable<(string Key, string Value)> ParseHeaderPairs(string headers)
    {
        foreach (string line in headers.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int index = line.IndexOf(':');
            if (index <= 0)
            {
                continue;
            }

            yield return (line[..index].Trim(), line[(index + 1)..].Trim());
        }
    }

    private static string GetRequiredParameter(ToolRequest request, string id, string message)
    {
        if (!request.Parameters.TryGetValue(id, out string? value) || string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(message);
        }

        return value;
    }

    private static string GetParameter(ToolRequest request, string id)
    {
        // 缺失的可选参数统一视为空字符串，让具体工具实现保持简洁。
        return request.Parameters.TryGetValue(id, out string? value) ? value : string.Empty;
    }

    private static int ParseOptionalInt(ToolRequest request, string id, int defaultValue = 0)
    {
        string value = GetParameter(request, id);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result) ? result : defaultValue;
    }

    private static bool IsTrue(string value)
    {
        return value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1" || value == "是";
    }

    private static string Quote(string value)
    {
        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }

    private static string TrimMessage(string message)
    {
        message = message.Trim();
        return message.Length <= 1200 ? message : message[^1200..];
    }
}
