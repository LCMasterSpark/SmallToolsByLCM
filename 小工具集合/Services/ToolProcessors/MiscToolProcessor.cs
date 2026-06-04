// 文件作用：实现 UUID、时间戳、密码生成等杂项生成工具。
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using 小工具集合.Models;

namespace 小工具集合.Services;

public sealed partial class ToolProcessor
{
    private static string Timestamp(ToolRequest request)
    {
        if (request.OperationId == "now")
        {
            DateTimeOffset now = DateTimeOffset.Now;
            return $"本地时间：{now:yyyy-MM-dd HH:mm:ss zzz}{Environment.NewLine}Unix 秒：{now.ToUnixTimeSeconds()}{Environment.NewLine}Unix 毫秒：{now.ToUnixTimeMilliseconds()}";
        }

        if (request.OperationId == "toDate")
        {
            string input = request.Input.Trim();
            if (!long.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out long unix))
            {
                throw new FormatException("请输入 Unix 时间戳数字。");
            }

            DateTimeOffset value = input.Length > 10 ? DateTimeOffset.FromUnixTimeMilliseconds(unix) : DateTimeOffset.FromUnixTimeSeconds(unix);
            return value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture);
        }

        if (!DateTimeOffset.TryParse(request.Input.Trim(), CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out DateTimeOffset dateTime))
        {
            throw new FormatException("请输入可识别的日期时间，例如 2026-05-31 22:30:00。");
        }

        return $"Unix 秒：{dateTime.ToUnixTimeSeconds()}{Environment.NewLine}Unix 毫秒：{dateTime.ToUnixTimeMilliseconds()}";
    }

    private static string GeneratePasswords(ToolRequest request)
    {
        const int count = 5;
        int length = ParseOptionalInt(request, "length", 16);
        if (length is < 4 or > 128)
        {
            throw new InvalidOperationException("密码长度必须在 4 到 128 位之间。");
        }

        string strength = GetParameter(request, "strength");
        bool excludeAmbiguous = IsTrue(GetParameter(request, "excludeAmbiguous"));
        var builder = new StringBuilder();

        if (strength == "弱密码")
        {
            builder.AppendLine("弱密码仅适合低安全场景。");
            builder.AppendLine();
            for (int i = 0; i < count; i++)
            {
                builder.AppendLine(GenerateReadablePassword(length, excludeAmbiguous));
            }

            return builder.ToString();
        }

        for (int i = 0; i < count; i++)
        {
            builder.AppendLine(GenerateStrongPassword(length, excludeAmbiguous));
        }

        return builder.ToString();
    }

    private static string GenerateStrongPassword(int length, bool excludeAmbiguous)
    {
        string lower = ApplyAmbiguousPolicy("abcdefghijklmnopqrstuvwxyz", excludeAmbiguous);
        string upper = ApplyAmbiguousPolicy("ABCDEFGHIJKLMNOPQRSTUVWXYZ", excludeAmbiguous);
        string digits = ApplyAmbiguousPolicy("0123456789", excludeAmbiguous);
        const string symbols = "!@#$%^&*()-_=+[]{};:,.?/";
        string all = lower + upper + digits + symbols;

        if (length < 4)
        {
            throw new InvalidOperationException("强密码长度至少需要 4 位。");
        }

        var chars = new List<char>
        {
            Pick(lower),
            Pick(upper),
            Pick(digits),
            Pick(symbols)
        };

        while (chars.Count < length)
        {
            chars.Add(Pick(all));
        }

        Shuffle(chars);
        return new string(chars.ToArray());
    }

    private static string GenerateReadablePassword(int length, bool excludeAmbiguous)
    {
        string consonants = ApplyAmbiguousPolicy("bcdfghjkmnpqrstvwxyz", excludeAmbiguous);
        string vowels = ApplyAmbiguousPolicy("aeu", excludeAmbiguous);
        string digits = ApplyAmbiguousPolicy("23456789", excludeAmbiguous);
        var builder = new StringBuilder(length);

        while (builder.Length < length)
        {
            string syllable = string.Create(CultureInfo.InvariantCulture, $"{Pick(consonants)}{Pick(vowels)}");
            foreach (char value in syllable)
            {
                if (builder.Length < length)
                {
                    builder.Append(value);
                }
            }

            if (builder.Length < length && builder.Length % 5 == 4)
            {
                builder.Append(Pick(digits));
            }
        }

        if (length >= 6 && !builder.ToString().Any(char.IsDigit))
        {
            int index = RandomNumberGenerator.GetInt32(1, length);
            builder[index] = Pick(digits);
        }

        return builder.ToString();
    }

    private static string ApplyAmbiguousPolicy(string characters, bool excludeAmbiguous)
    {
        const string ambiguous = "il1IoO0";
        return excludeAmbiguous
            ? new string(characters.Where(character => !ambiguous.Contains(character, StringComparison.Ordinal)).ToArray())
            : characters;
    }

    private static char Pick(string characters)
    {
        if (string.IsNullOrEmpty(characters))
        {
            throw new InvalidOperationException("密码字符集为空，请调整参数后重试。");
        }

        return characters[RandomNumberGenerator.GetInt32(characters.Length)];
    }

    private static void Shuffle(IList<char> values)
    {
        for (int i = values.Count - 1; i > 0; i--)
        {
            int j = RandomNumberGenerator.GetInt32(i + 1);
            (values[i], values[j]) = (values[j], values[i]);
        }
    }
}
