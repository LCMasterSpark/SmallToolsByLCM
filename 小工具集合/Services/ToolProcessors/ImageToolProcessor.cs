// 文件作用：实现图片格式转换和压缩处理。
using System.IO;
using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using 小工具集合.Models;

namespace 小工具集合.Services;

public sealed partial class ToolProcessor
{
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

    private static string CompressImages(ToolRequest request, ToolExecutionContext? context = null)
    {
        string[] files = GetInputFiles(request);
        string outputDirectory = EnsureOutputDirectory(request, files[0]);
        int quality = Math.Clamp(ParseOptionalInt(request, "quality", 85), 1, 100);
        int maxWidth = Math.Max(0, ParseOptionalInt(request, "maxWidth"));
        int maxHeight = Math.Max(0, ParseOptionalInt(request, "maxHeight"));
        string targetFormat = GetParameter(request, "targetFormat");
        bool overwrite = IsTrue(GetParameter(request, "overwrite"));

        var report = new StringBuilder();
        report.AppendLine($"图片压缩：{files.Length} 个文件，质量 {quality}");
        foreach (string file in files)
        {
            context?.WaitIfPaused();
            try
            {
                string format = ResolveImageOutputFormat(file, targetFormat);
                string outputPath = Path.Combine(outputDirectory, Path.GetFileNameWithoutExtension(file) + "_compressed." + format);
                if (File.Exists(outputPath) && !overwrite)
                {
                    report.AppendLine($"[跳过] {Path.GetFileName(file)}：输出文件已存在。");
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? Environment.CurrentDirectory);
                using Image image = Image.Load(file);
                ResizeImageIfNeeded(image, maxWidth, maxHeight);
                SaveCompressedImage(image, outputPath, format, quality);
                report.AppendLine($"[成功] {Path.GetFileName(file)} -> {outputPath} ({image.Width}x{image.Height})");
            }
            catch (Exception ex) when (ex is InvalidOperationException or IOException or ArgumentException or NotSupportedException)
            {
                report.AppendLine($"[失败] {Path.GetFileName(file)}：{ex.Message}");
            }
        }

        return report.ToString();
    }

    private static string ResolveImageOutputFormat(string inputPath, string targetFormat)
    {
        string format = targetFormat == "保持原格式"
            ? Path.GetExtension(inputPath).TrimStart('.').ToLowerInvariant()
            : targetFormat.ToLowerInvariant();
        return format switch
        {
            "jpeg" => "jpg",
            "jpg" or "png" or "webp" => format,
            _ => throw new NotSupportedException("图片压缩仅支持 jpg、png、webp。")
        };
    }

    private static void ResizeImageIfNeeded(Image image, int maxWidth, int maxHeight)
    {
        if (maxWidth <= 0 && maxHeight <= 0)
        {
            return;
        }

        double widthRatio = maxWidth > 0 ? (double)maxWidth / image.Width : double.PositiveInfinity;
        double heightRatio = maxHeight > 0 ? (double)maxHeight / image.Height : double.PositiveInfinity;
        double ratio = Math.Min(1, Math.Min(widthRatio, heightRatio));
        if (ratio >= 1)
        {
            return;
        }

        int width = Math.Max(1, (int)Math.Round(image.Width * ratio));
        int height = Math.Max(1, (int)Math.Round(image.Height * ratio));
        image.Mutate(context => context.Resize(new ResizeOptions
        {
            Size = new Size(width, height),
            Mode = ResizeMode.Max,
            Sampler = KnownResamplers.Lanczos3
        }));
    }

    private static void SaveCompressedImage(Image image, string outputPath, string format, int quality)
    {
        switch (format)
        {
            case "jpg":
                image.Save(outputPath, new JpegEncoder { Quality = quality });
                break;
            case "png":
                image.Save(outputPath, new PngEncoder());
                break;
            case "webp":
                image.Save(outputPath, new WebpEncoder { Quality = quality });
                break;
            default:
                throw new NotSupportedException("图片压缩仅支持 jpg、png、webp。");
        }
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
}
