// 文件作用：注册翻译分组的文本、文件和实时翻译工具元数据。
using 小工具集合.Models;

namespace 小工具集合.Services;

public static partial class ToolCatalog
{
    private static ToolDefinition TextTranslator() => new()
    {
        Id = "textTranslator",
        Name = "文本翻译",
        GroupName = "翻译",
        Description = "普通输入输出式翻译，适合短文本、代码注释和片段翻译。",
        InputWatermark = "请输入要翻译的文本。",
        Operations = [new() { Id = "translate", Name = "翻译文本" }],
        Parameters =
        [
            new() { Id = "provider", Name = "翻译引擎", Kind = ToolParameterKind.Combo, DefaultValue = "LibreTranslate", Options = ["LibreTranslate", "Azure Translator", "DeepL", "Google Translate", "百度翻译", "网易有道", "OpenAI-compatible", "Ollama"] },
            new() { Id = "sourceLanguage", Name = "源语言", Kind = ToolParameterKind.Combo, DefaultValue = "自动", Options = ["自动", "中文", "英文", "日文", "韩文", "法文", "德文", "西班牙文", "俄文"] },
            new() { Id = "targetLanguage", Name = "目标语言", Kind = ToolParameterKind.Combo, DefaultValue = "中文", Options = ["中文", "英文", "日文", "韩文", "法文", "德文", "西班牙文", "俄文"] },
            new() { Id = "useGlossary", Name = "使用术语表", Kind = ToolParameterKind.CheckBox }
        ]
    };

    private static ToolDefinition FileTranslator() => new()
    {
        Id = "fileTranslator",
        Name = "文件翻译",
        GroupName = "翻译",
        Description = "批量翻译文本类文件和 Office 文件，输出翻译副本，不覆盖原文件。",
        InputWatermark = "在参数区添加要翻译的文件；输入区可留空。",
        RequiresInput = false,
        Operations = [new() { Id = "translate", Name = "翻译文件" }],
        Parameters =
        [
            new() { Id = "inputFiles", Name = "文件队列", Kind = ToolParameterKind.FileList },
            new() { Id = "outputDirectory", Name = "输出目录", Kind = ToolParameterKind.Directory },
            new() { Id = "provider", Name = "翻译引擎", Kind = ToolParameterKind.Combo, DefaultValue = "LibreTranslate", Options = ["LibreTranslate", "Azure Translator", "DeepL", "Google Translate", "百度翻译", "网易有道", "OpenAI-compatible", "Ollama"] },
            new() { Id = "sourceLanguage", Name = "源语言", Kind = ToolParameterKind.Combo, DefaultValue = "自动", Options = ["自动", "中文", "英文", "日文", "韩文", "法文", "德文", "西班牙文", "俄文"] },
            new() { Id = "targetLanguage", Name = "目标语言", Kind = ToolParameterKind.Combo, DefaultValue = "中文", Options = ["中文", "英文", "日文", "韩文", "法文", "德文", "西班牙文", "俄文"] },
            new() { Id = "useGlossary", Name = "使用术语表", Kind = ToolParameterKind.CheckBox },
            new()
            {
                Id = "fileNote",
                Name = "格式说明",
                Kind = ToolParameterKind.ReadOnly,
                DefaultValue = "支持 txt、md、csv、json、xml、html、docx、pptx、xlsx；PDF 暂不翻译。输出文件会追加 _translated。"
            }
        ]
    };

    private static ToolDefinition LiveTranslator() => new()
    {
        Id = "liveTranslator",
        Name = "实时翻译",
        GroupName = "翻译",
        Description = "Luna 风格轻量实时翻译：悬浮窗、区域/窗口 OCR、自动刷新和重复文本过滤。",
        RequiresInput = false,
        Operations = [new() { Id = "interactive", Name = "互动" }],
        InteractiveViewKey = "liveTranslator"
    };

}
