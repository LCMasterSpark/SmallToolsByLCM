// 文件作用：注册文本格式化分组的工具元数据。
using 小工具集合.Models;

namespace 小工具集合.Services;

public static partial class ToolCatalog
{
    private static ToolDefinition Formatter(string id, string name, string description) => new()
    {
        Id = id,
        Name = name,
        GroupName = "文本格式化",
        Description = description,
        Operations =
        [
            new() { Id = "format", Name = "格式化" },
            new() { Id = "minify", Name = "压缩" }
        ]
    };

    private static ToolDefinition JwtParser() => new()
    {
        Id = "jwt",
        Name = "JWT 解析",
        GroupName = "文本格式化",
        Description = "本地解码 JWT Header/Payload，不校验签名。",
        InputWatermark = "请输入 JWT token。",
        Operations = [new() { Id = "parse", Name = "解析" }]
    };

    private static ToolDefinition RegexTester() => new()
    {
        Id = "regexTest",
        Name = "正则测试",
        GroupName = "文本格式化",
        Description = "测试正则表达式并输出匹配项、分组和索引。",
        InputWatermark = "请输入要测试的文本。",
        Operations = [new() { Id = "test", Name = "测试" }],
        Parameters =
        [
            new() { Id = "pattern", Name = "正则", Kind = ToolParameterKind.Multiline },
            new() { Id = "ignoreCase", Name = "忽略大小写", Kind = ToolParameterKind.CheckBox },
            new() { Id = "multiline", Name = "多行模式", Kind = ToolParameterKind.CheckBox }
        ]
    };

    private static ToolDefinition TextDiff() => new()
    {
        Id = "textDiff",
        Name = "文本差异",
        GroupName = "文本格式化",
        Description = "对比原文本和新文本，输出统一 diff。",
        InputWatermark = "请输入原文本。",
        Operations = [new() { Id = "diff", Name = "生成 Diff" }],
        Parameters = [new() { Id = "newText", Name = "新文本", Kind = ToolParameterKind.Multiline }]
    };

}
