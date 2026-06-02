using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using 小工具集合.Models;

namespace 小工具集合.Services;

public interface IToolProcessor
{
    ToolResult Execute(ToolRequest request);
    Task<ToolResult> ExecuteAsync(ToolRequest request, ToolExecutionContext context);
}

/// <summary>
/// 批处理工具使用的协作式暂停钩子。
/// 它只会在文件之间暂停，不会中断正在处理的当前文件或进程。
/// </summary>
public sealed class ToolExecutionContext(Func<bool> isPaused)
{
    public void WaitIfPaused()
    {
        while (isPaused())
        {
            Thread.Sleep(200);
        }
    }
}

/// <summary>
/// 执行工具请求。文本和网络工具按单次操作执行，
/// 文件类工具通过 ExecuteAsync 执行，以便在批处理间隙检查暂停状态。
/// </summary>
public sealed class ToolProcessor : IToolProcessor
{
    // AES-GCM 载荷格式：前缀 + salt + nonce + tag + 密文。
    // 前缀用于区分文本加密和文件加密格式。
    private const int AesSaltSize = 16;
    private const int AesNonceSize = 12;
    private const int AesTagSize = 16;
    private const int AesKeySize = 32;
    private const int Pbkdf2Iterations = 100_000;
    private const string AesPackagePrefix = "AESG";
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(60) };

    public ToolResult Execute(ToolRequest request)
    {
        try
        {
            // ToolCatalog 负责可见的工具元数据；这里把元数据 Id
            // 映射到运行时真正执行的实现。
            string output = request.ToolId switch
            {
                "base64" => Base64(request),
                "url" => request.OperationId == "encode" ? WebUtility.UrlEncode(request.Input) : WebUtility.UrlDecode(request.Input),
                "html" => request.OperationId == "encode" ? WebUtility.HtmlEncode(request.Input) : WebUtility.HtmlDecode(request.Input),
                "unicode" => UnicodeEscape(request),
                "json" => Json(request),
                "xml" => Xml(request),
                "sha256" => Hash(request.Input, SHA256.HashData),
                "sha512" => Hash(request.Input, SHA512.HashData),
                "md5" => Hash(request.Input, MD5.HashData),
                "hmacSha256" => HmacSha256(request),
                "aesGcm" => request.OperationId == "encrypt" ? EncryptAes(request) : DecryptAes(request),
                "encodeCrypto" => request.OperationId == "encrypt" ? EncryptAes(request, "ENCD") : DecryptAes(request, "ENCD"),
                "rsaOaep" => request.OperationId == "encrypt" ? EncryptRsa(request) : DecryptRsa(request),
                "fileEncode" => EncodeFiles(request),
                "mp4ToMp3" => ExtractMp3Batch(request),
                "imageConvert" => ConvertImages(request),
                "httpRequest" => SendHttpRequest(request),
                "urlParams" => ParseUrlParameters(request.Input),
                "headerFormat" => FormatHeaders(request.Input),
                "cookieFormat" => FormatCookies(request.Input),
                "ping" => PingHost(request),
                "hostsReset" => ResetHosts(request),
                "networkReset" => ResetNetwork(request),
                "uuid" => Guid.NewGuid().ToString("D"),
                "timestamp" => Timestamp(request),
                "passwordGenerator" => GeneratePasswords(request),
                _ => throw new NotSupportedException("暂不支持该工具。")
            };

            return ToolResult.Ok(output ?? string.Empty);
        }
        catch (Exception ex) when (ex is FormatException or JsonException or XmlException or CryptographicException or InvalidOperationException or ArgumentException or IOException or Win32Exception or HttpRequestException)
        {
            return ToolResult.Fail(ex.Message);
        }
    }

    public Task<ToolResult> ExecuteAsync(ToolRequest request, ToolExecutionContext context)
    {
        // 大多数工具都是短小的 CPU/字符串操作，可以复用同步执行路径。
        // 批量文件工具单独分支处理，便于在队列文件之间暂停。
        if (request.ToolId is not ("fileEncode" or "mp4ToMp3" or "imageConvert"))
        {
            return Task.Run(() => Execute(request));
        }

        return Task.Run(() =>
        {
            try
            {
                string output = request.ToolId switch
                {
                    "fileEncode" => EncodeFiles(request, context),
                    "mp4ToMp3" => ExtractMp3Batch(request, context),
                    "imageConvert" => ConvertImages(request, context),
                    _ => throw new NotSupportedException("暂不支持该工具。")
                };

                return ToolResult.Ok(output);
            }
            catch (Exception ex) when (ex is FormatException or CryptographicException or InvalidOperationException or ArgumentException or IOException or Win32Exception)
            {
                return ToolResult.Fail(ex.Message);
            }
        });
    }

    private static string Base64(ToolRequest request)
    {
        return request.OperationId == "encode"
            ? Convert.ToBase64String(Encoding.UTF8.GetBytes(request.Input))
            : Encoding.UTF8.GetString(Convert.FromBase64String(request.Input.Trim()));
    }

    private static string UnicodeEscape(ToolRequest request)
    {
        if (request.OperationId == "encode")
        {
            var builder = new StringBuilder();
            foreach (char value in request.Input)
            {
                builder.Append(value <= 0x7F ? value : FormattableString.Invariant($"\\u{(int)value:x4}"));
            }

            return builder.ToString();
        }

        return Regex.Replace(request.Input, @"\\u([0-9a-fA-F]{4})", match =>
        {
            int code = int.Parse(match.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return ((char)code).ToString();
        });
    }

    private static string Json(ToolRequest request)
    {
        using JsonDocument document = JsonDocument.Parse(request.Input);
        return JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions { WriteIndented = request.OperationId == "format" });
    }

    private static string Xml(ToolRequest request)
    {
        var document = new XmlDocument { PreserveWhitespace = false };
        document.LoadXml(request.Input);
        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            Indent = request.OperationId == "format",
            OmitXmlDeclaration = document.FirstChild is not XmlDeclaration
        };

        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        using XmlWriter xmlWriter = XmlWriter.Create(writer, settings);
        document.Save(xmlWriter);
        xmlWriter.Flush();
        return writer.ToString();
    }

    private static string Hash(string input, Func<byte[], byte[]> hashData)
    {
        return Convert.ToHexString(hashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();
    }

    private static string HmacSha256(ToolRequest request)
    {
        string key = GetRequiredParameter(request, "key", "请输入 HMAC 密钥。");
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(request.Input))).ToLowerInvariant();
    }

    private static string EncryptAes(ToolRequest request, string packagePrefix = AesPackagePrefix)
    {
        string password = GetRequiredParameter(request, "password", "请输入密码。");
        byte[] salt = RandomNumberGenerator.GetBytes(AesSaltSize);
        byte[] nonce = RandomNumberGenerator.GetBytes(AesNonceSize);
        byte[] key = DeriveAesKey(password, salt);
        byte[] plainBytes = Encoding.UTF8.GetBytes(request.Input);
        byte[] cipherBytes = new byte[plainBytes.Length];
        byte[] tag = new byte[AesTagSize];

        try
        {
            // 随机 salt 用于 PBKDF2 派生密钥，nonce 保证每次加密唯一。
            // 二者都不是秘密，因此会和密文一起存储。
            using var aes = new AesGcm(key, AesTagSize);
            aes.Encrypt(nonce, plainBytes, cipherBytes, tag);
            byte[] package = new byte[packagePrefix.Length + salt.Length + nonce.Length + tag.Length + cipherBytes.Length];
            Encoding.ASCII.GetBytes(packagePrefix).CopyTo(package, 0);
            salt.CopyTo(package, packagePrefix.Length);
            nonce.CopyTo(package, packagePrefix.Length + salt.Length);
            tag.CopyTo(package, packagePrefix.Length + salt.Length + nonce.Length);
            cipherBytes.CopyTo(package, packagePrefix.Length + salt.Length + nonce.Length + tag.Length);
            return Convert.ToBase64String(package);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    private static string DecryptAes(ToolRequest request, string packagePrefix = AesPackagePrefix)
    {
        string password = GetRequiredParameter(request, "password", "请输入密码。");
        byte[] package = Convert.FromBase64String(request.Input.Trim());
        int headerSize = packagePrefix.Length + AesSaltSize + AesNonceSize + AesTagSize;
        if (package.Length <= headerSize || Encoding.ASCII.GetString(package, 0, packagePrefix.Length) != packagePrefix)
        {
            throw new FormatException("密文格式不正确。请使用本工具生成的密文。");
        }

        // 固定头部长度与 EncryptAes/EncryptBytes 保持一致，便于直接切片读取。
        byte[] salt = package[packagePrefix.Length..(packagePrefix.Length + AesSaltSize)];
        byte[] nonce = package[(packagePrefix.Length + AesSaltSize)..(packagePrefix.Length + AesSaltSize + AesNonceSize)];
        byte[] tag = package[(packagePrefix.Length + AesSaltSize + AesNonceSize)..headerSize];
        byte[] cipherBytes = package[headerSize..];
        byte[] plainBytes = new byte[cipherBytes.Length];
        byte[] key = DeriveAesKey(password, salt);

        try
        {
            using var aes = new AesGcm(key, AesTagSize);
            aes.Decrypt(nonce, cipherBytes, tag, plainBytes);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (CryptographicException ex)
        {
            throw new CryptographicException("AES 解密失败：密码错误或密文已损坏。", ex);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    private static byte[] DeriveAesKey(string password, byte[] salt)
    {
        return Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, Pbkdf2Iterations, HashAlgorithmName.SHA256, AesKeySize);
    }

    private static byte[] EncryptBytes(byte[] plainBytes, string password, string packagePrefix)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(AesSaltSize);
        byte[] nonce = RandomNumberGenerator.GetBytes(AesNonceSize);
        byte[] key = DeriveAesKey(password, salt);
        byte[] cipherBytes = new byte[plainBytes.Length];
        byte[] tag = new byte[AesTagSize];

        try
        {
            using var aes = new AesGcm(key, AesTagSize);
            aes.Encrypt(nonce, plainBytes, cipherBytes, tag);
            byte[] package = new byte[packagePrefix.Length + salt.Length + nonce.Length + tag.Length + cipherBytes.Length];
            Encoding.ASCII.GetBytes(packagePrefix).CopyTo(package, 0);
            salt.CopyTo(package, packagePrefix.Length);
            nonce.CopyTo(package, packagePrefix.Length + salt.Length);
            tag.CopyTo(package, packagePrefix.Length + salt.Length + nonce.Length);
            cipherBytes.CopyTo(package, packagePrefix.Length + salt.Length + nonce.Length + tag.Length);
            return package;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    private static byte[] DecryptBytes(byte[] package, string password, string packagePrefix)
    {
        int headerSize = packagePrefix.Length + AesSaltSize + AesNonceSize + AesTagSize;
        if (package.Length <= headerSize || Encoding.ASCII.GetString(package, 0, packagePrefix.Length) != packagePrefix)
        {
            throw new FormatException("文件密文格式不正确。请使用本工具生成的 Encode 文件密文。");
        }

        byte[] salt = package[packagePrefix.Length..(packagePrefix.Length + AesSaltSize)];
        byte[] nonce = package[(packagePrefix.Length + AesSaltSize)..(packagePrefix.Length + AesSaltSize + AesNonceSize)];
        byte[] tag = package[(packagePrefix.Length + AesSaltSize + AesNonceSize)..headerSize];
        byte[] cipherBytes = package[headerSize..];
        byte[] plainBytes = new byte[cipherBytes.Length];
        byte[] key = DeriveAesKey(password, salt);

        try
        {
            using var aes = new AesGcm(key, AesTagSize);
            aes.Decrypt(nonce, cipherBytes, tag, plainBytes);
            return plainBytes;
        }
        catch (CryptographicException ex)
        {
            throw new CryptographicException("文件解密失败：密码错误或文件已损坏。", ex);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    private static string EncryptRsa(ToolRequest request)
    {
        string pem = GetRequiredParameter(request, "key", "请输入 PEM 公钥。");
        using var rsa = RSA.Create();
        rsa.ImportFromPem(pem);
        return Convert.ToBase64String(rsa.Encrypt(Encoding.UTF8.GetBytes(request.Input), RSAEncryptionPadding.OaepSHA256));
    }

    private static string DecryptRsa(ToolRequest request)
    {
        string pem = GetRequiredParameter(request, "key", "请输入 PEM 私钥。");
        using var rsa = RSA.Create();
        rsa.ImportFromPem(pem);
        return Encoding.UTF8.GetString(rsa.Decrypt(Convert.FromBase64String(request.Input.Trim()), RSAEncryptionPadding.OaepSHA256));
    }

    private static string ExtractMp3Batch(ToolRequest request, ToolExecutionContext? context = null)
    {
        string[] files = GetInputFiles(request);
        string outputDirectory = EnsureOutputDirectory(request, files[0]);
        string quality = GetParameter(request, "quality");
        string qualityArg = quality switch
        {
            "高质量" => "0",
            "小体积" => "5",
            _ => "2"
        };

        var report = new StringBuilder();
        report.AppendLine($"MP4 提取 MP3：{files.Length} 个文件");
        foreach (string file in files)
        {
            // 在开始每个文件前检查暂停，避免 ffmpeg 正在输出时被中途打断。
            context?.WaitIfPaused();
            string outputPath = Path.Combine(outputDirectory, Path.GetFileNameWithoutExtension(file) + ".mp3");
            var watch = Stopwatch.StartNew();
            try
            {
                RunProcess("ffmpeg", $"-y -i {Quote(file)} -vn -codec:a libmp3lame -q:a {qualityArg} {Quote(outputPath)}");
                report.AppendLine($"[成功] {Path.GetFileName(file)} -> {outputPath} ({watch.ElapsedMilliseconds} ms)");
            }
            catch (Win32Exception ex)
            {
                throw new InvalidOperationException("未找到 ffmpeg。请先安装 ffmpeg，并确保 ffmpeg.exe 已加入 PATH。", ex);
            }
            catch (Exception ex) when (ex is InvalidOperationException or IOException)
            {
                report.AppendLine($"[失败] {Path.GetFileName(file)}：{ex.Message}");
            }
        }

        return report.ToString();
    }

    private static string EncodeFiles(ToolRequest request, ToolExecutionContext? context = null)
    {
        string[] files = GetInputFiles(request);
        string outputDirectory = EnsureOutputDirectory(request, files[0]);
        string password = GetRequiredParameter(request, "password", "请输入密码。");
        bool overwrite = IsTrue(GetParameter(request, "overwrite"));
        bool encrypt = request.OperationId == "encrypt";

        var report = new StringBuilder();
        report.AppendLine($"Encode 文件{(encrypt ? "加密" : "解密")}：{files.Length} 个文件");
        foreach (string file in files)
        {
            // 每个文件独立处理：单个文件失败只记录到报告中，不影响后续队列。
            context?.WaitIfPaused();
            string outputName = encrypt
                ? Path.GetFileName(file) + ".enc"
                : Path.GetFileName(file).EndsWith(".enc", StringComparison.OrdinalIgnoreCase)
                    ? Path.GetFileNameWithoutExtension(file)
                    : Path.GetFileName(file) + ".dec";
            string outputPath = Path.Combine(outputDirectory, outputName);

            try
            {
                if (File.Exists(outputPath) && !overwrite)
                {
                    report.AppendLine($"[跳过] {Path.GetFileName(file)}：输出文件已存在。");
                    continue;
                }

                byte[] input = File.ReadAllBytes(file);
                byte[] output = encrypt ? EncryptBytes(input, password, "ENCF") : DecryptBytes(input, password, "ENCF");
                File.WriteAllBytes(outputPath, output);
                report.AppendLine($"[成功] {Path.GetFileName(file)} -> {outputPath}");
            }
            catch (Exception ex) when (ex is IOException or CryptographicException or FormatException or InvalidOperationException)
            {
                report.AppendLine($"[失败] {Path.GetFileName(file)}：{ex.Message}");
            }
        }

        return report.ToString();
    }

    private static string ConvertImages(ToolRequest request, ToolExecutionContext? context = null)
    {
        string[] files = GetInputFiles(request);
        string outputDirectory = EnsureOutputDirectory(request, files[0]);
        string targetFormat = GetRequiredParameter(request, "targetFormat", "请选择目标格式。").ToLowerInvariant();
        string rawPixelFormat = GetParameter(request, "rawPixelFormat");
        bool overwrite = IsTrue(GetParameter(request, "overwrite"));
        int rawWidth = ParseOptionalInt(request, "rawWidth");
        int rawHeight = ParseOptionalInt(request, "rawHeight");

        var report = new StringBuilder();
        report.AppendLine($"图片转换：{files.Length} 个文件 -> {targetFormat}");
        foreach (string file in files)
        {
            context?.WaitIfPaused();
            string outputPath = Path.Combine(outputDirectory, Path.GetFileNameWithoutExtension(file) + "." + targetFormat);
            try
            {
                if (File.Exists(outputPath) && !overwrite)
                {
                    report.AppendLine($"[跳过] {Path.GetFileName(file)}：输出文件已存在。");
                    continue;
                }

                ConvertOneImage(file, outputPath, targetFormat, rawPixelFormat, rawWidth, rawHeight);
                report.AppendLine($"[成功] {Path.GetFileName(file)} -> {outputPath}");
            }
            catch (Exception ex) when (ex is InvalidOperationException or IOException or ArgumentException)
            {
                report.AppendLine($"[失败] {Path.GetFileName(file)}：{ex.Message}");
            }
        }

        return report.ToString();
    }

    private static void ConvertOneImage(string inputPath, string outputPath, string targetFormat, string rawPixelFormat, int rawWidth, int rawHeight)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? Environment.CurrentDirectory);
        if (Path.GetExtension(inputPath).Equals(".raw", StringComparison.OrdinalIgnoreCase))
        {
            // RAW 文件没有内嵌尺寸和像素格式，调用方必须提供足够信息来还原图像缓冲区。
            if (rawWidth <= 0 || rawHeight <= 0)
            {
                throw new InvalidOperationException("RAW 导入需要填写宽和高。");
            }

            if (rawPixelFormat == "RGB24")
            {
                byte[] data = File.ReadAllBytes(inputPath);
                using Image<Rgb24> image = Image.LoadPixelData<Rgb24>(data, rawWidth, rawHeight);
                SaveImage(image, outputPath, targetFormat);
                return;
            }

            byte[] rgba = File.ReadAllBytes(inputPath);
            using Image<Rgba32> rgbaImage = Image.LoadPixelData<Rgba32>(rgba, rawWidth, rawHeight);
            SaveImage(rgbaImage, outputPath, targetFormat);
            return;
        }

        using Image imageFile = Image.Load(inputPath);
        if (targetFormat == "raw")
        {
            if (rawPixelFormat == "RGB24")
            {
                using Image<Rgb24> rgb = imageFile.CloneAs<Rgb24>();
                byte[] data = new byte[rgb.Width * rgb.Height * 3];
                rgb.CopyPixelDataTo(data);
                File.WriteAllBytes(outputPath, data);
                return;
            }

            using Image<Rgba32> rgba = imageFile.CloneAs<Rgba32>();
            byte[] rgbaData = new byte[rgba.Width * rgba.Height * 4];
            rgba.CopyPixelDataTo(rgbaData);
            File.WriteAllBytes(outputPath, rgbaData);
            return;
        }

        SaveImage(imageFile, outputPath, targetFormat);
    }

    private static void SaveImage(Image image, string outputPath, string targetFormat)
    {
        switch (targetFormat)
        {
            case "jpg":
            case "jpeg":
                image.Save(outputPath, new JpegEncoder { Quality = 90 });
                break;
            case "png":
                image.Save(outputPath, new PngEncoder());
                break;
            case "webp":
                image.Save(outputPath, new WebpEncoder { Quality = 90 });
                break;
            default:
                throw new InvalidOperationException("目标格式仅支持 jpg、png、webp、raw。");
        }
    }

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

    private static string Timestamp(ToolRequest request)
    {
        if (request.OperationId == "now")
        {
            DateTimeOffset now = DateTimeOffset.Now;
            return $"本地时间：{now:yyyy-MM-dd HH:mm:ss zzz}{Environment.NewLine}Unix 秒：{now.ToUnixTimeSeconds()}{Environment.NewLine}Unix 毫秒：{now.ToUnixTimeMilliseconds()}";
        }

        if (request.OperationId == "toDate")
        {
            string input = request.Input.Trim();
            if (!long.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out long unix))
            {
                throw new FormatException("请输入 Unix 时间戳数字。");
            }

            DateTimeOffset value = input.Length > 10 ? DateTimeOffset.FromUnixTimeMilliseconds(unix) : DateTimeOffset.FromUnixTimeSeconds(unix);
            return value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture);
        }

        if (!DateTimeOffset.TryParse(request.Input.Trim(), CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out DateTimeOffset dateTime))
        {
            throw new FormatException("请输入可识别的日期时间，例如 2026-05-31 22:30:00。");
        }

        return $"Unix 秒：{dateTime.ToUnixTimeSeconds()}{Environment.NewLine}Unix 毫秒：{dateTime.ToUnixTimeMilliseconds()}";
    }

    private static string GeneratePasswords(ToolRequest request)
    {
        const int count = 5;
        int length = ParseOptionalInt(request, "length", 16);
        if (length is < 4 or > 128)
        {
            throw new InvalidOperationException("密码长度必须在 4 到 128 位之间。");
        }

        string strength = GetParameter(request, "strength");
        bool excludeAmbiguous = IsTrue(GetParameter(request, "excludeAmbiguous"));
        var builder = new StringBuilder();

        if (strength == "弱密码")
        {
            builder.AppendLine("弱密码仅适合低安全场景。");
            builder.AppendLine();
            for (int i = 0; i < count; i++)
            {
                builder.AppendLine(GenerateReadablePassword(length, excludeAmbiguous));
            }

            return builder.ToString();
        }

        for (int i = 0; i < count; i++)
        {
            builder.AppendLine(GenerateStrongPassword(length, excludeAmbiguous));
        }

        return builder.ToString();
    }

    private static string GenerateStrongPassword(int length, bool excludeAmbiguous)
    {
        string lower = ApplyAmbiguousPolicy("abcdefghijklmnopqrstuvwxyz", excludeAmbiguous);
        string upper = ApplyAmbiguousPolicy("ABCDEFGHIJKLMNOPQRSTUVWXYZ", excludeAmbiguous);
        string digits = ApplyAmbiguousPolicy("0123456789", excludeAmbiguous);
        const string symbols = "!@#$%^&*()-_=+[]{};:,.?/";
        string all = lower + upper + digits + symbols;

        if (length < 4)
        {
            throw new InvalidOperationException("强密码长度至少需要 4 位。");
        }

        var chars = new List<char>
        {
            Pick(lower),
            Pick(upper),
            Pick(digits),
            Pick(symbols)
        };

        while (chars.Count < length)
        {
            chars.Add(Pick(all));
        }

        Shuffle(chars);
        return new string(chars.ToArray());
    }

    private static string GenerateReadablePassword(int length, bool excludeAmbiguous)
    {
        string consonants = ApplyAmbiguousPolicy("bcdfghjkmnpqrstvwxyz", excludeAmbiguous);
        string vowels = ApplyAmbiguousPolicy("aeu", excludeAmbiguous);
        string digits = ApplyAmbiguousPolicy("23456789", excludeAmbiguous);
        var builder = new StringBuilder(length);

        while (builder.Length < length)
        {
            string syllable = string.Create(CultureInfo.InvariantCulture, $"{Pick(consonants)}{Pick(vowels)}");
            foreach (char value in syllable)
            {
                if (builder.Length < length)
                {
                    builder.Append(value);
                }
            }

            if (builder.Length < length && builder.Length % 5 == 4)
            {
                builder.Append(Pick(digits));
            }
        }

        if (length >= 6 && !builder.ToString().Any(char.IsDigit))
        {
            int index = RandomNumberGenerator.GetInt32(1, length);
            builder[index] = Pick(digits);
        }

        return builder.ToString();
    }

    private static string ApplyAmbiguousPolicy(string characters, bool excludeAmbiguous)
    {
        const string ambiguous = "il1IoO0";
        return excludeAmbiguous
            ? new string(characters.Where(character => !ambiguous.Contains(character, StringComparison.Ordinal)).ToArray())
            : characters;
    }

    private static char Pick(string characters)
    {
        if (string.IsNullOrEmpty(characters))
        {
            throw new InvalidOperationException("密码字符集为空，请调整参数后重试。");
        }

        return characters[RandomNumberGenerator.GetInt32(characters.Length)];
    }

    private static void Shuffle(IList<char> values)
    {
        for (int i = values.Count - 1; i > 0; i--)
        {
            int j = RandomNumberGenerator.GetInt32(i + 1);
            (values[i], values[j]) = (values[j], values[i]);
        }
    }

    private static string[] GetInputFiles(ToolRequest request)
    {
        string raw = GetParameter(request, "inputFiles");
        string[] files = raw.Split(["\r\n", "\n", ";"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(path => path.Trim('"'))
            .Where(File.Exists)
            .ToArray();
        if (files.Length == 0)
        {
            throw new InvalidOperationException("请先添加至少一个有效文件。");
        }

        return files;
    }

    private static string EnsureOutputDirectory(ToolRequest request, string firstInputFile)
    {
        string outputDirectory = GetParameter(request, "outputDirectory").Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            outputDirectory = Path.GetDirectoryName(firstInputFile) ?? Environment.CurrentDirectory;
        }

        Directory.CreateDirectory(outputDirectory);
        return outputDirectory;
    }

    private static IEnumerable<(string Key, string Value)> ParseHeaderPairs(string headers)
    {
        foreach (string line in headers.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int index = line.IndexOf(':');
            if (index <= 0)
            {
                continue;
            }

            yield return (line[..index].Trim(), line[(index + 1)..].Trim());
        }
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

    private static string RunProcess(string fileName, string arguments)
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
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
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

    private static string GetRequiredParameter(ToolRequest request, string id, string message)
    {
        if (!request.Parameters.TryGetValue(id, out string? value) || string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(message);
        }

        return value;
    }

    private static string GetParameter(ToolRequest request, string id)
    {
        // 缺失的可选参数统一视为空字符串，让具体工具实现保持简洁。
        return request.Parameters.TryGetValue(id, out string? value) ? value : string.Empty;
    }

    private static int ParseOptionalInt(ToolRequest request, string id, int defaultValue = 0)
    {
        string value = GetParameter(request, id);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result) ? result : defaultValue;
    }

    private static bool IsTrue(string value)
    {
        return value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1" || value == "是";
    }

    private static string Quote(string value)
    {
        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }

    private static string TrimMessage(string message)
    {
        message = message.Trim();
        return message.Length <= 1200 ? message : message[^1200..];
    }
}
