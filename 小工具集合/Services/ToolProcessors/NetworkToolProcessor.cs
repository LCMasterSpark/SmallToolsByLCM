using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32;
using 小工具集合.Models;

namespace 小工具集合.Services;

public sealed partial class ToolProcessor
{
    private static string SendHttpRequest(ToolRequest request)
    {
        string method = GetParameter(request, "method");
        string url = GetRequiredParameter(request, "url", "请输入 URL。");
        using var message = new HttpRequestMessage(new HttpMethod(method), url);
        foreach ((string key, string value) in ParseHeaderPairs(GetParameter(request, "headers")))
        {
            if (!message.Headers.TryAddWithoutValidation(key, value))
            {
                message.Content ??= new StringContent(string.Empty);
                message.Content.Headers.TryAddWithoutValidation(key, value);
            }
        }

        if (method is "POST" or "PUT" or "PATCH" or "DELETE" && !string.IsNullOrEmpty(request.Input))
        {
            message.Content = new StringContent(request.Input, Encoding.UTF8, GuessContentType(GetParameter(request, "headers")));
        }

        var watch = Stopwatch.StartNew();
        using HttpResponseMessage response = HttpClient.Send(message);
        string body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        var builder = new StringBuilder();
        builder.AppendLine($"状态：{(int)response.StatusCode} {response.ReasonPhrase}");
        builder.AppendLine($"耗时：{watch.ElapsedMilliseconds} ms");
        builder.AppendLine();
        builder.AppendLine("响应头：");
        foreach (var header in response.Headers)
        {
            builder.AppendLine($"{header.Key}: {string.Join(", ", header.Value)}");
        }

        foreach (var header in response.Content.Headers)
        {
            builder.AppendLine($"{header.Key}: {string.Join(", ", header.Value)}");
        }

        builder.AppendLine();
        builder.AppendLine("响应体：");
        builder.AppendLine(body);
        return builder.ToString();
    }

    private static string ParseUrlParameters(string input)
    {
        string query = input.Trim();
        int question = query.IndexOf('?');
        if (question >= 0)
        {
            query = query[(question + 1)..];
        }

        query = query.TrimStart('?');
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new FormatException("请输入包含查询参数的 URL 或 query string。");
        }

        var builder = new StringBuilder();
        foreach (string pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] parts = pair.Split('=', 2);
            string key = WebUtility.UrlDecode(parts[0]);
            string value = parts.Length > 1 ? WebUtility.UrlDecode(parts[1]) : string.Empty;
            builder.AppendLine($"{key} = {value}");
        }

        return builder.ToString();
    }

    private static string FormatHeaders(string input)
    {
        return string.Join(Environment.NewLine, ParseHeaderPairs(input).Select(pair => $"{pair.Key}: {pair.Value}"));
    }

    private static string FormatCookies(string input)
    {
        var builder = new StringBuilder();
        foreach (string part in input.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string[] pieces = part.Split('=', 2);
            builder.AppendLine(pieces.Length == 2 ? $"{pieces[0]} = {pieces[1]}" : pieces[0]);
        }

        return builder.ToString();
    }

    private static string PingHost(ToolRequest request)
    {
        string host = request.Input.Trim();
        if (string.IsNullOrWhiteSpace(host))
        {
            throw new InvalidOperationException("请输入主机名或 IP。");
        }

        int count = Math.Clamp(ParseOptionalInt(request, "count", 4), 1, 20);
        int timeout = Math.Clamp(ParseOptionalInt(request, "timeout", 1000), 100, 10000);
        int success = 0;
        var builder = new StringBuilder();
        using var ping = new Ping();
        for (int i = 1; i <= count; i++)
        {
            PingReply reply = ping.Send(host, timeout);
            if (reply.Status == IPStatus.Success)
            {
                success++;
                builder.AppendLine($"[{i}] 成功：{reply.Address}，{reply.RoundtripTime} ms");
            }
            else
            {
                builder.AppendLine($"[{i}] 失败：{reply.Status}");
            }
        }

        builder.AppendLine();
        builder.AppendLine($"统计：成功 {success}/{count}，丢包率 {(count - success) * 100 / count}%");
        return builder.ToString();
    }

    private static string CheckPort(ToolRequest request)
    {
        string host = request.Input.Trim();
        if (string.IsNullOrWhiteSpace(host))
        {
            throw new InvalidOperationException("请输入主机名或 IP。");
        }

        int port = Math.Clamp(ParseOptionalInt(request, "port", 443), 1, 65535);
        int count = Math.Clamp(ParseOptionalInt(request, "count", 4), 1, 20);
        int timeout = Math.Clamp(ParseOptionalInt(request, "timeout", 1000), 100, 10000);
        int success = 0;
        var builder = new StringBuilder();
        for (int i = 1; i <= count; i++)
        {
            var watch = Stopwatch.StartNew();
            try
            {
                using var client = new TcpClient();
                client.ConnectAsync(host, port).WaitAsync(TimeSpan.FromMilliseconds(timeout)).GetAwaiter().GetResult();
                success++;
                builder.AppendLine($"[{i}] 成功：{host}:{port}，{watch.ElapsedMilliseconds} ms");
            }
            catch (Exception ex) when (ex is SocketException or TimeoutException or IOException or InvalidOperationException)
            {
                builder.AppendLine($"[{i}] 失败：{TrimMessage(ex.Message)}");
            }
        }

        builder.AppendLine();
        builder.AppendLine($"统计：成功 {success}/{count}，失败率 {(count - success) * 100 / count}%");
        return builder.ToString();
    }

    private static string LookupDns(ToolRequest request)
    {
        string host = request.Input.Trim();
        if (string.IsNullOrWhiteSpace(host))
        {
            throw new InvalidOperationException("请输入域名。");
        }

        string recordType = GetParameter(request, "recordType").ToUpperInvariant();
        if (recordType is not ("A" or "AAAA" or "CNAME" or "MX" or "TXT" or "NS"))
        {
            throw new InvalidOperationException("DNS 类型仅支持 A、AAAA、CNAME、MX、TXT、NS。");
        }

        string output = RunProcess("nslookup", $"-type={recordType} {Quote(host)}", DnsLookupTimeout);
        return string.IsNullOrWhiteSpace(output) ? "查询完成，但 nslookup 未返回内容。" : output;
    }

    private static string QueryPublicIp()
    {
        (string Name, string Url)[] sources =
        [
            ("api.ipify.org", "https://api.ipify.org"),
            ("ifconfig.me/ip", "https://ifconfig.me/ip"),
            ("icanhazip.com", "https://icanhazip.com")
        ];

        var builder = new StringBuilder();
        for (int i = 0; i < sources.Length; i++)
        {
            (string name, string url) = sources[i];
            try
            {
                using var cancellation = new CancellationTokenSource(PublicIpRequestTimeout);
                string response = HttpClient.GetStringAsync(url, cancellation.Token).GetAwaiter().GetResult().Trim();
                if (!IPAddress.TryParse(response, out _))
                {
                    builder.AppendLine($"模式{i + 1}：失败（{name} 返回内容不是 IP：{TrimMessage(response)}）");
                    continue;
                }

                builder.AppendLine($"模式{i + 1}：成功（{name}） {response}");
            }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or InvalidOperationException)
            {
                builder.AppendLine($"模式{i + 1}：失败（{name}：{TrimMessage(ex.Message)}）");
            }
        }

        return builder.ToString();
    }

    private static string GenerateCurl(ToolRequest request)
    {
        string method = GetParameter(request, "method");
        string url = GetRequiredParameter(request, "url", "请输入 URL。");
        var parts = new List<string> { "curl", "-X", ShellQuote(method), ShellQuote(url) };
        foreach ((string key, string value) in ParseHeaderPairs(GetParameter(request, "headers")))
        {
            parts.Add("-H");
            parts.Add(ShellQuote($"{key}: {value}"));
        }

        if (!string.IsNullOrEmpty(request.Input))
        {
            parts.Add("--data-raw");
            parts.Add(ShellQuote(request.Input));
        }

        return string.Join(" ", parts);
    }

    private static string ShellQuote(string value)
    {
        return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n") + "\"";
    }

    private static string ResetHosts(ToolRequest request)
    {
        EnsureConfirmed(request);
        if (!IsAdministrator())
        {
            return "需要管理员权限写入 hosts 文件。请以管理员身份启动本工具后再执行。";
        }

        string hostsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers", "etc", "hosts");
        string backupPath = hostsPath + "." + DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture) + ".bak";
        File.Copy(hostsPath, backupPath, true);
        File.WriteAllText(hostsPath, DefaultHostsContent(), new UTF8Encoding(false));
        return $"hosts 已重置。备份文件：{backupPath}";
    }

    private static string ResetNetwork(ToolRequest request)
    {
        EnsureConfirmed(request);
        string commands = "ipconfig /flushdns & ipconfig /release & ipconfig /renew & netsh winsock reset & netsh int ip reset";
        if (!IsAdministrator())
        {
            var info = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/k " + Quote(commands + " & echo. & echo 网络修复命令已执行，可能需要重启电脑。"),
                Verb = "runas",
                UseShellExecute = true
            };
            Process.Start(info);
            return "已请求管理员权限执行网络修复命令。请在弹出的窗口中确认。";
        }

        return RunProcess("cmd.exe", "/c " + Quote(commands)) + Environment.NewLine + "网络修复命令已执行，可能需要重启电脑。";
    }

    private static string ResetProxy(ToolRequest request)
    {
        EnsureConfirmed(request);
        var builder = new StringBuilder();

        try
        {
            using RegistryKey internetSettings = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings", true)
                ?? throw new InvalidOperationException("无法打开当前用户代理设置注册表项。");
            internetSettings.SetValue("ProxyEnable", 0, RegistryValueKind.DWord);
            DeleteRegistryValueIfExists(internetSettings, "ProxyServer");
            DeleteRegistryValueIfExists(internetSettings, "ProxyOverride");
            DeleteRegistryValueIfExists(internetSettings, "AutoConfigURL");
            builder.AppendLine("[成功] 已关闭当前用户系统代理，并清除代理服务器/PAC 地址。");

            NotifyInternetSettingsChanged();
            builder.AppendLine("[成功] 已通知系统刷新代理设置。");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or InvalidOperationException)
        {
            builder.AppendLine($"[失败] 当前用户系统代理清理失败：{ex.Message}");
        }

        try
        {
            string output = RunProcess("netsh", "winhttp reset proxy");
            builder.AppendLine("[成功] WinHTTP 代理已重置。");
            if (!string.IsNullOrWhiteSpace(output))
            {
                builder.AppendLine(output.Trim());
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or IOException)
        {
            builder.AppendLine($"[失败] WinHTTP 代理重置失败：{ex.Message}");
        }

        builder.AppendLine();
        builder.AppendLine("说明：部分应用会维护自己的代理配置，必要时请在对应应用内单独关闭。");
        return builder.ToString();
    }

    private static string GuessContentType(string headers)
    {
        foreach ((string key, string value) in ParseHeaderPairs(headers))
        {
            if (key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        return "application/json";
    }

    private static string RunProcess(string fileName, string arguments, TimeSpan? timeout = null)
    {
        // 外部工具隐藏运行，并完整捕获输出，最后统一显示到结果文本框。
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };

        using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException($"无法启动 {fileName}。");
        if (timeout is { } processTimeout && !process.WaitForExit(processTimeout))
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
            }
            catch (Win32Exception)
            {
            }

            throw new InvalidOperationException($"{fileName} 执行超过 {processTimeout.TotalSeconds:0.#} 秒，已停止。");
        }

        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        if (timeout is null)
        {
            process.WaitForExit();
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(TrimMessage(string.IsNullOrWhiteSpace(error) ? output : error));
        }

        return string.IsNullOrWhiteSpace(output) ? error : output;
    }

    private static void EnsureConfirmed(ToolRequest request)
    {
        if (!IsTrue(GetParameter(request, "confirm")))
        {
            throw new InvalidOperationException("请先勾选“确认执行”。");
        }
    }

    private static bool IsAdministrator()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static string DefaultHostsContent()
    {
        return """
               # Copyright (c) Microsoft Corp.
               #
               # This is a sample HOSTS file used by Microsoft TCP/IP for Windows.
               #
               # 127.0.0.1       localhost
               # ::1             localhost
               """;
    }

    private static void DeleteRegistryValueIfExists(RegistryKey key, string name)
    {
        if (key.GetValue(name) is not null)
        {
            key.DeleteValue(name, false);
        }
    }

    private static void NotifyInternetSettingsChanged()
    {
        InternetSetOption(IntPtr.Zero, InternetOptionSettingsChanged, IntPtr.Zero, 0);
        InternetSetOption(IntPtr.Zero, InternetOptionRefresh, IntPtr.Zero, 0);
    }

    [DllImport("wininet.dll", SetLastError = true)]
    private static extern bool InternetSetOption(IntPtr hInternet, int dwOption, IntPtr lpBuffer, int dwBufferLength);
}
