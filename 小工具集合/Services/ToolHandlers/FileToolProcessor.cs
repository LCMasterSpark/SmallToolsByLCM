// 文件作用：实现文件加密、媒体转换、文件哈希等批处理工具。
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using 小工具集合.Models;

namespace 小工具集合.Services;

public sealed partial class ToolProcessor
{
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

    private static string HashFiles(ToolRequest request, ToolExecutionContext? context = null)
    {
        string[] files = GetInputFiles(request);
        string algorithm = GetParameter(request, "algorithm");
        var report = new StringBuilder();
        report.AppendLine($"文件哈希：{files.Length} 个文件，算法 {algorithm}");
        foreach (string file in files)
        {
            context?.WaitIfPaused();
            try
            {
                using FileStream stream = File.OpenRead(file);
                byte[] hash = algorithm switch
                {
                    "MD5" => MD5.HashData(stream),
                    "SHA-512" => SHA512.HashData(stream),
                    _ => SHA256.HashData(stream)
                };
                report.AppendLine($"[成功] {Path.GetFileName(file)}");
                report.AppendLine(Convert.ToHexString(hash).ToLowerInvariant());
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                report.AppendLine($"[失败] {Path.GetFileName(file)}：{ex.Message}");
            }
        }

        return report.ToString();
    }
}
