// 文件作用：实现 JSON、XML、JWT、正则测试和文本 Diff 工具。
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using 小工具集合.Models;

namespace 小工具集合.Services;

public sealed partial class ToolProcessor
{
    private static string Base64(ToolRequest request)
    {
        return request.OperationId == "encode"
            ? Convert.ToBase64String(Encoding.UTF8.GetBytes(request.Input))
            : Encoding.UTF8.GetString(Convert.FromBase64String(request.Input.Trim()));
    }

    private static string UnicodeEscape(ToolRequest request)
    {
        if (request.OperationId == "encode")
        {
            var builder = new StringBuilder();
            foreach (char value in request.Input)
            {
                builder.Append(value <= 0x7F ? value : FormattableString.Invariant($"\\u{(int)value:x4}"));
            }

            return builder.ToString();
        }

        return Regex.Replace(request.Input, @"\\u([0-9a-fA-F]{4})", match =>
        {
            int code = int.Parse(match.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return ((char)code).ToString();
        });
    }

    private static string Json(ToolRequest request)
    {
        using JsonDocument document = JsonDocument.Parse(request.Input);
        return JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions { WriteIndented = request.OperationId == "format" });
    }

    private static string Xml(ToolRequest request)
    {
        var document = new XmlDocument { PreserveWhitespace = false };
        document.LoadXml(request.Input);
        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            Indent = request.OperationId == "format",
            OmitXmlDeclaration = document.FirstChild is not XmlDeclaration
        };

        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        using XmlWriter xmlWriter = XmlWriter.Create(writer, settings);
        document.Save(xmlWriter);
        xmlWriter.Flush();
        return writer.ToString();
    }

    private static string ParseJwt(string input)
    {
        string[] parts = input.Trim().Split('.');
        if (parts.Length < 2)
        {
            throw new FormatException("JWT 至少应包含 Header 和 Payload 两段。");
        }

        var builder = new StringBuilder();
        builder.AppendLine("Header:");
        builder.AppendLine(PrettyJson(Encoding.UTF8.GetString(Base64UrlDecode(parts[0]))));
        builder.AppendLine();
        builder.AppendLine("Payload:");
        builder.AppendLine(PrettyJson(Encoding.UTF8.GetString(Base64UrlDecode(parts[1]))));
        builder.AppendLine();
        builder.AppendLine(parts.Length >= 3 ? "签名：存在（未校验）" : "签名：不存在");
        return builder.ToString();
    }

    private static string PrettyJson(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions { WriteIndented = true });
    }

    private static byte[] Base64UrlDecode(string value)
    {
        string base64 = value.Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + ((4 - base64.Length % 4) % 4), '=');
        return Convert.FromBase64String(base64);
    }

    private static string TestRegex(ToolRequest request)
    {
        string pattern = GetRequiredParameter(request, "pattern", "请输入正则表达式。");
        RegexOptions options = RegexOptions.None;
        if (IsTrue(GetParameter(request, "ignoreCase")))
        {
            options |= RegexOptions.IgnoreCase;
        }

        if (IsTrue(GetParameter(request, "multiline")))
        {
            options |= RegexOptions.Multiline;
        }

        MatchCollection matches;
        try
        {
            matches = Regex.Matches(request.Input, pattern, options, RegexTimeout);
            _ = matches.Count;
        }
        catch (RegexMatchTimeoutException ex)
        {
            throw new InvalidOperationException($"正则匹配超过 {RegexTimeout.TotalSeconds:0.#} 秒，请简化表达式或缩小输入文本。", ex);
        }

        var builder = new StringBuilder();
        builder.AppendLine($"匹配数量：{matches.Count}");
        for (int i = 0; i < matches.Count; i++)
        {
            Match match = matches[i];
            builder.AppendLine();
            builder.AppendLine($"[{i + 1}] Index={match.Index}, Length={match.Length}");
            builder.AppendLine(match.Value);
            for (int groupIndex = 1; groupIndex < match.Groups.Count; groupIndex++)
            {
                Group group = match.Groups[groupIndex];
                builder.AppendLine($"  Group {groupIndex}: {(group.Success ? group.Value : "<未匹配>")}");
            }
        }

        return builder.ToString();
    }

    private static string DiffText(ToolRequest request)
    {
        string[] oldLines = NormalizeLines(request.Input);
        string[] newLines = NormalizeLines(GetParameter(request, "newText"));
        var lines = new List<string> { "--- 原文本", "+++ 新文本" };
        foreach (DiffLine line in BuildMyersDiff(oldLines, newLines))
        {
            lines.Add(line.Kind switch
            {
                DiffKind.Insert => "+ " + line.Text,
                DiffKind.Delete => "- " + line.Text,
                _ => "  " + line.Text
            });
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string[] NormalizeLines(string text)
    {
        return text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
    }

    private enum DiffKind
    {
        Equal,
        Delete,
        Insert
    }

    private readonly record struct DiffLine(DiffKind Kind, string Text);

    private static IReadOnlyList<DiffLine> BuildMyersDiff(string[] oldLines, string[] newLines)
    {
        int oldCount = oldLines.Length;
        int newCount = newLines.Length;
        int maxDistance = oldCount + newCount;
        int offset = maxDistance + 1;
        int[] furthest = new int[(maxDistance + 1) * 2 + 1];
        Array.Fill(furthest, -1);
        furthest[offset + 1] = 0;
        var trace = new List<int[]>();

        for (int distance = 0; distance <= maxDistance; distance++)
        {
            for (int diagonal = -distance; diagonal <= distance; diagonal += 2)
            {
                int index = offset + diagonal;
                int x = diagonal == -distance || (diagonal != distance && furthest[index - 1] < furthest[index + 1])
                    ? furthest[index + 1]
                    : furthest[index - 1] + 1;
                x = Math.Max(0, x);
                int y = x - diagonal;

                while (x < oldCount && y < newCount && oldLines[x] == newLines[y])
                {
                    x++;
                    y++;
                }

                furthest[index] = x;
                if (x >= oldCount && y >= newCount)
                {
                    trace.Add((int[])furthest.Clone());
                    return BacktrackMyersDiff(oldLines, newLines, trace, offset);
                }
            }

            trace.Add((int[])furthest.Clone());
        }

        return [];
    }

    private static IReadOnlyList<DiffLine> BacktrackMyersDiff(string[] oldLines, string[] newLines, IReadOnlyList<int[]> trace, int offset)
    {
        int x = oldLines.Length;
        int y = newLines.Length;
        var output = new List<DiffLine>();

        for (int distance = trace.Count - 1; distance > 0; distance--)
        {
            int[] previous = trace[distance - 1];
            int diagonal = x - y;
            int previousDiagonal = diagonal == -distance || (diagonal != distance && GetFurthest(previous, offset, diagonal - 1) < GetFurthest(previous, offset, diagonal + 1))
                ? diagonal + 1
                : diagonal - 1;
            int previousX = Math.Max(0, GetFurthest(previous, offset, previousDiagonal));
            int previousY = previousX - previousDiagonal;

            while (x > previousX && y > previousY)
            {
                output.Add(new DiffLine(DiffKind.Equal, oldLines[x - 1]));
                x--;
                y--;
            }

            if (x == previousX)
            {
                if (y > 0)
                {
                    output.Add(new DiffLine(DiffKind.Insert, newLines[y - 1]));
                    y--;
                }
            }
            else if (x > 0)
            {
                output.Add(new DiffLine(DiffKind.Delete, oldLines[x - 1]));
                x--;
            }
        }

        while (x > 0 && y > 0 && oldLines[x - 1] == newLines[y - 1])
        {
            output.Add(new DiffLine(DiffKind.Equal, oldLines[x - 1]));
            x--;
            y--;
        }

        while (x > 0)
        {
            output.Add(new DiffLine(DiffKind.Delete, oldLines[x - 1]));
            x--;
        }

        while (y > 0)
        {
            output.Add(new DiffLine(DiffKind.Insert, newLines[y - 1]));
            y--;
        }

        output.Reverse();
        return output;
    }

    private static int GetFurthest(int[] furthest, int offset, int diagonal)
    {
        int index = offset + diagonal;
        return index >= 0 && index < furthest.Length ? furthest[index] : -1;
    }
}
