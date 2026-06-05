// 文件作用：注册生成工具分组的工具元数据。
using 小工具集合.Models;

namespace 小工具集合.Services;

public static partial class ToolCatalog
{
    private static ToolDefinition PasswordGenerator() => new()
    {
        Id = "passwordGenerator",
        Name = "密码生成器",
        GroupName = "生成工具",
        Description = "生成强密码或更易输入的弱密码候选。",
        InputWatermark = "密码生成器不需要输入正文；请在参数区设置长度和强度。",
        RequiresInput = false,
        Operations = [new() { Id = "generate", Name = "生成密码" }],
        Parameters =
        [
            new() { Id = "strength", Name = "密码强度", Kind = ToolParameterKind.Combo, DefaultValue = "强密码", Options = ["强密码", "弱密码"] },
            new() { Id = "length", Name = "长度", Kind = ToolParameterKind.Number, DefaultValue = "16" },
            new() { Id = "excludeAmbiguous", Name = "排除易混淆字符", Kind = ToolParameterKind.CheckBox, DefaultValue = "true" },
            new()
            {
                Id = "ambiguousHelp",
                Name = "排除字符",
                Kind = ToolParameterKind.ReadOnly,
                DefaultValue = "默认排除：i l 1 I o O 0，避免手抄或口述时看错。"
            }
        ]
    };

    private static ToolDefinition ScreenPointer() => new()
    {
        Id = "screenPointer",
        Name = "屏幕指针",
        GroupName = "生成工具",
        Description = "生成并配置点击穿透的屏幕准星 overlay。",
        RequiresInput = false,
        Operations = [new() { Id = "interactive", Name = "互动" }],
        InteractiveViewKey = "screenPointer"
    };

    private static ToolDefinition QrCode() => new()
    {
        Id = "qrCode",
        Name = "生成 QR Code",
        GroupName = "生成工具",
        Description = "把文本或 URL 生成可预览、可复制、可保存的二维码图片。",
        RequiresInput = false,
        Operations = [new() { Id = "interactive", Name = "互动" }],
        InteractiveViewKey = "qrCode"
    };

    private static ToolDefinition OcrTool() => new()
    {
        Id = "screenshotOcr",
        Name = "截图 OCR",
        GroupName = "生成工具",
        Description = "从图片文件、剪贴板图片或全屏截图识别文字，在线优先并给出本地回退说明。",
        RequiresInput = false,
        Operations = [new() { Id = "recognize", Name = "识别文字" }],
        Parameters =
        [
            new() { Id = "source", Name = "图片来源", Kind = ToolParameterKind.Combo, DefaultValue = "图片文件", Options = ["图片文件", "剪贴板图片", "全屏截图"] },
            new() { Id = "imageFile", Name = "图片文件", Kind = ToolParameterKind.FileOpen },
            new() { Id = "language", Name = "语言", Kind = ToolParameterKind.Combo, DefaultValue = "自动", Options = ["自动", "中文简体", "中文繁体", "英文", "日文", "韩文"] },
            new() { Id = "engine", Name = "OCR.space 引擎", Kind = ToolParameterKind.Combo, DefaultValue = "2", Options = ["1", "2", "3"] },
            new() { Id = "scale", Name = "低清图片增强", Kind = ToolParameterKind.CheckBox, DefaultValue = "true" },
            new()
            {
                Id = "privacy",
                Name = "隐私提示",
                Kind = ToolParameterKind.ReadOnly,
                DefaultValue = "在线 OCR 会把图片上传到 OCR.space；关闭联网后会尝试本地回退。"
            }
        ]
    };
}
