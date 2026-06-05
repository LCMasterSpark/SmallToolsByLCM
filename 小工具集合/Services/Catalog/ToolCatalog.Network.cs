// 文件作用：注册网络与接口分组的工具元数据。
using 小工具集合.Models;

namespace 小工具集合.Services;

public static partial class ToolCatalog
{
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
}
