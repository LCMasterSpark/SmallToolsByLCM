// 文件作用：维护工具分组顺序、聚合索引和旧工具 Id 兼容映射。
using 小工具集合.Models;

namespace 小工具集合.Services;

public static partial class ToolCatalog
{
    // 工具目录保持静态：工具定义属于不可变的应用元数据，
    // 用户输入的运行时值则保存在 MainWindowViewModel.Parameters 中。
    public static IReadOnlyList<ToolGroup> Groups { get; } =
    [
        new()
        {
            Name = "编码转换",
            Icon = "\uE8AB",
            Tools =
            [
                Codec("base64", "Base64", "UTF-8 文本与 Base64 互转。"),
                UrlTool(),
                Codec("html", "HTML 实体", "HTML 特殊字符与实体互转。"),
                Codec("unicode", "Unicode 转义", "中文和 Unicode 转义序列互转，例如 \\u4f60\\u597d。")
            ]
        },
        new()
        {
            Name = "文本格式化",
            Icon = "\uE943",
            Tools =
            [
                Formatter("json", "JSON", "JSON 格式化、压缩和校验。"),
                Formatter("xml", "XML", "XML 格式化、压缩和校验。"),
                JwtParser(),
                RegexTester(),
                TextDiff()
            ]
        },
        new()
        {
            Name = "加密摘要",
            Icon = "\uE72E",
            Tools =
            [
                Hash("sha256", "SHA-256", "计算 SHA-256 十六进制摘要。"),
                Hash("sha512", "SHA-512", "计算 SHA-512 十六进制摘要。"),
                Hash("md5", "MD5", "计算 MD5 摘要。", "MD5 已不适合安全用途，只建议用于旧系统校验。"),
                Hmac(),
                Aes(),
                EncodeCrypto(),
                Rsa()
            ]
        },
        new()
        {
            Name = "文件与批处理",
            Icon = "\uE8B7",
            Tools =
            [
                FileEncode(),
                Mp4ToMp3(),
                ImageConvert(),
                FileHash(),
                ImageCompress()
            ]
        },
        new()
        {
            Name = "OfficeHelper",
            Icon = "\uE8A5",
            Tools =
            [
                CsvCleaner(),
                ExcelSheetMerge(),
                WordTextExtract(),
                OfficeImageExtract(),
                WordBatchReplace(),
                ExcelCsvTools(),
                WordMerge(),
                PptTextExtract(),
                OfficeMetadata(),
                PdfTools(),
                LocalOfficeEngineCheck(),
                LocalOfficeConvertTools()
            ]
        },
        new()
        {
            Name = "翻译",
            Icon = "\uE8E2",
            Tools =
            [
                TextTranslator(),
                FileTranslator(),
                LiveTranslator()
            ]
        },
        new()
        {
            Name = "网络与接口",
            Icon = "\uE774",
            Tools =
            [
                HttpRequest(),
                NetworkText("urlParams", "URL 参数解析", "解析 URL 查询参数并格式化输出。", "parse", "解析参数"),
                NetworkText("headerFormat", "Header 格式化", "将 HTTP Header 文本格式化为逐行键值列表。", "format", "格式化"),
                NetworkText("cookieFormat", "Cookie 格式化", "将 Cookie 字符串拆分为逐行键值列表。", "format", "格式化"),
                Ping(),
                PortCheck(),
                PortUsage(),
                DnsLookup(),
                PublicIp(),
                CurlGenerator(),
                HostsReset(),
                ProxyReset(),
                NetworkReset()
            ]
        },
        new()
        {
            Name = "生成工具",
            Icon = "\uE710",
            Tools =
            [
                new()
                {
                    Id = "uuid",
                    Name = "UUID",
                    GroupName = "生成工具",
                    Description = "生成 UUID/GUID。",
                    InputWatermark = "UUID 工具不需要输入；可直接点击执行。",
                    RequiresInput = false,
                    Operations = [new() { Id = "new", Name = "生成 UUID" }]
                },
                new()
                {
                    Id = "timestamp",
                    Name = "时间戳",
                    GroupName = "生成工具",
                    Description = "Unix 时间戳与本地时间互转。",
                    InputWatermark = "输入 Unix 时间戳或日期时间；留空则使用当前时间。",
                    Operations =
                    [
                        new() { Id = "now", Name = "当前时间" },
                        new() { Id = "toDate", Name = "时间戳转时间" },
                        new() { Id = "toUnix", Name = "时间转时间戳" }
                    ]
                },
                PasswordGenerator(),
                QrCode(),
                OcrTool(),
                ScreenPointer()
            ]
        },
        new()
        {
            Name = "趣味实验室",
            Icon = "\uE7F4",
            Tools =
            [
                ChoicePicker(),
                ShuffleLines(),
                RandomNumber(),
                DiceRoller(),
                ReverseText(),
                EmojiWrap(),
                MockingText(),
                ZalgoText(),
                CommitMessage(),
                VariableName(),
                FakeLog(),
                ExcuseGenerator(),
                OnlineHitokoto(),
                OnlinePoemLine(),
                WeatherCard(),
                IpInfoCard(),
                PowerChecker(),
                TimePointer(),
                Minesweeper()
            ]
        }
    ];

    public static IReadOnlyList<ToolDefinition> AllTools { get; } = Groups.SelectMany(group => group.Tools).ToList();

    public static ToolDefinition DefaultTool => AllTools[0];

    public static ToolDefinition FindTool(string? id)
    {
        id = id switch
        {
            "excelToCsvBatch" or "csvToExcel" => "excelCsvTools",
            "pdfInfo" or "pdfTextExtract" or "pdfImageExtract" or "pdfToWordLite" => "pdfTools",
            "pdfToWordLocal" or "officeToPdfLocal" or "batchOfficeConvert" => "localOfficeConvert",
            _ => id
        };

        return AllTools.FirstOrDefault(tool => tool.Id == id) ?? DefaultTool;
    }
}
