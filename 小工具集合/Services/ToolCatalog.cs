using 小工具集合.Models;

namespace 小工具集合.Services;

/// <summary>
/// 应用中所有工具的集中注册表。
/// 新增工具时先在这里声明元数据，再到 ToolProcessor 中补执行分支。
/// </summary>
public static class ToolCatalog
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
                Codec("url", "URL 编码", "URL 查询值、路径片段的百分号编码互转。"),
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
                Formatter("xml", "XML", "XML 格式化、压缩和校验。")
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
                ImageConvert()
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
                PasswordGenerator()
            ]
        }
    ];

    public static IReadOnlyList<ToolDefinition> AllTools { get; } = Groups.SelectMany(group => group.Tools).ToList();

    public static ToolDefinition DefaultTool => AllTools[0];

    public static ToolDefinition FindTool(string? id)
    {
        return AllTools.FirstOrDefault(tool => tool.Id == id) ?? DefaultTool;
    }

    // 这些工厂方法用于复用常见的操作和参数结构，避免同类工具定义写散。
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

    private static ToolDefinition Hash(string id, string name, string description, string warning = "") => new()
    {
        Id = id,
        Name = name,
        GroupName = "加密摘要",
        Description = description,
        Warning = warning,
        Operations = [new() { Id = "hash", Name = "计算摘要" }]
    };

    private static ToolDefinition Hmac() => new()
    {
        Id = "hmacSha256",
        Name = "HMAC-SHA256",
        GroupName = "加密摘要",
        Description = "使用文本密钥计算 HMAC-SHA256。",
        Operations = [new() { Id = "sign", Name = "计算 HMAC" }],
        Parameters = [new() { Id = "key", Name = "密钥", Kind = ToolParameterKind.Password }]
    };

    private static ToolDefinition Aes() => new()
    {
        Id = "aesGcm",
        Name = "AES-GCM",
        GroupName = "加密摘要",
        Description = "使用密码加密/解密文本，输出带 salt、nonce 和 tag 的 Base64 密文。",
        Operations = [new() { Id = "encrypt", Name = "加密" }, new() { Id = "decrypt", Name = "解密" }],
        Parameters = [new() { Id = "password", Name = "密码", Kind = ToolParameterKind.Password }]
    };

    private static ToolDefinition EncodeCrypto() => new()
    {
        Id = "encodeCrypto",
        Name = "Encode",
        GroupName = "加密摘要",
        Description = "Encode 文本加解密，使用密码生成带认证信息的 Base64 密文。",
        Operations = [new() { Id = "encrypt", Name = "加密" }, new() { Id = "decrypt", Name = "解密" }],
        Parameters = [new() { Id = "password", Name = "密码", Kind = ToolParameterKind.Password }]
    };

    private static ToolDefinition Rsa() => new()
    {
        Id = "rsaOaep",
        Name = "RSA-OAEP",
        GroupName = "加密摘要",
        Description = "使用 PEM 公钥加密、PEM 私钥解密，填充为 OAEP + SHA-256。",
        Operations = [new() { Id = "encrypt", Name = "公钥加密" }, new() { Id = "decrypt", Name = "私钥解密" }],
        Parameters = [new() { Id = "key", Name = "PEM 密钥", Kind = ToolParameterKind.Multiline }]
    };

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

    private static ToolDefinition HttpRequest() => new()
    {
        Id = "httpRequest",
        Name = "HTTP 请求",
        GroupName = "网络与接口",
        Description = "发送常用 HTTP 请求并查看状态、耗时、响应头和响应体。",
        InputWatermark = "输入请求 Body；GET 请求可留空。",
        Operations = [new() { Id = "send", Name = "发送请求" }],
        Parameters =
        [
            new() { Id = "method", Name = "方法", Kind = ToolParameterKind.Combo, DefaultValue = "GET", Options = ["GET", "POST", "PUT", "PATCH", "DELETE"] },
            new() { Id = "url", Name = "URL", Kind = ToolParameterKind.Text },
            new() { Id = "headers", Name = "Headers", Kind = ToolParameterKind.Multiline }
        ]
    };

    private static ToolDefinition NetworkText(string id, string name, string description, string operationId, string operationName) => new()
    {
        Id = id,
        Name = name,
        GroupName = "网络与接口",
        Description = description,
        Operations = [new() { Id = operationId, Name = operationName }]
    };

    private static ToolDefinition Ping() => new()
    {
        Id = "ping",
        Name = "Ping",
        GroupName = "网络与接口",
        Description = "测试主机或 IP 的连通性并输出统计。",
        InputWatermark = "输入主机名或 IP，例如 8.8.8.8。",
        Operations = [new() { Id = "ping", Name = "开始 Ping" }],
        Parameters =
        [
            new() { Id = "count", Name = "次数", Kind = ToolParameterKind.Number, DefaultValue = "4" },
            new() { Id = "timeout", Name = "超时(ms)", Kind = ToolParameterKind.Number, DefaultValue = "1000" }
        ]
    };

    private static ToolDefinition HostsReset() => new()
    {
        Id = "hostsReset",
        Name = "重置 hosts",
        GroupName = "网络与接口",
        Description = "备份并重置本机 hosts 文件。需要管理员权限。",
        Warning = "危险操作：会清空自定义 hosts 映射，执行前会先生成备份。",
        InputWatermark = "无需输入；点击执行后处理 hosts 文件。",
        RequiresInput = false,
        Operations = [new() { Id = "reset", Name = "备份并重置" }],
        Parameters = [new() { Id = "confirm", Name = "确认执行", Kind = ToolParameterKind.CheckBox }]
    };

    private static ToolDefinition NetworkReset() => new()
    {
        Id = "networkReset",
        Name = "重置网络",
        GroupName = "网络与接口",
        Description = "以管理员权限运行 Windows 网络修复命令。",
        Warning = "危险操作：会释放/续租 IP、重置 Winsock/IP，完成后可能需要重启。",
        InputWatermark = "无需输入；点击执行后会弹出管理员权限确认。",
        RequiresInput = false,
        Operations = [new() { Id = "reset", Name = "执行修复命令" }],
        Parameters = [new() { Id = "confirm", Name = "确认执行", Kind = ToolParameterKind.CheckBox }]
    };

    private static ToolDefinition ProxyReset() => new()
    {
        Id = "proxyReset",
        Name = "重置代理",
        GroupName = "网络与接口",
        Description = "关闭当前用户系统代理并重置 WinHTTP 代理。",
        Warning = "危险操作：会清除当前用户代理服务器和 PAC 自动配置地址，可能影响正在使用代理的软件。",
        InputWatermark = "无需输入；点击执行后重置系统代理设置。",
        RequiresInput = false,
        Operations = [new() { Id = "reset", Name = "清除代理设置" }],
        Parameters = [new() { Id = "confirm", Name = "确认执行", Kind = ToolParameterKind.CheckBox }]
    };

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
}
