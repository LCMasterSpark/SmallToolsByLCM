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

    private static ToolDefinition PortCheck() => new()
    {
        Id = "portCheck",
        Name = "端口连通测试",
        GroupName = "网络与接口",
        Description = "测试主机 TCP 端口连通性并输出耗时统计。",
        InputWatermark = "请输入主机名或 IP，例如 example.com。",
        Operations = [new() { Id = "check", Name = "开始测试" }],
        Parameters =
        [
            new() { Id = "port", Name = "端口", Kind = ToolParameterKind.Number, DefaultValue = "443" },
            new() { Id = "count", Name = "次数", Kind = ToolParameterKind.Number, DefaultValue = "4" },
            new() { Id = "timeout", Name = "超时(ms)", Kind = ToolParameterKind.Number, DefaultValue = "1000" }
        ]
    };

    private static ToolDefinition PortUsage() => new()
    {
        Id = "portUsage",
        Name = "端口占用查询",
        GroupName = "网络与接口",
        Description = "查询本机端口被哪个进程占用，并输出 PID、进程名和参考命令。",
        InputWatermark = "请输入本机端口号，例如 8080。",
        Operations = [new() { Id = "query", Name = "查询占用" }]
    };

    private static ToolDefinition DnsLookup() => new()
    {
        Id = "dnsLookup",
        Name = "DNS 查询",
        GroupName = "网络与接口",
        Description = "使用系统 DNS 查询常见记录类型。",
        InputWatermark = "请输入域名，例如 example.com。",
        Operations = [new() { Id = "lookup", Name = "查询" }],
        Parameters = [new() { Id = "recordType", Name = "类型", Kind = ToolParameterKind.Combo, DefaultValue = "A", Options = ["A", "AAAA", "CNAME", "MX", "TXT", "NS"] }]
    };

    private static ToolDefinition PublicIp() => new()
    {
        Id = "publicIp",
        Name = "公网 IP 查询",
        GroupName = "网络与接口",
        Description = "依次请求多个公开接口查询当前公网 IP。",
        InputWatermark = "无需输入；点击执行后联网查询。",
        RequiresInput = false,
        Operations = [new() { Id = "query", Name = "查询公网 IP" }]
    };

    private static ToolDefinition CurlGenerator() => new()
    {
        Id = "curlGenerator",
        Name = "生成 curl",
        GroupName = "网络与接口",
        Description = "根据方法、URL、Headers 和 Body 生成 curl 命令。",
        InputWatermark = "请输入请求 Body；无 Body 可留空。",
        Operations = [new() { Id = "generate", Name = "生成 curl" }],
        Parameters =
        [
            new() { Id = "method", Name = "方法", Kind = ToolParameterKind.Combo, DefaultValue = "GET", Options = ["GET", "POST", "PUT", "PATCH", "DELETE"] },
            new() { Id = "url", Name = "URL", Kind = ToolParameterKind.Text },
            new() { Id = "headers", Name = "Headers", Kind = ToolParameterKind.Multiline }
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

    private static ToolDefinition ChoicePicker() => new()
    {
        Id = "choicePicker",
        Name = "选择困难救星",
        GroupName = "趣味实验室",
        Description = "从多行候选中随机抽一个结果。",
        InputWatermark = "每行输入一个候选项。",
        Operations = [new() { Id = "pick", Name = "随机选择" }],
        Parameters =
        [
            new() { Id = "trimEmpty", Name = "去空行", Kind = ToolParameterKind.CheckBox, DefaultValue = "true" },
            new() { Id = "distinct", Name = "去重", Kind = ToolParameterKind.CheckBox, DefaultValue = "true" }
        ]
    };

    private static ToolDefinition ShuffleLines() => new()
    {
        Id = "shuffleLines",
        Name = "随机打乱行",
        GroupName = "趣味实验室",
        Description = "将多行文本随机重新排序。",
        InputWatermark = "每行输入一个项目。",
        Operations = [new() { Id = "shuffle", Name = "打乱" }],
        Parameters =
        [
            new() { Id = "trimEmpty", Name = "去空行", Kind = ToolParameterKind.CheckBox, DefaultValue = "true" },
            new() { Id = "distinct", Name = "去重", Kind = ToolParameterKind.CheckBox }
        ]
    };

    private static ToolDefinition RandomNumber() => new()
    {
        Id = "randomNumber",
        Name = "随机数字",
        GroupName = "趣味实验室",
        Description = "生成指定范围内的随机整数。",
        InputWatermark = "无需输入；请在参数区设置范围。",
        RequiresInput = false,
        Operations = [new() { Id = "generate", Name = "生成" }],
        Parameters =
        [
            new() { Id = "min", Name = "最小值", Kind = ToolParameterKind.Number, DefaultValue = "1" },
            new() { Id = "max", Name = "最大值", Kind = ToolParameterKind.Number, DefaultValue = "100" },
            new() { Id = "count", Name = "数量", Kind = ToolParameterKind.Number, DefaultValue = "1" },
            new() { Id = "unique", Name = "不重复", Kind = ToolParameterKind.CheckBox }
        ]
    };

    private static ToolDefinition DiceRoller() => new()
    {
        Id = "diceRoller",
        Name = "骰子工具",
        GroupName = "趣味实验室",
        Description = "掷骰表达式，例如 1d6、2d20+3。",
        InputWatermark = "请输入骰子表达式，例如 2d6+1。",
        Operations = [new() { Id = "roll", Name = "开骰" }]
    };

    private static ToolDefinition ReverseText() => new()
    {
        Id = "reverseText",
        Name = "倒放文本",
        GroupName = "趣味实验室",
        Description = "将文本整体、逐行或行序倒放。",
        InputWatermark = "请输入要倒放的文本。",
        Operations = [new() { Id = "reverse", Name = "倒放" }],
        Parameters = [new() { Id = "mode", Name = "模式", Kind = ToolParameterKind.Combo, DefaultValue = "全文倒放", Options = ["全文倒放", "逐行倒放", "行序倒放"] }]
    };

    private static ToolDefinition EmojiWrap() => new()
    {
        Id = "emojiWrap",
        Name = "Emoji 装饰",
        GroupName = "趣味实验室",
        Description = "给每行文本加一点随机 Emoji 气氛。",
        InputWatermark = "请输入要装饰的文本。",
        Operations = [new() { Id = "wrap", Name = "装饰" }],
        Parameters =
        [
            new() { Id = "style", Name = "风格", Kind = ToolParameterKind.Combo, DefaultValue = "随机", Options = ["随机", "开心", "星星", "办公"] },
            new() { Id = "bothSides", Name = "两侧装饰", Kind = ToolParameterKind.CheckBox, DefaultValue = "true" },
            new() { Id = "enhancement", Name = "模式", Kind = ToolParameterKind.Combo, DefaultValue = "本地", Options = ["本地", "联网增强"] }
        ]
    };

    private static ToolDefinition MockingText() => new()
    {
        Id = "mockingText",
        Name = "阴阳怪气转换",
        GroupName = "趣味实验室",
        Description = "把文本转换成大小写交替或带波浪的语气。",
        InputWatermark = "请输入要整活的文本。",
        Operations = [new() { Id = "mock", Name = "转换" }],
        Parameters = [new() { Id = "mode", Name = "模式", Kind = ToolParameterKind.Combo, DefaultValue = "交替大小写", Options = ["交替大小写", "随机大小写", "加波浪"] }]
    };

    private static ToolDefinition ZalgoText() => new()
    {
        Id = "zalgoText",
        Name = "故障文本",
        GroupName = "趣味实验室",
        Description = "给文本增加轻量故障效果。",
        InputWatermark = "请输入要故障化的文本。",
        Operations = [new() { Id = "glitch", Name = "故障化" }],
        Parameters = [new() { Id = "strength", Name = "强度", Kind = ToolParameterKind.Combo, DefaultValue = "低", Options = ["低", "中", "高"] }]
    };

    private static ToolDefinition CommitMessage() => new()
    {
        Id = "commitMessage",
        Name = "Commit 文案",
        GroupName = "趣味实验室",
        Description = "根据改动摘要生成规则化 commit 文案候选。",
        InputWatermark = "请输入改动摘要，例如：新增 URL 拆解和端口查询。",
        Operations = [new() { Id = "generate", Name = "生成文案" }],
        Parameters =
        [
            new() { Id = "language", Name = "语言", Kind = ToolParameterKind.Combo, DefaultValue = "中文", Options = ["中文", "English"] },
            new() { Id = "type", Name = "类型", Kind = ToolParameterKind.Combo, DefaultValue = "自动", Options = ["自动", "feat", "fix", "refactor", "docs", "chore"] },
            new() { Id = "enhancement", Name = "模式", Kind = ToolParameterKind.Combo, DefaultValue = "本地", Options = ["本地", "联网增强"] }
        ]
    };

    private static ToolDefinition VariableName() => new()
    {
        Id = "variableName",
        Name = "变量名灵感",
        GroupName = "趣味实验室",
        Description = "从短语生成多种命名风格候选。",
        InputWatermark = "请输入中文、英文或混合短语。",
        Operations = [new() { Id = "generate", Name = "生成变量名" }],
        Parameters = [new() { Id = "enhancement", Name = "模式", Kind = ToolParameterKind.Combo, DefaultValue = "本地", Options = ["本地", "联网增强"] }]
    };

    private static ToolDefinition FakeLog() => new()
    {
        Id = "fakeLog",
        Name = "假日志生成器",
        GroupName = "趣味实验室",
        Description = "生成本地离线的假日志/假错误。",
        InputWatermark = "可选：输入日志主题；留空则随机生成。",
        Operations = [new() { Id = "generate", Name = "生成日志" }],
        Parameters =
        [
            new() { Id = "level", Name = "级别", Kind = ToolParameterKind.Combo, DefaultValue = "混合", Options = ["混合", "INFO", "WARN", "ERROR", "DEBUG"] },
            new() { Id = "count", Name = "行数", Kind = ToolParameterKind.Number, DefaultValue = "8" },
            new() { Id = "service", Name = "服务名", Kind = ToolParameterKind.Text, DefaultValue = "fun-lab" },
            new() { Id = "enhancement", Name = "模式", Kind = ToolParameterKind.Combo, DefaultValue = "本地", Options = ["本地", "联网增强"] }
        ]
    };

    private static ToolDefinition ExcuseGenerator() => new()
    {
        Id = "excuseGenerator",
        Name = "程序员借口",
        GroupName = "趣味实验室",
        Description = "生成一个完全离线、只供娱乐的程序员借口。",
        InputWatermark = "无需输入；想指定场景也可以写一句。",
        Operations = [new() { Id = "generate", Name = "生成借口" }],
        Parameters = [new() { Id = "enhancement", Name = "模式", Kind = ToolParameterKind.Combo, DefaultValue = "本地", Options = ["本地", "联网增强"] }]
    };

    private static ToolDefinition OnlineHitokoto() => new()
    {
        Id = "onlineHitokoto",
        Name = "随机一言",
        GroupName = "趣味实验室",
        Description = "从 Hitokoto 获取一句随机短句，并标注来源。",
        InputWatermark = "无需输入；请在参数区选择分类。",
        RequiresInput = false,
        Operations = [new() { Id = "query", Name = "来一句" }],
        Parameters =
        [
            new()
            {
                Id = "category",
                Name = "分类",
                Kind = ToolParameterKind.Combo,
                DefaultValue = "随机",
                Options = ["随机", "动画", "漫画", "游戏", "文学", "原创", "网络", "影视", "诗词", "哲学", "抖机灵"]
            }
        ]
    };

    private static ToolDefinition OnlinePoemLine() => new()
    {
        Id = "onlinePoemLine",
        Name = "诗词一句",
        GroupName = "趣味实验室",
        Description = "从免 Key 接口获取一句诗词分类短句。",
        InputWatermark = "无需输入；点击执行后联网获取。",
        RequiresInput = false,
        Operations = [new() { Id = "query", Name = "取一句" }]
    };

    private static ToolDefinition WeatherCard() => new()
    {
        Id = "weatherCard",
        Name = "天气小卡",
        GroupName = "趣味实验室",
        Description = "使用 Open-Meteo 查询城市或经纬度的当前天气。",
        InputWatermark = "输入城市名或纬度,经度，例如：北京 或 39.9,116.4。",
        Operations = [new() { Id = "query", Name = "查询天气" }]
    };

    private static ToolDefinition IpInfoCard() => new()
    {
        Id = "ipInfoCard",
        Name = "IP 信息小卡",
        GroupName = "趣味实验室",
        Description = "查询当前出口 IP、指定 IP 或域名的地理和运营商信息。",
        InputWatermark = "可留空查询当前出口 IP；也可输入 IP 或域名，例如 8.8.8.8。",
        RequiresInput = false,
        Operations = [new() { Id = "query", Name = "查询 IP" }]
    };

    private static ToolDefinition PowerChecker() => new()
    {
        Id = "powerChecker",
        Name = "电量检测器",
        GroupName = "趣味实验室",
        Description = "使用 LCMasterSpark 高精度存在性判定引擎检测这台电脑是否有电。",
        RequiresInput = false,
        Operations = [new() { Id = "interactive", Name = "互动" }],
        InteractiveViewKey = "powerChecker"
    };

    private static ToolDefinition TimePointer() => new()
    {
        Id = "timePointer",
        Name = "时间显示器",
        GroupName = "趣味实验室",
        Description = "启动高精度时间感知分析，并用红色箭头指向任务栏时间。",
        RequiresInput = false,
        Operations = [new() { Id = "interactive", Name = "互动" }],
        InteractiveViewKey = "timePointer"
    };

    private static ToolDefinition Minesweeper() => new()
    {
        Id = "minesweeper",
        Name = "扫雷",
        GroupName = "趣味实验室",
        Description = "内嵌版扫雷小游戏，保留普通模式、街机模式和本次运行内战绩。",
        RequiresInput = false,
        Operations = [new() { Id = "interactive", Name = "互动" }],
        InteractiveViewKey = "minesweeper"
    };
}
