// 文件作用：注册编码转换分组的工具元数据。
using 小工具集合.Models;

namespace 小工具集合.Services;

public static partial class ToolCatalog
{
    private static ToolDefinition Codec(string id, string name, string description) => new()
    {
        Id = id,
        Name = name,
        GroupName = "编码转换",
        Description = description,
        Operations =
        [
            new() { Id = "encode", Name = "编码" },
            new() { Id = "decode", Name = "解码" }
        ]
    };

    private static ToolDefinition UrlTool() => new()
    {
        Id = "url",
        Name = "URL 工具",
        GroupName = "编码转换",
        Description = "URL 编码、解码和完整 URL 拆解。",
        InputWatermark = "输入要编码/解码的文本，或输入完整 URL / query string 进行拆解。",
        Operations =
        [
            new() { Id = "encode", Name = "编码" },
            new() { Id = "decode", Name = "解码" },
            new() { Id = "analyze", Name = "拆解" }
        ]
    };

}
