// 文件作用：实现 OfficeHelper 第二批中 Excel/CSV、Word 合并、PPT 文本和 Office 元数据类工具。
using System.Globalization;
using System.IO;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;
using 小工具集合.Models;
using DrawingText = DocumentFormat.OpenXml.Drawing.Text;
using P = DocumentFormat.OpenXml.Presentation;
using WpDocument = DocumentFormat.OpenXml.Wordprocessing.Document;
using WpBreak = DocumentFormat.OpenXml.Wordprocessing.Break;
using WpRun = DocumentFormat.OpenXml.Wordprocessing.Run;

namespace 小工具集合.Services;

public sealed partial class ToolProcessor
{
    private static string ExportExcelToCsvBatch(ToolRequest request, ToolExecutionContext? context = null)
    {
        string[] files = GetInputFiles(request);
        string outputDirectory = EnsureOutputDirectory(request, files[0]);
        string requestedSheet = GetParameter(request, "sheetName").Trim();
        bool includeSheetName = IsTrue(GetParameter(request, "includeSheetName"));
        var builder = new StringBuilder();
        builder.AppendLine($"Excel 导出 CSV：{files.Length} 个文件");

        foreach (string file in files)
        {
            context?.WaitIfPaused();
            try
            {
                EnsureExtension(file, ".xlsx");
                List<ExcelSheetData> sheets = ReadWorkbookSheets(file).ToList();
                if (!string.IsNullOrWhiteSpace(requestedSheet))
                {
                    sheets = sheets.Where(sheet => sheet.Name.Equals(requestedSheet, StringComparison.OrdinalIgnoreCase)).ToList();
                }

                if (sheets.Count == 0)
                {
                    builder.AppendLine($"[跳过] {Path.GetFileName(file)}：未找到匹配工作表。");
                    continue;
                }

                foreach (ExcelSheetData sheet in sheets)
                {
                    string baseName = includeSheetName
                        ? $"{Path.GetFileNameWithoutExtension(file)}_{sheet.Name}"
                        : Path.GetFileNameWithoutExtension(file);
                    string outputPath = GetUniqueOutputPath(outputDirectory, baseName, ".csv");
                    WriteCsvRows(outputPath, sheet.Rows, ",");
                    builder.AppendLine($"[成功] {Path.GetFileName(file)} / {sheet.Name} -> {outputPath}");
                }
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or OpenXmlPackageException)
            {
                builder.AppendLine($"[失败] {Path.GetFileName(file)}：{TrimMessage(ex.Message)}");
            }
        }

        builder.AppendLine();
        builder.AppendLine($"输出目录：{outputDirectory}");
        return builder.ToString();
    }

    private static string ConvertCsvToExcel(ToolRequest request, ToolExecutionContext? context = null)
    {
        string[] files = GetInputFiles(request);
        string outputDirectory = EnsureOutputDirectory(request, files[0]);
        string delimiter = ResolveDelimiter(GetParameter(request, "delimiter"));
        string workbookName = GetParameter(request, "workbookName").Trim();
        if (string.IsNullOrWhiteSpace(workbookName))
        {
            workbookName = "csv_merged";
        }

        var sheets = new List<ExcelSheetData>();
        foreach (string file in files)
        {
            context?.WaitIfPaused();
            EnsureExtension(file, ".csv");
            sheets.Add(new ExcelSheetData(SanitizeSheetName(Path.GetFileNameWithoutExtension(file)), ReadCsvRows(file, delimiter)));
        }

        string outputPath = GetUniqueOutputPath(outputDirectory, workbookName, ".xlsx");
        WriteWorkbook(outputPath, sheets);
        return $"已把 {files.Length} 个 CSV 合成为 Excel。{Environment.NewLine}输出文件：{outputPath}";
    }

    private static string MergeWordDocuments(ToolRequest request, ToolExecutionContext? context = null)
    {
        string[] files = GetInputFiles(request);
        string outputDirectory = EnsureOutputDirectory(request, files[0]);
        string outputName = GetParameter(request, "outputName").Trim();
        bool insertPageBreaks = IsTrue(GetParameter(request, "insertPageBreaks"));
        if (string.IsNullOrWhiteSpace(outputName))
        {
            outputName = "word_merged";
        }

        foreach (string file in files)
        {
            EnsureExtension(file, ".docx");
        }

        string outputPath = GetUniqueOutputPath(outputDirectory, outputName, ".docx");
        File.Copy(files[0], outputPath);

        using WordprocessingDocument target = WordprocessingDocument.Open(outputPath, true);
        Body targetBody = target.MainDocumentPart?.Document?.Body
            ?? throw new InvalidOperationException("目标 Word 文档缺少正文。");
        for (int i = 1; i < files.Length; i++)
        {
            context?.WaitIfPaused();
            using WordprocessingDocument source = WordprocessingDocument.Open(files[i], false);
            Body? sourceBody = source.MainDocumentPart?.Document?.Body;
            if (sourceBody is null)
            {
                continue;
            }

            if (insertPageBreaks)
            {
                targetBody.Append(new Paragraph(new WpRun(new WpBreak { Type = BreakValues.Page })));
            }

            foreach (OpenXmlElement element in sourceBody.Elements())
            {
                targetBody.Append(element.CloneNode(deep: true));
            }
        }

        target.MainDocumentPart?.Document?.Save();
        return $"已合并 {files.Length} 个 Word 文档。{Environment.NewLine}输出文件：{outputPath}{Environment.NewLine}提示：复杂样式、页眉页脚、批注和修订不作为基础合并保证项。";
    }

    private static string ExtractPptText(ToolRequest request, ToolExecutionContext? context = null)
    {
        string[] files = GetInputFiles(request);
        string outputDirectory = EnsureOutputDirectory(request, files[0]);
        bool saveTxt = IsTrue(GetParameter(request, "saveTxt"));
        bool includeSlideHeaders = IsTrue(GetParameter(request, "includeSlideHeaders"));
        var builder = new StringBuilder();

        foreach (string file in files)
        {
            context?.WaitIfPaused();
            EnsureExtension(file, ".pptx");
            string text = ReadPptText(file, includeSlideHeaders);
            builder.AppendLine($"===== {Path.GetFileName(file)} =====");
            builder.AppendLine(text);
            builder.AppendLine();
            if (saveTxt)
            {
                string outputPath = GetUniqueOutputPath(outputDirectory, Path.GetFileNameWithoutExtension(file), ".txt");
                File.WriteAllText(outputPath, text, Encoding.UTF8);
            }
        }

        if (saveTxt)
        {
            builder.AppendLine($"txt 已保存到：{outputDirectory}");
        }

        return builder.ToString().TrimEnd();
    }

    private static string InspectOfficeMetadata(ToolRequest request, ToolExecutionContext? context = null)
    {
        string[] files = GetInputFiles(request);
        var builder = new StringBuilder();

        foreach (string file in files)
        {
            context?.WaitIfPaused();
            try
            {
                using OpenXmlPackage package = OpenOfficePackage(file, false);
                builder.AppendLine($"===== {Path.GetFileName(file)} =====");
                AppendPackageMetadata(builder, package);
                builder.AppendLine();
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or OpenXmlPackageException)
            {
                builder.AppendLine($"[失败] {Path.GetFileName(file)}：{TrimMessage(ex.Message)}");
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static void WriteWorkbook(string file, IReadOnlyList<ExcelSheetData> sheets)
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create(file, SpreadsheetDocumentType.Workbook);
        WorkbookPart workbookPart = document.AddWorkbookPart();
        workbookPart.Workbook = new Workbook();
        Sheets workbookSheets = workbookPart.Workbook.AppendChild(new Sheets());
        uint sheetId = 1;

        foreach (ExcelSheetData sheet in sheets)
        {
            WorksheetPart worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            var sheetData = new SheetData();
            worksheetPart.Worksheet = new Worksheet(sheetData);
            workbookSheets.Append(new Sheet
            {
                Id = workbookPart.GetIdOfPart(worksheetPart),
                SheetId = sheetId++,
                Name = SanitizeSheetName(sheet.Name)
            });

            foreach (string[] rowValues in sheet.Rows)
            {
                var row = new Row();
                foreach (string value in rowValues)
                {
                    row.Append(new Cell
                    {
                        DataType = CellValues.String,
                        CellValue = new CellValue(value ?? string.Empty)
                    });
                }

                sheetData.Append(row);
            }
        }
    }

    private static string ReadPptText(string file, bool includeSlideHeaders)
    {
        using PresentationDocument document = PresentationDocument.Open(file, false);
        PresentationPart presentationPart = document.PresentationPart
            ?? throw new InvalidOperationException("pptx 缺少 PresentationPart。");
        var builder = new StringBuilder();
        int index = 0;

        P.Presentation presentation = presentationPart.Presentation
            ?? throw new InvalidOperationException("pptx 缺少 Presentation。");
        P.SlideIdList? slideIdList = presentation.SlideIdList;
        foreach (P.SlideId slideId in slideIdList?.Elements<P.SlideId>() ?? [])
        {
            if (slideId.RelationshipId is null)
            {
                continue;
            }

            index++;
            SlidePart slidePart = (SlidePart)presentationPart.GetPartById(slideId.RelationshipId!);
            string[] lines = (slidePart.Slide?.Descendants<DrawingText>() ?? [])
                .Select(text => text.Text?.Trim())
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .Select(text => text!)
                .ToArray();
            if (includeSlideHeaders)
            {
                builder.AppendLine($"--- Slide {index} ---");
            }

            builder.AppendLine(string.Join(Environment.NewLine, lines));
            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    private static OpenXmlPackage OpenOfficePackage(string file, bool isEditable)
    {
        return Path.GetExtension(file).ToLowerInvariant() switch
        {
            ".docx" => WordprocessingDocument.Open(file, isEditable),
            ".xlsx" => SpreadsheetDocument.Open(file, isEditable),
            ".pptx" => PresentationDocument.Open(file, isEditable),
            _ => throw new InvalidOperationException("仅支持 docx、xlsx、pptx 元数据查看。")
        };
    }

    private static void AppendPackageMetadata(StringBuilder builder, OpenXmlPackage package)
    {
        var properties = package.PackageProperties;
        builder.AppendLine($"标题：{Empty(properties.Title)}");
        builder.AppendLine($"主题：{Empty(properties.Subject)}");
        builder.AppendLine($"作者：{Empty(properties.Creator)}");
        builder.AppendLine($"关键词：{Empty(properties.Keywords)}");
        builder.AppendLine($"说明：{Empty(properties.Description)}");
        builder.AppendLine($"创建时间：{FormatDate(properties.Created)}");
        builder.AppendLine($"修改时间：{FormatDate(properties.Modified)}");
        builder.AppendLine($"最后修改者：{Empty(properties.LastModifiedBy)}");
        builder.AppendLine($"分类：{Empty(properties.Category)}");
        builder.AppendLine($"版本：{Empty(properties.Version)}");
    }

    private static string SanitizeSheetName(string value)
    {
        char[] invalid = ['\\', '/', '?', '*', '[', ']', ':'];
        foreach (char c in invalid)
        {
            value = value.Replace(c, '_');
        }

        value = string.IsNullOrWhiteSpace(value) ? "Sheet" : value.Trim();
        return value.Length <= 31 ? value : value[..31];
    }

    private static string Empty(string? value) => string.IsNullOrWhiteSpace(value) ? "(空)" : value;

    private static string FormatDate(DateTime? value) => value?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? "(空)";
}
