// 文件作用：注册文件与批处理分组的工具元数据。
using 小工具集合.Models;

namespace 小工具集合.Services;

public static partial class ToolCatalog
{
    private static ToolDefinition Mp4ToMp3() => new()
    {
        Id = "mp4ToMp3",
        Name = "MP4 提取 MP3",
        GroupName = "文件与批处理",
        Description = "批量从 MP4 文件中提取音频并保存为 MP3，需要本机可调用 ffmpeg。",
        InputWatermark = "可在参数区添加多个 MP4 文件；输入区可留空。",
        RequiresInput = false,
        Operations = [new() { Id = "extract", Name = "提取 MP3" }],
        Parameters =
        [
            new() { Id = "inputFiles", Name = "MP4 队列", Kind = ToolParameterKind.FileList },
            new() { Id = "outputDirectory", Name = "输出目录", Kind = ToolParameterKind.Directory },
            new() { Id = "quality", Name = "质量", Kind = ToolParameterKind.Combo, DefaultValue = "均衡", Options = ["高质量", "均衡", "小体积"] }
        ]
    };

    private static ToolDefinition FileEncode() => new()
    {
        Id = "fileEncode",
        Name = "Encode 文件加解密",
        GroupName = "文件与批处理",
        Description = "批量使用 Encode 密码格式加密或解密文件，输出到指定目录。",
        InputWatermark = "可在参数区添加多个文件；输入区可留空。",
        RequiresInput = false,
        Operations =
        [
            new() { Id = "encrypt", Name = "批量加密" },
            new() { Id = "decrypt", Name = "批量解密" }
        ],
        Parameters =
        [
            new() { Id = "inputFiles", Name = "文件队列", Kind = ToolParameterKind.FileList },
            new() { Id = "outputDirectory", Name = "输出目录", Kind = ToolParameterKind.Directory },
            new() { Id = "password", Name = "密码", Kind = ToolParameterKind.Password },
            new() { Id = "overwrite", Name = "覆盖同名", Kind = ToolParameterKind.CheckBox, DefaultValue = "true" }
        ]
    };

    private static ToolDefinition ImageConvert() => new()
    {
        Id = "imageConvert",
        Name = "图片格式互转",
        GroupName = "文件与批处理",
        Description = "批量转换 jpg、png、webp 和通用 raw 像素文件。",
        InputWatermark = "可在参数区添加多个图片文件；RAW 导入需填写宽高。",
        RequiresInput = false,
        Operations = [new() { Id = "convert", Name = "转换" }],
        Parameters =
        [
            new() { Id = "inputFiles", Name = "图片队列", Kind = ToolParameterKind.FileList },
            new() { Id = "outputDirectory", Name = "输出目录", Kind = ToolParameterKind.Directory },
            new() { Id = "targetFormat", Name = "目标格式", Kind = ToolParameterKind.Combo, DefaultValue = "png", Options = ["jpg", "png", "webp", "raw"] },
            new() { Id = "rawWidth", Name = "RAW 宽", Kind = ToolParameterKind.Number },
            new() { Id = "rawHeight", Name = "RAW 高", Kind = ToolParameterKind.Number },
            new() { Id = "rawPixelFormat", Name = "RAW 像素", Kind = ToolParameterKind.Combo, DefaultValue = "RGBA32", Options = ["RGBA32", "RGB24"] },
            new() { Id = "overwrite", Name = "覆盖同名", Kind = ToolParameterKind.CheckBox, DefaultValue = "true" }
        ]
    };

    private static ToolDefinition FileHash() => new()
    {
        Id = "fileHash",
        Name = "文件哈希",
        GroupName = "文件与批处理",
        Description = "批量计算文件 MD5、SHA-256 或 SHA-512 摘要。",
        InputWatermark = "可在参数区添加多个文件；输入区可留空。",
        RequiresInput = false,
        Operations = [new() { Id = "hash", Name = "计算哈希" }],
        Parameters =
        [
            new() { Id = "inputFiles", Name = "文件队列", Kind = ToolParameterKind.FileList },
            new() { Id = "algorithm", Name = "算法", Kind = ToolParameterKind.Combo, DefaultValue = "SHA-256", Options = ["SHA-256", "SHA-512", "MD5"] }
        ]
    };

    private static ToolDefinition ImageCompress() => new()
    {
        Id = "imageCompress",
        Name = "图片压缩/改尺寸",
        GroupName = "文件与批处理",
        Description = "批量压缩 jpg、png、webp 图片，可限制最大宽高。",
        InputWatermark = "可在参数区添加多个图片文件；输入区可留空。",
        RequiresInput = false,
        Operations = [new() { Id = "compress", Name = "压缩图片" }],
        Parameters =
        [
            new() { Id = "inputFiles", Name = "图片队列", Kind = ToolParameterKind.FileList },
            new() { Id = "outputDirectory", Name = "输出目录", Kind = ToolParameterKind.Directory },
            new() { Id = "quality", Name = "质量", Kind = ToolParameterKind.Number, DefaultValue = "85" },
            new() { Id = "maxWidth", Name = "最大宽", Kind = ToolParameterKind.Number },
            new() { Id = "maxHeight", Name = "最大高", Kind = ToolParameterKind.Number },
            new() { Id = "targetFormat", Name = "目标格式", Kind = ToolParameterKind.Combo, DefaultValue = "保持原格式", Options = ["保持原格式", "jpg", "png", "webp"] },
            new() { Id = "overwrite", Name = "覆盖同名", Kind = ToolParameterKind.CheckBox, DefaultValue = "true" },
            new() { Id = "advancedNote", Name = "预设说明", Kind = ToolParameterKind.ReadOnly, DefaultValue = "默认质量 85；最大宽/高留空表示不改尺寸；输出文件默认追加 _compressed。" }
        ]
    };
}
