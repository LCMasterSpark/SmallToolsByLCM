// 文件作用：注册 OfficeHelper 分组和 PDF/本机转换工具元数据。
using 小工具集合.Models;

namespace 小工具集合.Services;

public static partial class ToolCatalog
{
    private static ToolDefinition CsvCleaner() => new()
    {
        Id = "csvCleaner",
        Name = "CSV 清洗",
        GroupName = "OfficeHelper",
        Description = "规范化 CSV 文件，支持分隔符选择、去空行、去重和单元格修剪。",
        InputWatermark = "在参数区添加 CSV 文件并选择输出目录。",
        RequiresInput = false,
        Operations = [new() { Id = "clean", Name = "清洗 CSV" }],
        Parameters =
        [
            new() { Id = "inputFiles", Name = "CSV 队列", Kind = ToolParameterKind.FileList },
            new() { Id = "outputDirectory", Name = "输出目录", Kind = ToolParameterKind.Directory },
            new() { Id = "delimiter", Name = "分隔符", Kind = ToolParameterKind.Combo, DefaultValue = "逗号 (,)", Options = ["逗号 (,)", "制表符 (Tab)", "分号 (;)", "竖线 (|)"] },
            new() { Id = "trimCells", Name = "修剪单元格空白", Kind = ToolParameterKind.CheckBox, DefaultValue = "true" },
            new() { Id = "removeEmptyRows", Name = "去空行", Kind = ToolParameterKind.CheckBox, DefaultValue = "true" },
            new() { Id = "distinctRows", Name = "去重行", Kind = ToolParameterKind.CheckBox }
        ]
    };

    private static ToolDefinition ExcelSheetMerge() => new()
    {
        Id = "excelSheetMerge",
        Name = "Excel 多表合并",
        GroupName = "OfficeHelper",
        Description = "把一个或多个 xlsx 的工作表合并为 CSV 或新的 xlsx。",
        InputWatermark = "在参数区添加 xlsx 文件并选择输出目录。",
        RequiresInput = false,
        Operations = [new() { Id = "merge", Name = "合并工作表" }],
        Parameters =
        [
            new() { Id = "inputFiles", Name = "Excel 队列", Kind = ToolParameterKind.FileList },
            new() { Id = "outputDirectory", Name = "输出目录", Kind = ToolParameterKind.Directory },
            new() { Id = "outputFormat", Name = "输出格式", Kind = ToolParameterKind.Combo, DefaultValue = "CSV", Options = ["CSV", "XLSX"] },
            new() { Id = "includeSourceColumns", Name = "加入来源列", Kind = ToolParameterKind.CheckBox, DefaultValue = "true" }
        ]
    };

    private static ToolDefinition WordTextExtract() => new()
    {
        Id = "wordTextExtract",
        Name = "Word 文本提取",
        GroupName = "OfficeHelper",
        Description = "从 docx 提取正文文本，可同时保存为 txt 文件。",
        InputWatermark = "在参数区添加 docx 文件；文本会输出到结果区。",
        RequiresInput = false,
        Operations = [new() { Id = "extract", Name = "提取文本" }],
        Parameters =
        [
            new() { Id = "inputFiles", Name = "Word 队列", Kind = ToolParameterKind.FileList },
            new() { Id = "outputDirectory", Name = "输出目录", Kind = ToolParameterKind.Directory },
            new() { Id = "saveTxt", Name = "另存 txt", Kind = ToolParameterKind.CheckBox },
            new() { Id = "includeFileHeaders", Name = "输出文件标题", Kind = ToolParameterKind.CheckBox, DefaultValue = "true" }
        ]
    };

    private static ToolDefinition OfficeImageExtract() => new()
    {
        Id = "officeImageExtract",
        Name = "Office 图片提取",
        GroupName = "OfficeHelper",
        Description = "从 docx、xlsx、pptx 提取内嵌图片到指定目录。",
        InputWatermark = "在参数区添加 Office 文件并选择输出目录。",
        RequiresInput = false,
        Operations = [new() { Id = "extract", Name = "提取图片" }],
        Parameters =
        [
            new() { Id = "inputFiles", Name = "Office 队列", Kind = ToolParameterKind.FileList },
            new() { Id = "outputDirectory", Name = "输出目录", Kind = ToolParameterKind.Directory }
        ]
    };

    private static ToolDefinition WordBatchReplace() => new()
    {
        Id = "wordBatchReplace",
        Name = "Word 批量替换",
        GroupName = "OfficeHelper",
        Description = "按多行规则替换 docx 文本并输出到新目录，不覆盖原文件。",
        InputWatermark = "替换规则写在参数区，格式：旧文本 => 新文本。",
        RequiresInput = false,
        Operations = [new() { Id = "replace", Name = "批量替换" }],
        Parameters =
        [
            new() { Id = "inputFiles", Name = "Word 队列", Kind = ToolParameterKind.FileList },
            new() { Id = "outputDirectory", Name = "输出目录", Kind = ToolParameterKind.Directory },
            new() { Id = "replacements", Name = "替换规则", Kind = ToolParameterKind.Multiline, DefaultValue = "旧文本 => 新文本" },
            new() { Id = "caseSensitive", Name = "区分大小写", Kind = ToolParameterKind.CheckBox, DefaultValue = "true" }
        ]
    };

    private static ToolDefinition ExcelCsvTools() => new()
    {
        Id = "excelCsvTools",
        Name = "Excel/CSV 工具",
        GroupName = "OfficeHelper",
        Description = "把 Excel 导出 CSV，或把多个 CSV 合成一个 Excel。",
        InputWatermark = "在参数区添加文件并选择输出目录，再用操作下拉框选择转换方向。",
        RequiresInput = false,
        Operations =
        [
            new() { Id = "excelToCsv", Name = "Excel 导出 CSV" },
            new() { Id = "csvToExcel", Name = "CSV 合成 Excel" }
        ],
        Parameters =
        [
            new() { Id = "inputFiles", Name = "文件队列", Kind = ToolParameterKind.FileList },
            new() { Id = "outputDirectory", Name = "输出目录", Kind = ToolParameterKind.Directory },
            new() { Id = "delimiter", Name = "分隔符", Kind = ToolParameterKind.Combo, DefaultValue = "逗号 (,)", Options = ["逗号 (,)", "制表符 (Tab)", "分号 (;)", "竖线 (|)"] },
            new() { Id = "sheetName", Name = "工作表名", Kind = ToolParameterKind.Text },
            new() { Id = "includeSheetName", Name = "文件名包含工作表", Kind = ToolParameterKind.CheckBox, DefaultValue = "true" },
            new() { Id = "workbookName", Name = "工作簿名", Kind = ToolParameterKind.Text, DefaultValue = "csv_merged" }
        ]
    };

    private static ToolDefinition ExcelToCsvBatch() => new()
    {
        Id = "excelToCsvBatch",
        Name = "Excel 导出 CSV",
        GroupName = "OfficeHelper",
        Description = "批量把 xlsx 工作表导出为 CSV，可指定工作表名。",
        InputWatermark = "在参数区添加 xlsx 文件并选择输出目录。",
        RequiresInput = false,
        Operations = [new() { Id = "export", Name = "导出 CSV" }],
        Parameters =
        [
            new() { Id = "inputFiles", Name = "Excel 队列", Kind = ToolParameterKind.FileList },
            new() { Id = "outputDirectory", Name = "输出目录", Kind = ToolParameterKind.Directory },
            new() { Id = "sheetName", Name = "工作表名", Kind = ToolParameterKind.Text },
            new() { Id = "includeSheetName", Name = "文件名包含工作表", Kind = ToolParameterKind.CheckBox, DefaultValue = "true" }
        ]
    };

    private static ToolDefinition CsvToExcel() => new()
    {
        Id = "csvToExcel",
        Name = "CSV 合成 Excel",
        GroupName = "OfficeHelper",
        Description = "把一个或多个 CSV 合成一个 xlsx，每个 CSV 一个工作表。",
        InputWatermark = "在参数区添加 CSV 文件并选择输出目录。",
        RequiresInput = false,
        Operations = [new() { Id = "convert", Name = "生成 XLSX" }],
        Parameters =
        [
            new() { Id = "inputFiles", Name = "CSV 队列", Kind = ToolParameterKind.FileList },
            new() { Id = "outputDirectory", Name = "输出目录", Kind = ToolParameterKind.Directory },
            new() { Id = "delimiter", Name = "分隔符", Kind = ToolParameterKind.Combo, DefaultValue = "逗号 (,)", Options = ["逗号 (,)", "制表符 (Tab)", "分号 (;)", "竖线 (|)"] },
            new() { Id = "workbookName", Name = "工作簿名", Kind = ToolParameterKind.Text, DefaultValue = "csv_merged" }
        ]
    };

    private static ToolDefinition WordMerge() => new()
    {
        Id = "wordMerge",
        Name = "Word 合并",
        GroupName = "OfficeHelper",
        Description = "把多个 docx 按队列顺序合并为一个 docx，保留基础段落和表格结构。",
        InputWatermark = "在参数区按顺序添加 docx 文件。",
        RequiresInput = false,
        Operations = [new() { Id = "merge", Name = "合并 Word" }],
        Parameters =
        [
            new() { Id = "inputFiles", Name = "Word 队列", Kind = ToolParameterKind.FileList },
            new() { Id = "outputDirectory", Name = "输出目录", Kind = ToolParameterKind.Directory },
            new() { Id = "outputName", Name = "输出文件名", Kind = ToolParameterKind.Text, DefaultValue = "word_merged" },
            new() { Id = "insertPageBreaks", Name = "文档间插入分页", Kind = ToolParameterKind.CheckBox, DefaultValue = "true" }
        ]
    };

    private static ToolDefinition PptTextExtract() => new()
    {
        Id = "pptTextExtract",
        Name = "PPT 文本提取",
        GroupName = "OfficeHelper",
        Description = "从 pptx 提取幻灯片文本，可同时另存 txt。",
        InputWatermark = "在参数区添加 pptx 文件。",
        RequiresInput = false,
        Operations = [new() { Id = "extract", Name = "提取 PPT 文本" }],
        Parameters =
        [
            new() { Id = "inputFiles", Name = "PPT 队列", Kind = ToolParameterKind.FileList },
            new() { Id = "outputDirectory", Name = "输出目录", Kind = ToolParameterKind.Directory },
            new() { Id = "saveTxt", Name = "另存 txt", Kind = ToolParameterKind.CheckBox },
            new() { Id = "includeSlideHeaders", Name = "输出幻灯片标题", Kind = ToolParameterKind.CheckBox, DefaultValue = "true" }
        ]
    };

    private static ToolDefinition OfficeMetadata() => new()
    {
        Id = "officeMetadata",
        Name = "Office 元数据",
        GroupName = "OfficeHelper",
        Description = "查看 docx、xlsx、pptx 的标题、作者、创建/修改时间等核心属性。",
        InputWatermark = "在参数区添加 Office 文件。",
        RequiresInput = false,
        Operations = [new() { Id = "inspect", Name = "查看元数据" }],
        Parameters = [new() { Id = "inputFiles", Name = "Office 队列", Kind = ToolParameterKind.FileList }]
    };

    private static ToolDefinition PdfTools() => new()
    {
        Id = "pdfTools",
        Name = "PDF 工具",
        GroupName = "OfficeHelper",
        Description = "查看 PDF 信息、提取文本/图片，或导出可编辑文字版 Word。",
        Warning = "Lite 转 Word 只承诺可编辑文本，不保证高保真排版；扫描件可能没有可提取文本。",
        InputWatermark = "在参数区添加 PDF 文件，按需选择输出目录，再用操作下拉框选择具体动作。",
        RequiresInput = false,
        Operations =
        [
            new() { Id = "info", Name = "PDF 信息" },
            new() { Id = "text", Name = "PDF 文本提取" },
            new() { Id = "images", Name = "PDF 图片提取" },
            new() { Id = "wordLite", Name = "PDF 转 Word Lite" }
        ],
        Parameters =
        [
            new() { Id = "inputFiles", Name = "PDF 队列", Kind = ToolParameterKind.FileList },
            new() { Id = "outputDirectory", Name = "输出目录", Kind = ToolParameterKind.Directory },
            new() { Id = "saveTxt", Name = "另存 txt", Kind = ToolParameterKind.CheckBox },
            new() { Id = "includePageHeaders", Name = "输出页码标题", Kind = ToolParameterKind.CheckBox, DefaultValue = "true" },
            new() { Id = "includePageBreaks", Name = "页间插入分页", Kind = ToolParameterKind.CheckBox, DefaultValue = "true" }
        ]
    };

    private static ToolDefinition PdfInfo() => new()
    {
        Id = "pdfInfo",
        Name = "PDF 信息",
        GroupName = "OfficeHelper",
        Description = "查看 PDF 页数、版本、元数据和每页尺寸。",
        InputWatermark = "在参数区添加 PDF 文件。",
        RequiresInput = false,
        Operations = [new() { Id = "inspect", Name = "查看 PDF 信息" }],
        Parameters = [new() { Id = "inputFiles", Name = "PDF 队列", Kind = ToolParameterKind.FileList }]
    };

    private static ToolDefinition PdfTextExtract() => new()
    {
        Id = "pdfTextExtract",
        Name = "PDF 文本提取",
        GroupName = "OfficeHelper",
        Description = "本地提取 PDF 文本，可同时保存为 txt。",
        InputWatermark = "在参数区添加 PDF 文件。",
        RequiresInput = false,
        Operations = [new() { Id = "extract", Name = "提取 PDF 文本" }],
        Parameters =
        [
            new() { Id = "inputFiles", Name = "PDF 队列", Kind = ToolParameterKind.FileList },
            new() { Id = "outputDirectory", Name = "输出目录", Kind = ToolParameterKind.Directory },
            new() { Id = "saveTxt", Name = "另存 txt", Kind = ToolParameterKind.CheckBox },
            new() { Id = "includePageHeaders", Name = "输出页码标题", Kind = ToolParameterKind.CheckBox, DefaultValue = "true" }
        ]
    };

    private static ToolDefinition PdfImageExtract() => new()
    {
        Id = "pdfImageExtract",
        Name = "PDF 图片提取",
        GroupName = "OfficeHelper",
        Description = "本地提取 PDF 页面中的内嵌图片。",
        InputWatermark = "在参数区添加 PDF 文件并选择输出目录。",
        RequiresInput = false,
        Operations = [new() { Id = "extract", Name = "提取 PDF 图片" }],
        Parameters =
        [
            new() { Id = "inputFiles", Name = "PDF 队列", Kind = ToolParameterKind.FileList },
            new() { Id = "outputDirectory", Name = "输出目录", Kind = ToolParameterKind.Directory }
        ]
    };

    private static ToolDefinition PdfToWordLite() => new()
    {
        Id = "pdfToWordLite",
        Name = "PDF 转 Word Lite",
        GroupName = "OfficeHelper",
        Description = "把 PDF 文本按页导出为 docx，只承诺可编辑文字版，不保证原排版。",
        Warning = "Lite 版不是高保真 PDF 转 Word；扫描件可能没有可提取文本。",
        InputWatermark = "在参数区添加 PDF 文件并选择输出目录。",
        RequiresInput = false,
        Operations = [new() { Id = "convert", Name = "生成 Lite DOCX" }],
        Parameters =
        [
            new() { Id = "inputFiles", Name = "PDF 队列", Kind = ToolParameterKind.FileList },
            new() { Id = "outputDirectory", Name = "输出目录", Kind = ToolParameterKind.Directory },
            new() { Id = "includePageBreaks", Name = "页间插入分页", Kind = ToolParameterKind.CheckBox, DefaultValue = "true" }
        ]
    };

    private static ToolDefinition LocalOfficeEngineCheck() => new()
    {
        Id = "localOfficeEngineCheck",
        Name = "本机 Office 引擎检测",
        GroupName = "OfficeHelper",
        Description = "检测 Microsoft Office COM、LibreOffice 和 WPS 实验引擎的可用性。",
        InputWatermark = "该工具不需要输入，直接执行。",
        RequiresInput = false,
        Operations = [new() { Id = "check", Name = "检测引擎" }]
    };

    private static ToolDefinition LocalOfficeConvertTools() => new()
    {
        Id = "localOfficeConvert",
        Name = "本机高级转换",
        GroupName = "OfficeHelper",
        Description = "调用本机 Office、LibreOffice 或 WPS 实验引擎完成高级格式转换。",
        Warning = "需要本机已安装对应引擎；转换质量和弹窗行为由本机软件决定。",
        InputWatermark = "在参数区添加 pdf/docx/xlsx/pptx 文件，选择输出目录、引擎和转换动作。",
        RequiresInput = false,
        Operations =
        [
            new() { Id = "pdfToWord", Name = "PDF 转 Word" },
            new() { Id = "officeToPdf", Name = "Office 转 PDF" },
            new() { Id = "batch", Name = "Office/PDF 批量转换" }
        ],
        Parameters =
        [
            new() { Id = "inputFiles", Name = "文件队列", Kind = ToolParameterKind.FileList },
            new() { Id = "outputDirectory", Name = "输出目录", Kind = ToolParameterKind.Directory },
            new() { Id = "targetFormat", Name = "目标格式", Kind = ToolParameterKind.Combo, DefaultValue = "PDF", Options = ["PDF", "DOCX"] },
            new() { Id = "engine", Name = "引擎", Kind = ToolParameterKind.Combo, DefaultValue = "自动检测", Options = ["自动检测", "Microsoft Office", "LibreOffice", "WPS 实验"] },
            new() { Id = "timeoutSeconds", Name = "单文件超时秒", Kind = ToolParameterKind.Number, DefaultValue = "120" }
        ]
    };

    private static ToolDefinition PdfToWordLocal() => new()
    {
        Id = "pdfToWordLocal",
        Name = "PDF 转 Word 本机高级",
        GroupName = "OfficeHelper",
        Description = "调用本机 Word、LibreOffice 或 WPS 实验引擎将 PDF 转为 docx。",
        Warning = "需要本机已安装对应引擎；转换质量由引擎决定。",
        InputWatermark = "在参数区添加 PDF 文件并选择输出目录。",
        RequiresInput = false,
        Operations = [new() { Id = "convert", Name = "本机转 Word" }],
        Parameters =
        [
            new() { Id = "inputFiles", Name = "PDF 队列", Kind = ToolParameterKind.FileList },
            new() { Id = "outputDirectory", Name = "输出目录", Kind = ToolParameterKind.Directory },
            new() { Id = "engine", Name = "引擎", Kind = ToolParameterKind.Combo, DefaultValue = "自动检测", Options = ["自动检测", "Microsoft Office", "LibreOffice", "WPS 实验"] },
            new() { Id = "timeoutSeconds", Name = "单文件超时秒", Kind = ToolParameterKind.Number, DefaultValue = "120" }
        ]
    };

    private static ToolDefinition OfficeToPdfLocal() => new()
    {
        Id = "officeToPdfLocal",
        Name = "Office 转 PDF 本机",
        GroupName = "OfficeHelper",
        Description = "调用本机 Office COM 或 LibreOffice 将 docx/xlsx/pptx 转为 PDF。",
        InputWatermark = "在参数区添加 docx、xlsx 或 pptx 文件。",
        RequiresInput = false,
        Operations = [new() { Id = "convert", Name = "转 PDF" }],
        Parameters =
        [
            new() { Id = "inputFiles", Name = "Office 队列", Kind = ToolParameterKind.FileList },
            new() { Id = "outputDirectory", Name = "输出目录", Kind = ToolParameterKind.Directory },
            new() { Id = "engine", Name = "引擎", Kind = ToolParameterKind.Combo, DefaultValue = "自动检测", Options = ["自动检测", "Microsoft Office", "LibreOffice", "WPS 实验"] },
            new() { Id = "timeoutSeconds", Name = "单文件超时秒", Kind = ToolParameterKind.Number, DefaultValue = "120" }
        ]
    };

    private static ToolDefinition BatchOfficeConvert() => new()
    {
        Id = "batchOfficeConvert",
        Name = "Office/PDF 批量转换",
        GroupName = "OfficeHelper",
        Description = "批量执行 PDF->DOCX 或 Office->PDF，按本机引擎可用性转换。",
        InputWatermark = "在参数区添加 pdf/docx/xlsx/pptx 文件。",
        RequiresInput = false,
        Operations = [new() { Id = "convert", Name = "批量转换" }],
        Parameters =
        [
            new() { Id = "inputFiles", Name = "文件队列", Kind = ToolParameterKind.FileList },
            new() { Id = "outputDirectory", Name = "输出目录", Kind = ToolParameterKind.Directory },
            new() { Id = "targetFormat", Name = "目标格式", Kind = ToolParameterKind.Combo, DefaultValue = "PDF", Options = ["PDF", "DOCX"] },
            new() { Id = "engine", Name = "引擎", Kind = ToolParameterKind.Combo, DefaultValue = "自动检测", Options = ["自动检测", "Microsoft Office", "LibreOffice", "WPS 实验"] },
            new() { Id = "timeoutSeconds", Name = "单文件超时秒", Kind = ToolParameterKind.Number, DefaultValue = "120" }
        ]
    };
}
