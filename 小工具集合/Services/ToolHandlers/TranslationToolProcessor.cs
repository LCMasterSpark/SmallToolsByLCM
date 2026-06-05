// 文件作用：实现文本翻译和文件翻译工具，复用统一翻译服务并安全输出翻译副本。
using System.IO;
using System.Net.Http;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using 小工具集合.Models;
using 小工具集合.Services.Translation;
using AText = DocumentFormat.OpenXml.Drawing.Text;
using WpText = DocumentFormat.OpenXml.Wordprocessing.Text;

namespace 小工具集合.Services;

public sealed partial class ToolProcessor
{
    private static string TranslateText(ToolRequest request)
    {
        TranslationSettings settings = BuildTranslationSettings(request);
        string provider = GetParameterOrDefault(request, "provider", settings.DefaultProvider);
        string source = GetParameterOrDefault(request, "sourceLanguage", settings.DefaultSourceLanguage);
        string target = GetParameterOrDefault(request, "targetLanguage", settings.DefaultTargetLanguage);
        bool useGlossary = IsTrue(GetParameterOrDefault(request, "useGlossary", settings.UseGlossary ? "true" : "false"));
        settings.UseGlossary = useGlossary;

        TranslationResponse response = new TranslationService()
            .TranslateAsync(new TranslationRequest(request.Input, source, target, provider, settings))
            .GetAwaiter()
            .GetResult();

        return response.Text
            + Environment.NewLine
            + Environment.NewLine
            + $"来源：{response.Provider}"
            + (string.IsNullOrWhiteSpace(response.Detail) ? string.Empty : Environment.NewLine + response.Detail);
    }

    private static string TranslateFiles(ToolRequest request, ToolExecutionContext? context = null)
    {
        string[] files = GetInputFiles(request);
        string outputDirectory = EnsureOutputDirectory(request, files[0]);
        TranslationSettings settings = BuildTranslationSettings(request);
        string provider = GetParameterOrDefault(request, "provider", settings.DefaultProvider);
        string source = GetParameterOrDefault(request, "sourceLanguage", settings.DefaultSourceLanguage);
        string target = GetParameterOrDefault(request, "targetLanguage", settings.DefaultTargetLanguage);
        settings.UseGlossary = IsTrue(GetParameterOrDefault(request, "useGlossary", settings.UseGlossary ? "true" : "false"));
        var translator = new TranslationService();
        var builder = new StringBuilder();
        int success = 0;
        int failure = 0;

        builder.AppendLine("文件翻译结果：");
        // 文件翻译是批处理热点：单个文件失败只计入结果，不中断后续文件。
        // 这样可以配合任务队列的失败项重试，而不是让一次接口错误毁掉整批。
        foreach (string file in files)
        {
            context?.WaitIfPaused();
            try
            {
                string outputPath = TranslateFile(file, outputDirectory, translator, provider, source, target, settings, context);
                success++;
                builder.AppendLine($"{Path.GetFileName(file)} -> {Path.GetFileName(outputPath)}");
            }
            catch (Exception ex) when (ex is InvalidOperationException or IOException or CsvHelperException or OpenXmlPackageException or HttpRequestException or TaskCanceledException)
            {
                failure++;
                builder.AppendLine($"{Path.GetFileName(file)}：失败 - {TrimMessage(ex.Message)}");
            }
        }

        builder.AppendLine();
        builder.AppendLine($"成功：{success}，失败：{failure}");
        builder.AppendLine($"输出目录：{outputDirectory}");
        return builder.ToString();
    }

    private static string TranslateFile(
        string file,
        string outputDirectory,
        TranslationService translator,
        string provider,
        string source,
        string target,
        TranslationSettings settings,
        ToolExecutionContext? context)
    {
        // 所有输出都走唯一文件名，避免覆盖源文件或上一次翻译结果。
        string extension = Path.GetExtension(file).ToLowerInvariant();
        string outputPath = GetUniqueOutputPath(outputDirectory, Path.GetFileNameWithoutExtension(file) + "_translated", extension);

        switch (extension)
        {
            case ".txt":
            case ".md":
            case ".json":
            case ".xml":
            case ".html":
            case ".htm":
                string text = File.ReadAllText(file, Encoding.UTF8);
                File.WriteAllText(outputPath, TranslateChunk(translator, text, provider, source, target, settings), Encoding.UTF8);
                return outputPath;
            case ".csv":
                TranslateCsv(file, outputPath, translator, provider, source, target, settings, context);
                return outputPath;
            case ".docx":
                File.Copy(file, outputPath);
                TranslateDocx(outputPath, translator, provider, source, target, settings, context);
                return outputPath;
            case ".pptx":
                File.Copy(file, outputPath);
                TranslatePptx(outputPath, translator, provider, source, target, settings, context);
                return outputPath;
            case ".xlsx":
                File.Copy(file, outputPath);
                TranslateXlsx(outputPath, translator, provider, source, target, settings, context);
                return outputPath;
            case ".pdf":
                throw new InvalidOperationException("PDF 翻译暂不在本期支持，请先用 PDF 文本提取或 PDF 转 Word Lite。");
            default:
                throw new InvalidOperationException("不支持的文件类型：" + extension);
        }
    }

    private static void TranslateCsv(
        string input,
        string output,
        TranslationService translator,
        string provider,
        string source,
        string target,
        TranslationSettings settings,
        ToolExecutionContext? context)
    {
        using var reader = new StreamReader(input, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        using var csvReader = new CsvReader(reader, new CsvConfiguration(System.Globalization.CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = false,
            BadDataFound = null,
            MissingFieldFound = null
        });
        using var writer = new StreamWriter(output, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        using var csvWriter = new CsvWriter(writer, System.Globalization.CultureInfo.InvariantCulture);
        while (csvReader.Read())
        {
            context?.WaitIfPaused();
            foreach (string? field in csvReader.Parser.Record ?? [])
            {
                csvWriter.WriteField(string.IsNullOrWhiteSpace(field)
                    ? field ?? string.Empty
                    : TranslateChunk(translator, field, provider, source, target, settings));
            }

            csvWriter.NextRecord();
        }
    }

    private static void TranslateDocx(
        string file,
        TranslationService translator,
        string provider,
        string source,
        string target,
        TranslationSettings settings,
        ToolExecutionContext? context)
    {
        // OpenXML 的文本常被拆成多个 run；这里保留原文档结构，只替换文本节点。
        // 这不是高保真排版重建器，重点是“可编辑副本 + 不破坏源文件”。
        using WordprocessingDocument document = WordprocessingDocument.Open(file, true);
        foreach (WpText text in document.MainDocumentPart?.Document?.Descendants<WpText>() ?? [])
        {
            context?.WaitIfPaused();
            if (!string.IsNullOrWhiteSpace(text.Text))
            {
                text.Text = TranslateChunk(translator, text.Text, provider, source, target, settings);
            }
        }

        document.MainDocumentPart?.Document?.Save();
    }

    private static void TranslatePptx(
        string file,
        TranslationService translator,
        string provider,
        string source,
        string target,
        TranslationSettings settings,
        ToolExecutionContext? context)
    {
        using PresentationDocument document = PresentationDocument.Open(file, true);
        foreach (SlidePart slidePart in document.PresentationPart?.SlideParts ?? [])
        {
            if (slidePart.Slide is null)
            {
                continue;
            }

            foreach (AText text in slidePart.Slide.Descendants<AText>())
            {
                context?.WaitIfPaused();
                if (!string.IsNullOrWhiteSpace(text.Text))
                {
                    text.Text = TranslateChunk(translator, text.Text, provider, source, target, settings);
                }
            }

            slidePart.Slide.Save();
        }
    }

    private static void TranslateXlsx(
        string file,
        TranslationService translator,
        string provider,
        string source,
        string target,
        TranslationSettings settings,
        ToolExecutionContext? context)
    {
        // xlsx 文本可能在 shared strings，也可能是 inline string；两条路径都要扫。
        using SpreadsheetDocument document = SpreadsheetDocument.Open(file, true);
        SharedStringTable? sharedStrings = document.WorkbookPart?.SharedStringTablePart?.SharedStringTable;
        if (sharedStrings is not null)
        {
            foreach (SharedStringItem item in sharedStrings.Elements<SharedStringItem>())
            {
                context?.WaitIfPaused();
                foreach (DocumentFormat.OpenXml.Spreadsheet.Text text in item.Descendants<DocumentFormat.OpenXml.Spreadsheet.Text>())
                {
                    if (!string.IsNullOrWhiteSpace(text.Text))
                    {
                        text.Text = TranslateChunk(translator, text.Text, provider, source, target, settings);
                    }
                }
            }

            sharedStrings.Save();
        }

        foreach (WorksheetPart worksheetPart in document.WorkbookPart?.WorksheetParts ?? [])
        {
            if (worksheetPart.Worksheet is null)
            {
                continue;
            }

            foreach (Cell cell in worksheetPart.Worksheet.Descendants<Cell>())
            {
                context?.WaitIfPaused();
                if (cell.DataType?.Value == CellValues.InlineString)
                {
                    foreach (DocumentFormat.OpenXml.Spreadsheet.Text text in cell.InlineString?.Descendants<DocumentFormat.OpenXml.Spreadsheet.Text>() ?? [])
                    {
                        if (!string.IsNullOrWhiteSpace(text.Text))
                        {
                            text.Text = TranslateChunk(translator, text.Text, provider, source, target, settings);
                        }
                    }
                }
                else if (cell.DataType?.Value == CellValues.String && cell.CellValue is not null && !string.IsNullOrWhiteSpace(cell.CellValue.Text))
                {
                    cell.CellValue.Text = TranslateChunk(translator, cell.CellValue.Text, provider, source, target, settings);
                }
            }

            worksheetPart.Worksheet.Save();
        }
    }

    private static string TranslateChunk(TranslationService translator, string text, string provider, string source, string target, TranslationSettings settings)
    {
        string normalized = text.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return text;
        }

        return translator.TranslateAsync(new TranslationRequest(text, source, target, provider, settings))
            .GetAwaiter()
            .GetResult()
            .Text;
    }

    private static TranslationSettings BuildTranslationSettings(ToolRequest request)
    {
        // 配置合流点：ViewModel 会把设置页里的翻译配置注入 request.Parameters。
        // ToolProcessor 仍从 app-state 读取默认值，保证测试和直接调用也能工作。
        TranslationSettings settings = new TranslationSettingsService().Load();
        ApplyString(request, "translationDefaultProvider", value => settings.DefaultProvider = value);
        ApplyString(request, "libreTranslateEndpoint", value => settings.LibreTranslateEndpoint = value);
        ApplyString(request, "libreTranslateApiKey", value => settings.LibreTranslateApiKey = value);
        ApplyString(request, "azureEndpoint", value => settings.AzureEndpoint = value);
        ApplyString(request, "azureRegion", value => settings.AzureRegion = value);
        ApplyString(request, "azureKey", value => settings.AzureKey = value);
        ApplyString(request, "deepLApiUrl", value => settings.DeepLApiUrl = value);
        ApplyString(request, "deepLApiKey", value => settings.DeepLApiKey = value);
        ApplyString(request, "googleApiKey", value => settings.GoogleApiKey = value);
        ApplyString(request, "baiduAppId", value => settings.BaiduAppId = value);
        ApplyString(request, "baiduSecret", value => settings.BaiduSecret = value);
        ApplyString(request, "youdaoAppKey", value => settings.YoudaoAppKey = value);
        ApplyString(request, "youdaoAppSecret", value => settings.YoudaoAppSecret = value);
        ApplyString(request, "openAiBaseUrl", value => settings.OpenAiBaseUrl = value);
        ApplyString(request, "openAiApiKey", value => settings.OpenAiApiKey = value);
        ApplyString(request, "openAiModel", value => settings.OpenAiModel = value);
        ApplyString(request, "ollamaEndpoint", value => settings.OllamaEndpoint = value);
        ApplyString(request, "ollamaModel", value => settings.OllamaModel = value);
        ApplyString(request, "translationGlossary", value => settings.Glossary = value);
        if (request.Parameters.TryGetValue("translationUseGlossary", out string? globalGlossary))
        {
            settings.UseGlossary = IsTrue(globalGlossary);
        }

        return settings;
    }

    private static void ApplyString(ToolRequest request, string key, Action<string> apply)
    {
        if (request.Parameters.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value))
        {
            apply(value);
        }
    }
}
