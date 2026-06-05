// 文件作用：实现 OfficeHelper 中 PDF 信息、文本提取、图片提取和 Lite Word 导出工具。
using System.Globalization;
using System.IO;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using 小工具集合.Models;
using WpDocument = DocumentFormat.OpenXml.Wordprocessing.Document;

namespace 小工具集合.Services;

public sealed partial class ToolProcessor
{
    private static string InspectPdfInfo(ToolRequest request, ToolExecutionContext? context = null)
    {
        string[] files = GetInputFiles(request);
        var builder = new StringBuilder();

        foreach (string file in files)
        {
            context?.WaitIfPaused();
            try
            {
                EnsureExtension(file, ".pdf");
                using PdfDocument document = PdfDocument.Open(file);
                builder.AppendLine($"===== {Path.GetFileName(file)} =====");
                builder.AppendLine($"版本：{document.Version.ToString(CultureInfo.InvariantCulture)}");
                builder.AppendLine($"页数：{document.NumberOfPages}");
                builder.AppendLine($"加密：{(document.IsEncrypted ? "是" : "否")}");
                AppendPdfMetadata(builder, document);
                builder.AppendLine("页面尺寸：");
                foreach (Page page in document.GetPages())
                {
                    builder.AppendLine($"  第 {page.Number} 页：{page.Width:0.##} x {page.Height:0.##} pt");
                }

                builder.AppendLine();
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException)
            {
                builder.AppendLine($"[失败] {Path.GetFileName(file)}：{TrimMessage(ex.Message)}");
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static string ExtractPdfText(ToolRequest request, ToolExecutionContext? context = null)
    {
        string[] files = GetInputFiles(request);
        string outputDirectory = EnsureOutputDirectory(request, files[0]);
        bool saveTxt = IsTrue(GetParameter(request, "saveTxt"));
        bool includePageHeaders = IsTrue(GetParameter(request, "includePageHeaders"));
        var builder = new StringBuilder();

        foreach (string file in files)
        {
            context?.WaitIfPaused();
            try
            {
                EnsureExtension(file, ".pdf");
                string text = ReadPdfText(file, includePageHeaders);
                builder.AppendLine($"===== {Path.GetFileName(file)} =====");
                builder.AppendLine(string.IsNullOrWhiteSpace(text) ? "(未提取到文本，可能是扫描件或图片型 PDF。)" : text);
                builder.AppendLine();
                if (saveTxt)
                {
                    string outputPath = GetUniqueOutputPath(outputDirectory, Path.GetFileNameWithoutExtension(file), ".txt");
                    File.WriteAllText(outputPath, text, Encoding.UTF8);
                }
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException)
            {
                builder.AppendLine($"[失败] {Path.GetFileName(file)}：{TrimMessage(ex.Message)}");
            }
        }

        if (saveTxt)
        {
            builder.AppendLine($"txt 已保存到：{outputDirectory}");
        }

        return builder.ToString().TrimEnd();
    }

    private static string ExtractPdfImages(ToolRequest request, ToolExecutionContext? context = null)
    {
        string[] files = GetInputFiles(request);
        string outputDirectory = EnsureOutputDirectory(request, files[0]);
        var builder = new StringBuilder();
        int total = 0;

        foreach (string file in files)
        {
            context?.WaitIfPaused();
            try
            {
                EnsureExtension(file, ".pdf");
                int count = ExtractPdfImagesFromFile(file, outputDirectory);
                total += count;
                builder.AppendLine($"{Path.GetFileName(file)}：提取 {count} 张图片");
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException)
            {
                builder.AppendLine($"[失败] {Path.GetFileName(file)}：{TrimMessage(ex.Message)}");
            }
        }

        builder.AppendLine();
        builder.AppendLine($"总计：{total} 张");
        builder.AppendLine($"输出目录：{outputDirectory}");
        return builder.ToString();
    }

    private static string ConvertPdfToWordLite(ToolRequest request, ToolExecutionContext? context = null)
    {
        string[] files = GetInputFiles(request);
        string outputDirectory = EnsureOutputDirectory(request, files[0]);
        bool includePageBreaks = IsTrue(GetParameter(request, "includePageBreaks"));
        var builder = new StringBuilder();
        builder.AppendLine("PDF 转 Word Lite：");

        foreach (string file in files)
        {
            context?.WaitIfPaused();
            try
            {
                EnsureExtension(file, ".pdf");
                IReadOnlyList<PdfPageText> pages = ReadPdfPages(file);
                if (pages.All(page => string.IsNullOrWhiteSpace(page.Text)))
                {
                    builder.AppendLine($"[跳过] {Path.GetFileName(file)}：未提取到文本，可能是扫描件或图片型 PDF。");
                    continue;
                }

                string outputPath = GetUniqueOutputPath(outputDirectory, Path.GetFileNameWithoutExtension(file) + "_lite", ".docx");
                WritePdfTextDocx(outputPath, Path.GetFileName(file), pages, includePageBreaks);
                builder.AppendLine($"[成功] {Path.GetFileName(file)} -> {outputPath}");
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException)
            {
                builder.AppendLine($"[失败] {Path.GetFileName(file)}：{TrimMessage(ex.Message)}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("提示：Lite 版只生成可编辑文字，不保证原 PDF 排版。");
        return builder.ToString();
    }

    private static string ReadPdfText(string file, bool includePageHeaders)
    {
        IReadOnlyList<PdfPageText> pages = ReadPdfPages(file);
        var builder = new StringBuilder();
        foreach (PdfPageText page in pages)
        {
            if (includePageHeaders)
            {
                builder.AppendLine($"--- Page {page.PageNumber} ---");
            }

            builder.AppendLine(page.Text);
            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    private static IReadOnlyList<PdfPageText> ReadPdfPages(string file)
    {
        using PdfDocument document = PdfDocument.Open(file);
        return document.GetPages()
            .Select(page => new PdfPageText(page.Number, ContentOrderTextExtractor.GetText(page).Trim()))
            .ToList();
    }

    private static int ExtractPdfImagesFromFile(string file, string outputDirectory)
    {
        using PdfDocument document = PdfDocument.Open(file);
        int count = 0;
        string baseName = SanitizeFileName(Path.GetFileNameWithoutExtension(file));
        foreach (Page page in document.GetPages())
        {
            foreach (IPdfImage image in page.GetImages())
            {
                count++;
                byte[] bytes;
                string extension;
                if (image.TryGetPng(out byte[]? pngBytes))
                {
                    bytes = pngBytes;
                    extension = ".png";
                }
                else
                {
                    bytes = image.RawBytes.ToArray();
                    extension = ".bin";
                }

                string outputPath = GetUniqueOutputPath(outputDirectory, $"{baseName}_page_{page.Number:000}_image_{count:000}", extension);
                File.WriteAllBytes(outputPath, bytes);
            }
        }

        return count;
    }

    private static void WritePdfTextDocx(string file, string title, IReadOnlyList<PdfPageText> pages, bool includePageBreaks)
    {
        using WordprocessingDocument document = WordprocessingDocument.Create(file, WordprocessingDocumentType.Document);
        MainDocumentPart mainPart = document.AddMainDocumentPart();
        var body = new Body();
        body.Append(new Paragraph(new Run(new Text(title))));
        body.Append(new Paragraph(new Run(new Text("PDF 转 Word Lite：可编辑文字版，不保证原排版。"))));

        for (int i = 0; i < pages.Count; i++)
        {
            PdfPageText page = pages[i];
            if (includePageBreaks && i > 0)
            {
                body.Append(new Paragraph(new Run(new Break { Type = BreakValues.Page })));
            }

            body.Append(new Paragraph(new Run(new Text($"Page {page.PageNumber}"))));
            foreach (string line in page.Text.Split(["\r\n", "\n"], StringSplitOptions.None))
            {
                body.Append(new Paragraph(new Run(new Text(line) { Space = SpaceProcessingModeValues.Preserve })));
            }
        }

        mainPart.Document = new WpDocument(body);
        mainPart.Document.Save();
    }

    private static void AppendPdfMetadata(StringBuilder builder, PdfDocument document)
    {
        DocumentInformation info = document.Information;
        builder.AppendLine("元数据：");
        builder.AppendLine($"  标题：{Empty(info.Title)}");
        builder.AppendLine($"  作者：{Empty(info.Author)}");
        builder.AppendLine($"  主题：{Empty(info.Subject)}");
        builder.AppendLine($"  关键词：{Empty(info.Keywords)}");
        builder.AppendLine($"  创建工具：{Empty(info.Creator)}");
        builder.AppendLine($"  生成器：{Empty(info.Producer)}");
        builder.AppendLine($"  创建时间：{Empty(info.CreationDate)}");
        builder.AppendLine($"  修改时间：{Empty(info.ModifiedDate)}");
    }

    private sealed record PdfPageText(int PageNumber, string Text);
}
