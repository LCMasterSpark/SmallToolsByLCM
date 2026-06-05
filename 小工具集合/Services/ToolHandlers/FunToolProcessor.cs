// 文件作用：实现趣味实验室的离线随机、文本整活和程序员梗工具。
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using 小工具集合.Models;

namespace 小工具集合.Services;

public sealed partial class ToolProcessor
{
    private static readonly char[] ZalgoMarks =
    [
        '\u0300', '\u0301', '\u0302', '\u0303', '\u0304', '\u0306', '\u0307', '\u0308',
        '\u030A', '\u030B', '\u030C', '\u0310', '\u0311', '\u0312', '\u0313', '\u031A',
        '\u0323', '\u0324', '\u0325', '\u0326', '\u0327', '\u0328', '\u0331', '\u0332'
    ];

    private static string PickChoice(ToolRequest request)
    {
        string[] choices = GetFunLines(request.Input, IsTrue(GetParameter(request, "trimEmpty")), IsTrue(GetParameter(request, "distinct")));
        if (choices.Length == 0)
        {
            throw new InvalidOperationException("请至少输入一个候选项。");
        }

        string choice = choices[RandomIndex(choices.Length)];
        return $"候选数量：{choices.Length}{Environment.NewLine}抽中：{choice}";
    }

    private static string ShuffleLines(ToolRequest request)
    {
        string[] lines = GetFunLines(request.Input, IsTrue(GetParameter(request, "trimEmpty")), IsTrue(GetParameter(request, "distinct")));
        if (lines.Length == 0)
        {
            throw new InvalidOperationException("请至少输入一行文本。");
        }

        ShuffleList(lines);
        return string.Join(Environment.NewLine, lines);
    }

    private static string GenerateRandomNumbers(ToolRequest request)
    {
        int min = GetRequiredInt(request, "min", 1);
        int max = GetRequiredInt(request, "max", 100);
        int count = Math.Clamp(GetRequiredInt(request, "count", 1), 1, 1000);
        bool unique = IsTrue(GetParameter(request, "unique"));
        if (min > max)
        {
            throw new InvalidOperationException("最小值不能大于最大值。");
        }

        long range = (long)max - min + 1;
        if (unique && count > range)
        {
            throw new InvalidOperationException("不重复数量超过可用数字范围。");
        }

        var values = new List<int>(count);
        if (unique)
        {
            if (range > 100_000)
            {
                throw new InvalidOperationException("不重复随机的范围过大，请缩小范围到 100000 以内。");
            }

            var pool = Enumerable.Range(min, (int)range).ToArray();
            ShuffleList(pool);
            values.AddRange(pool.Take(count));
        }
        else
        {
            for (int i = 0; i < count; i++)
            {
                values.Add(RandomInclusive(min, max));
            }
        }

        var builder = new StringBuilder();
        builder.AppendLine($"范围：{min} 到 {max}");
        builder.AppendLine($"数量：{count}");
        builder.AppendLine("结果：");
        builder.AppendLine(string.Join(", ", values));
        return builder.ToString();
    }

    private static string RollDice(ToolRequest request)
    {
        string expression = request.Input.Trim();
        if (string.IsNullOrWhiteSpace(expression))
        {
            expression = "1d6";
        }

        Match match = Regex.Match(expression, @"^\s*(\d*)d(\d+)([+-]\d+)?\s*$", RegexOptions.IgnoreCase, RegexTimeout);
        if (!match.Success)
        {
            throw new FormatException("请输入骰子表达式，例如 1d6、2d20+3。");
        }

        int count = string.IsNullOrEmpty(match.Groups[1].Value) ? 1 : int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        int sides = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        int modifier = string.IsNullOrEmpty(match.Groups[3].Value) ? 0 : int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
        if (count is < 1 or > 100 || sides is < 2 or > 1000)
        {
            throw new InvalidOperationException("骰子数量需在 1-100 之间，面数需在 2-1000 之间。");
        }

        int[] rolls = new int[count];
        for (int i = 0; i < rolls.Length; i++)
        {
            rolls[i] = RandomInclusive(1, sides);
        }

        int sum = rolls.Sum();
        int total = sum + modifier;
        var builder = new StringBuilder();
        builder.AppendLine($"表达式：{count}d{sides}{(modifier == 0 ? string.Empty : modifier.ToString("+#;-#;0", CultureInfo.InvariantCulture))}");
        builder.AppendLine($"骰子：{string.Join(", ", rolls)}");
        builder.AppendLine($"点数：{sum}");
        builder.AppendLine($"修正：{modifier}");
        builder.AppendLine($"总计：{total}");
        return builder.ToString();
    }

    private static string ReverseFunText(ToolRequest request)
    {
        string mode = GetParameter(request, "mode");
        string[] lines = SplitLines(request.Input);
        return mode switch
        {
            "逐行倒放" => string.Join(Environment.NewLine, lines.Select(ReverseTextElements)),
            "行序倒放" => string.Join(Environment.NewLine, lines.Reverse()),
            _ => ReverseTextElements(request.Input)
        };
    }

    private static string WrapWithEmoji(ToolRequest request)
    {
        string[] emoji = GetEmojiPool(GetParameter(request, "style"));
        bool bothSides = IsTrue(GetParameter(request, "bothSides"));
        string[] lines = SplitLines(request.Input);
        if (lines.Length == 0 || (lines.Length == 1 && string.IsNullOrEmpty(lines[0])))
        {
            lines = ["今天也可以很有意思。"];
        }

        string local = string.Join(Environment.NewLine, lines.Select(line =>
        {
            string left = RandomItem(emoji);
            string right = bothSides ? " " + RandomItem(emoji) : string.Empty;
            return $"{left} {line}{right}";
        }));

        return UsesOnlineEnhancement(request) ? EnhanceEmojiWrapOnline(request, local) : local;
    }

    private static string MockText(ToolRequest request)
    {
        string mode = GetParameter(request, "mode");
        return mode switch
        {
            "随机大小写" => RandomCase(request.Input),
            "加波浪" => string.Join("～", SplitTextElements(request.Input)) + "～",
            _ => AlternatingCase(request.Input)
        };
    }

    private static string GlitchText(ToolRequest request)
    {
        int marks = GetParameter(request, "strength") switch
        {
            "高" => 3,
            "中" => 2,
            _ => 1
        };

        var builder = new StringBuilder();
        foreach (string element in SplitTextElements(request.Input))
        {
            builder.Append(element);
            if (!string.IsNullOrWhiteSpace(element))
            {
                int count = RandomInclusive(0, marks);
                for (int i = 0; i < count; i++)
                {
                    builder.Append(ZalgoMarks[RandomIndex(ZalgoMarks.Length)]);
                }
            }
        }

        return builder.ToString();
    }

    private static string GenerateCommitMessages(ToolRequest request)
    {
        string summary = string.IsNullOrWhiteSpace(request.Input) ? "更新工具箱功能" : request.Input.Trim();
        string language = GetParameter(request, "language");
        string type = GetParameter(request, "type");
        if (string.IsNullOrWhiteSpace(type) || type == "自动")
        {
            type = InferCommitType(summary);
        }

        var builder = new StringBuilder();
        if (language == "English")
        {
            string phrase = ToSentence(summary);
            builder.AppendLine($"{type}: {phrase}");
            builder.AppendLine($"{type}(tools): {phrase}");
            builder.AppendLine($"{type}: update utility toolbox");
        }
        else
        {
            builder.AppendLine($"{type}: {summary}");
            builder.AppendLine($"{type}(tools): {summary}");
            builder.AppendLine($"{type}: 更新 LCM的工具箱");
        }

        string local = builder.ToString();
        return UsesOnlineEnhancement(request) ? EnhanceCommitMessagesOnline(request, local) : local;
    }

    private static string GenerateVariableNames(ToolRequest request)
    {
        string input = string.IsNullOrWhiteSpace(request.Input) ? "new value" : request.Input;
        string[] words = TokenizeNameWords(input);
        if (words.Length == 0)
        {
            throw new InvalidOperationException("请输入至少一个可用于命名的词。");
        }

        string camel = ToCamelCase(words);
        string pascal = ToPascalCase(words);
        string snake = string.Join('_', words);
        string kebab = string.Join('-', words);
        var builder = new StringBuilder();
        builder.AppendLine($"camelCase：{camel}");
        builder.AppendLine($"PascalCase：{pascal}");
        builder.AppendLine($"snake_case：{snake}");
        builder.AppendLine($"kebab-case：{kebab}");
        builder.AppendLine($"CONSTANT_CASE：{snake.ToUpperInvariant()}");
        string local = builder.ToString();
        return UsesOnlineEnhancement(request) ? EnhanceVariableNamesOnline(request, local) : local;
    }

    private static string GenerateFakeLog(ToolRequest request)
    {
        string level = GetParameter(request, "level");
        int count = Math.Clamp(GetRequiredInt(request, "count", 8), 1, 100);
        string service = GetParameter(request, "service");
        if (string.IsNullOrWhiteSpace(service))
        {
            service = "fun-lab";
        }

        string topic = string.IsNullOrWhiteSpace(request.Input) ? "background job" : request.Input.Trim();
        string[] levels = level == "混合" ? ["INFO", "DEBUG", "WARN", "ERROR"] : [level];
        string[] messages =
        [
            "started processing",
            "loaded local configuration",
            "cache state changed",
            "retry window opened",
            "validation completed",
            "unexpected input ignored",
            "result emitted",
            "operation finished"
        ];

        DateTimeOffset now = DateTimeOffset.Now;
        var builder = new StringBuilder();
        for (int i = 0; i < count; i++)
        {
            string itemLevel = RandomItem(levels);
            string message = RandomItem(messages);
            int trace = RandomNumberGenerator.GetInt32(100000, 999999);
            builder.AppendLine($"{now.AddSeconds(i):yyyy-MM-dd HH:mm:ss.fff zzz} [{itemLevel}] {service} trace={trace} topic=\"{topic}\" {message}");
        }

        string local = builder.ToString();
        return UsesOnlineEnhancement(request) ? EnhanceFakeLogOnline(request, local) : local;
    }

    private static string GenerateExcuse(ToolRequest request)
    {
        string context = string.IsNullOrWhiteSpace(request.Input) ? "这个问题" : request.Input.Trim();
        string[] causes =
        [
            "本地缓存和实际状态达成了短暂但坚定的分歧",
            "配置项以一种非常自信的方式理解错了需求",
            "旧逻辑在新场景里坚持发挥余热",
            "边界条件刚好站在了最显眼的位置",
            "默认值表现得过于积极"
        ];
        string[] fixes =
        [
            "我先加一层校验把它按住",
            "我把复现路径收窄后再动手",
            "我会补一个回归用例防止它复出",
            "我先把日志打亮一点",
            "我把这个行为改成显式配置"
        ];

        string local = $"{context}：{RandomItem(causes)}。{RandomItem(fixes)}。";
        return UsesOnlineEnhancement(request) ? EnhanceExcuseOnline(request, local) : local;
    }

    private static string[] GetFunLines(string input, bool trimEmpty, bool distinct)
    {
        IEnumerable<string> lines = SplitLines(input);
        if (trimEmpty)
        {
            lines = lines.Select(line => line.Trim()).Where(line => !string.IsNullOrWhiteSpace(line));
        }

        if (distinct)
        {
            lines = lines.Distinct(StringComparer.OrdinalIgnoreCase);
        }

        return lines.ToArray();
    }

    private static string[] SplitLines(string input)
    {
        return input.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
    }

    private static string ReverseTextElements(string input)
    {
        string[] elements = SplitTextElements(input);
        Array.Reverse(elements);
        return string.Concat(elements);
    }

    private static string[] SplitTextElements(string input)
    {
        int[] indexes = StringInfo.ParseCombiningCharacters(input);
        var elements = new string[indexes.Length];
        for (int i = 0; i < indexes.Length; i++)
        {
            int start = indexes[i];
            int end = i + 1 < indexes.Length ? indexes[i + 1] : input.Length;
            elements[i] = input[start..end];
        }

        return elements;
    }

    private static string[] GetEmojiPool(string style)
    {
        return style switch
        {
            "开心" => ["😀", "😄", "🥳", "✨", "👏"],
            "星星" => ["✨", "⭐", "🌟", "💫", "🔆"],
            "办公" => ["📌", "✅", "🧪", "🛠️", "📎"],
            _ => ["✨", "🎲", "🧪", "⭐", "✅", "💡", "📌", "🌟"]
        };
    }

    private static string AlternatingCase(string input)
    {
        var builder = new StringBuilder(input.Length);
        bool upper = true;
        foreach (char value in input)
        {
            if (char.IsLetter(value))
            {
                builder.Append(upper ? char.ToUpperInvariant(value) : char.ToLowerInvariant(value));
                upper = !upper;
            }
            else
            {
                builder.Append(value);
            }
        }

        return builder.ToString();
    }

    private static string RandomCase(string input)
    {
        var builder = new StringBuilder(input.Length);
        foreach (char value in input)
        {
            builder.Append(char.IsLetter(value) && RandomIndex(2) == 0 ? char.ToUpperInvariant(value) : char.ToLowerInvariant(value));
        }

        return builder.ToString();
    }

    private static string InferCommitType(string summary)
    {
        if (summary.Contains("修", StringComparison.OrdinalIgnoreCase) || summary.Contains("fix", StringComparison.OrdinalIgnoreCase))
        {
            return "fix";
        }

        if (summary.Contains("重构", StringComparison.OrdinalIgnoreCase) || summary.Contains("refactor", StringComparison.OrdinalIgnoreCase))
        {
            return "refactor";
        }

        if (summary.Contains("文档", StringComparison.OrdinalIgnoreCase) || summary.Contains("docs", StringComparison.OrdinalIgnoreCase))
        {
            return "docs";
        }

        return "feat";
    }

    private static string ToSentence(string input)
    {
        string value = Regex.Replace(input.Trim(), @"\s+", " ");
        return string.IsNullOrWhiteSpace(value) ? "update utilities" : value;
    }

    private static string[] TokenizeNameWords(string input)
    {
        var words = new List<string>();
        var current = new StringBuilder();
        foreach (char value in input)
        {
            if (value is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                current.Append(char.ToLowerInvariant(value));
            }
            else
            {
                FlushToken(words, current);
                if (char.IsLetterOrDigit(value))
                {
                    words.Add("u" + ((int)value).ToString("x4", CultureInfo.InvariantCulture));
                }
            }
        }

        FlushToken(words, current);
        return words.Select(SanitizeNameWord).Where(word => word.Length > 0).ToArray();
    }

    private static void FlushToken(List<string> words, StringBuilder current)
    {
        if (current.Length == 0)
        {
            return;
        }

        words.Add(current.ToString());
        current.Clear();
    }

    private static string SanitizeNameWord(string word)
    {
        string value = Regex.Replace(word, @"[^a-zA-Z0-9_]", string.Empty);
        return string.IsNullOrEmpty(value) ? string.Empty : value;
    }

    private static string ToCamelCase(string[] words)
    {
        string value = words[0] + string.Concat(words.Skip(1).Select(Capitalize));
        return char.IsDigit(value[0]) ? "value" + Capitalize(value) : value;
    }

    private static string ToPascalCase(string[] words)
    {
        string value = string.Concat(words.Select(Capitalize));
        return char.IsDigit(value[0]) ? "Value" + value : value;
    }

    private static string Capitalize(string word)
    {
        return string.IsNullOrEmpty(word) ? string.Empty : char.ToUpperInvariant(word[0]) + word[1..];
    }

    private static int GetRequiredInt(ToolRequest request, string id, int defaultValue)
    {
        string value = GetParameter(request, id);
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result))
        {
            throw new InvalidOperationException($"参数“{id}”需要填写整数。");
        }

        return result;
    }

    private static int RandomInclusive(int min, int max)
    {
        if (min == max)
        {
            return min;
        }

        ulong range = (ulong)((long)max - min) + 1UL;
        ulong limit = ulong.MaxValue - (ulong.MaxValue % range);
        ulong sample;
        do
        {
            sample = BitConverter.ToUInt64(RandomNumberGenerator.GetBytes(sizeof(ulong)));
        }
        while (sample >= limit);

        return (int)((long)(sample % range) + min);
    }

    private static int RandomIndex(int length)
    {
        return RandomNumberGenerator.GetInt32(length);
    }

    private static string RandomItem(IReadOnlyList<string> values)
    {
        return values[RandomIndex(values.Count)];
    }

    private static void ShuffleList<T>(IList<T> values)
    {
        for (int i = values.Count - 1; i > 0; i--)
        {
            int j = RandomNumberGenerator.GetInt32(i + 1);
            (values[i], values[j]) = (values[j], values[i]);
        }
    }
}
