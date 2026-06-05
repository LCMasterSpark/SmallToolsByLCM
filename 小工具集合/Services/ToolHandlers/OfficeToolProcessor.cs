// 文件作用：实现 OfficeHelper 中 CSV、Excel、Word 和 Office 图片提取类工具。
using System.Globalization;
using System.IO;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;
using 小工具集合.Models;
using WpText = DocumentFormat.OpenXml.Wordprocessing.Text;

namespace 小工具集合.Services;

public sealed partial class ToolProcessor
{
    private static string CleanCsvFiles(ToolRequest request, ToolExecutionContext? context = null)
    {
        string[] files = GetInputFiles(request);
        string outputDirectory = EnsureOutputDirectory(request, files[0]);
        string delimiter = ResolveDelimiter(GetParameter(request, "delimiter"));
        bool trimCells = IsTrue(GetParameter(request, "trimCells"));
        bool removeEmptyRows = IsTrue(GetParameter(request, "removeEmptyRows"));
        bool distinctRows = IsTrue(GetParameter(request, "distinctRows"));

        var builder = new StringBuilder();
        builder.AppendLine("CSV 清洗结果：");
        foreach (string file in files)
        {
            context?.WaitIfPaused();
            List<string[]> rows = ReadCsvRows(file, delimiter);
            int originalRows = rows.Count;
            if (trimCells)
            {
                rows = rows.Select(row => row.Select(cell => cell.Trim()).ToArray()).ToList();
            }

            if (removeEmptyRows)
            {
                rows = rows.Where(row => row.Any(cell => !string.IsNullOrWhiteSpace(cell))).ToList();
            }

            if (distinctRows)
            {
                rows = rows.DistinctBy(row => string.Join('\u001F', row), StringComparer.Ordinal).ToList();
            }

            string outputPath = GetUniqueOutputPath(outputDirectory, Path.GetFileNameWithoutExtension(file) + "_cleaned", ".csv");
            WriteCsvRows(outputPath, rows, ",");
            builder.AppendLine($"{Path.GetFileName(file)} -> {Path.GetFileName(outputPath)}，{originalRows} 行 -> {rows.Count} 行");
        }

        builder.AppendLine();
        builder.AppendLine($"输出目录：{outputDirectory}");
        return builder.ToString();
    }

    private static string MergeExcelSheets(ToolRequest request, ToolExecutionContext? context = null)
    {
        string[] files = GetInputFiles(request);
        string outputDirectory = EnsureOutputDirectory(request, files[0]);
        bool includeSourceColumns = IsTrue(GetParameter(request, "includeSourceColumns"));
        bool outputXlsx = GetParameter(request, "outputFormat").Equals("XLSX", StringComparison.OrdinalIgnoreCase);
        var rows = new List<string[]>();
        int sheetCount = 0;

        foreach (string file in files)
        {
            context?.WaitIfPaused();
            EnsureExtension(file, ".xlsx");
            foreach (ExcelSheetData sheet in ReadWorkbookSheets(file))
            {
                sheetCount++;
                foreach (string[] row in sheet.Rows)
                {
                    rows.Add(includeSourceColumns
                        ? [Path.GetFileName(file), sheet.Name, .. row]
                        : row);
                }
            }
        }

        if (includeSourceColumns)
        {
            rows.Insert(0, ["来源文件", "来源工作表"]);
        }

        string outputPath = outputXlsx
            ? GetUniqueOutputPath(outputDirectory, "excel_merged", ".xlsx")
            : GetUniqueOutputPath(outputDirectory, "excel_merged", ".csv");
        if (outputXlsx)
        {
            WriteWorkbook(outputPath, rows);
        }
        else
        {
            WriteCsvRows(outputPath, rows, ",");
        }

        return $"已合并 {files.Length} 个文件、{sheetCount} 个工作表、{rows.Count} 行。{Environment.NewLine}输出文件：{outputPath}";
    }

    private static string ExtractWordText(ToolRequest request, ToolExecutionContext? context = null)
    {
        string[] files = GetInputFiles(request);
        string outputDirectory = EnsureOutputDirectory(request, files[0]);
        bool saveTxt = IsTrue(GetParameter(request, "saveTxt"));
        bool includeFileHeaders = IsTrue(GetParameter(request, "includeFileHeaders"));
        var builder = new StringBuilder();

        foreach (string file in files)
        {
            context?.WaitIfPaused();
            EnsureExtension(file, ".docx");
            string text = ReadWordText(file);
            if (includeFileHeaders)
            {
                builder.AppendLine($"===== {Path.GetFileName(file)} =====");
            }

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

    private static string ExtractOfficeImages(ToolRequest request, ToolExecutionContext? context = null)
    {
        string[] files = GetInputFiles(request);
        string outputDirectory = EnsureOutputDirectory(request, files[0]);
        var builder = new StringBuilder();
        int total = 0;

        foreach (string file in files)
        {
            context?.WaitIfPaused();
            int count = ExtractImagesFromOfficeFile(file, outputDirectory);
            total += count;
            builder.AppendLine($"{Path.GetFileName(file)}：提取 {count} 张图片");
        }

        builder.AppendLine();
        builder.AppendLine($"总计：{total} 张");
        builder.AppendLine($"输出目录：{outputDirectory}");
        return builder.ToString();
    }

    private static string ReplaceWordTextBatch(ToolRequest request, ToolExecutionContext? context = null)
    {
        string[] files = GetInputFiles(request);
        string outputDirectory = EnsureOutputDirectory(request, files[0]);
        IReadOnlyList<TextReplacement> replacements = ParseTextReplacements(GetParameter(request, "replacements"));
        StringComparison comparison = IsTrue(GetParameter(request, "caseSensitive"))
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;
        var builder = new StringBuilder();
        builder.AppendLine("Word 批量替换结果：");

        foreach (string file in files)
        {
            context?.WaitIfPaused();
            EnsureExtension(file, ".docx");
            string outputPath = GetUniqueOutputPath(outputDirectory, Path.GetFileNameWithoutExtension(file) + "_replaced", ".docx");
            File.Copy(file, outputPath);
            int count = ReplaceWordTextInFile(outputPath, replacements, comparison);
            builder.AppendLine($"{Path.GetFileName(file)} -> {Path.GetFileName(outputPath)}，替换 {count} 处");
        }

        builder.AppendLine();
        builder.AppendLine("提示：Word 会把不同格式的文字拆成多个片段，跨片段文本可能无法替换。");
        builder.AppendLine($"输出目录：{outputDirectory}");
        return builder.ToString();
    }

    private static List<string[]> ReadCsvRows(string file, string delimiter)
    {
        using var reader = new StreamReader(file, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = delimiter,
            HasHeaderRecord = false,
            BadDataFound = null,
            MissingFieldFound = null
        });
        var rows = new List<string[]>();
        while (csv.Read())
        {
            rows.Add(csv.Parser.Record?.ToArray() ?? []);
        }

        return rows;
    }

    private static void WriteCsvRows(string file, IEnumerable<IReadOnlyList<string>> rows, string delimiter)
    {
        using var writer = new StreamWriter(file, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = delimiter
        });
        foreach (IReadOnlyList<string> row in rows)
        {
            foreach (string cell in row)
            {
                csv.WriteField(cell);
            }

            csv.NextRecord();
        }
    }

    private static IEnumerable<ExcelSheetData> ReadWorkbookSheets(string file)
    {
        try
        {
            using SpreadsheetDocument document = SpreadsheetDocument.Open(file, false);
            WorkbookPart workbookPart = document.WorkbookPart ?? throw new InvalidOperationException("xlsx 缺少 WorkbookPart。");
            SharedStringTable? sharedStrings = workbookPart.SharedStringTablePart?.SharedStringTable;
            Workbook workbook = workbookPart.Workbook ?? throw new InvalidOperationException("xlsx 缺少 Workbook。");
            Sheets? sheets = workbook.Sheets;
            if (sheets is null)
            {
                yield break;
            }

            foreach (Sheet sheet in sheets.OfType<Sheet>())
            {
                if (sheet.Id is null)
                {
                    continue;
                }

                WorksheetPart worksheetPart = (WorksheetPart)workbookPart.GetPartById(sheet.Id!);
                List<string[]> rows = ReadWorksheetRows(worksheetPart, sharedStrings);
                yield return new ExcelSheetData(sheet.Name?.Value ?? "Sheet", rows);
            }
        }
        finally
        {
        }
    }

    private static List<string[]> ReadWorksheetRows(WorksheetPart worksheetPart, SharedStringTable? sharedStrings)
    {
        var rows = new List<string[]>();
        Worksheet? worksheet = worksheetPart.Worksheet;
        if (worksheet is null)
        {
            return rows;
        }

        foreach (Row row in worksheet.Descendants<Row>())
        {
            var values = new SortedDictionary<int, string>();
            foreach (Cell cell in row.Elements<Cell>())
            {
                int index = GetCellColumnIndex(cell.CellReference?.Value);
                values[index] = ReadCellValue(cell, sharedStrings);
            }

            int lastIndex = values.Count == 0 ? -1 : values.Keys.Max();
            string[] rowValues = new string[lastIndex + 1];
            foreach ((int index, string value) in values)
            {
                rowValues[index] = value;
            }

            rows.Add(rowValues);
        }

        return rows;
    }

    private static string ReadCellValue(Cell cell, SharedStringTable? sharedStrings)
    {
        string value = cell.CellValue?.InnerText ?? cell.InnerText ?? string.Empty;
        if (cell.DataType?.Value == CellValues.SharedString && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index))
        {
            return sharedStrings?.ElementAt(index).InnerText ?? string.Empty;
        }

        return cell.DataType?.Value == CellValues.InlineString
            ? cell.InlineString?.InnerText ?? value
            : value;
    }

    private static void WriteWorkbook(string file, IReadOnlyList<string[]> rows)
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create(file, SpreadsheetDocumentType.Workbook);
        WorkbookPart workbookPart = document.AddWorkbookPart();
        workbookPart.Workbook = new Workbook();
        WorksheetPart worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
        var sheetData = new SheetData();
        worksheetPart.Worksheet = new Worksheet(sheetData);
        Sheets sheets = workbookPart.Workbook.AppendChild(new Sheets());
        sheets.Append(new Sheet
        {
            Id = workbookPart.GetIdOfPart(worksheetPart),
            SheetId = 1,
            Name = "Merged"
        });

        foreach (string[] rowValues in rows)
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

    private static string ReadWordText(string file)
    {
        try
        {
            using WordprocessingDocument document = WordprocessingDocument.Open(file, false);
            Body? body = document.MainDocumentPart?.Document?.Body;
            if (body is null)
            {
                return string.Empty;
            }

            return string.Join(Environment.NewLine, body.Descendants<Paragraph>().Select(paragraph => paragraph.InnerText));
        }
        catch (OpenXmlPackageException ex)
        {
            throw new InvalidOperationException($"无法读取 Word 文档：{ex.Message}", ex);
        }
    }

    private static int ExtractImagesFromOfficeFile(string file, string outputDirectory)
    {
        string extension = Path.GetExtension(file).ToLowerInvariant();
        try
        {
            return extension switch
            {
                ".docx" => ExtractImages(WordprocessingDocument.Open(file, false), file, outputDirectory),
                ".xlsx" => ExtractImages(SpreadsheetDocument.Open(file, false), file, outputDirectory),
                ".pptx" => ExtractImages(PresentationDocument.Open(file, false), file, outputDirectory),
                _ => throw new InvalidOperationException("仅支持 docx、xlsx、pptx 图片提取。")
            };
        }
        catch (OpenXmlPackageException ex)
        {
            throw new InvalidOperationException($"无法读取 Office 文档：{ex.Message}", ex);
        }
    }

    private static int ExtractImages(OpenXmlPackage package, string sourceFile, string outputDirectory)
    {
        using (package)
        {
            int count = 0;
            string baseName = SanitizeFileName(Path.GetFileNameWithoutExtension(sourceFile));
            foreach (ImagePart imagePart in EnumerateImageParts(package))
            {
                count++;
                string extension = GuessImageExtension(imagePart);
                string outputPath = GetUniqueOutputPath(outputDirectory, $"{baseName}_image_{count:000}", extension);
                using Stream source = imagePart.GetStream(FileMode.Open, FileAccess.Read);
                using FileStream target = File.Create(outputPath);
                source.CopyTo(target);
            }

            return count;
        }
    }

    private static IEnumerable<ImagePart> EnumerateImageParts(OpenXmlPartContainer container)
    {
        foreach (IdPartPair pair in container.Parts)
        {
            if (pair.OpenXmlPart is ImagePart imagePart)
            {
                yield return imagePart;
            }

            foreach (ImagePart nested in EnumerateImageParts(pair.OpenXmlPart))
            {
                yield return nested;
            }
        }
    }

    private static int ReplaceWordTextInFile(string file, IReadOnlyList<TextReplacement> replacements, StringComparison comparison)
    {
        try
        {
            using WordprocessingDocument document = WordprocessingDocument.Open(file, true);
            int count = 0;
            DocumentFormat.OpenXml.Wordprocessing.Document? wordDocument = document.MainDocumentPart?.Document;
            foreach (WpText text in wordDocument?.Descendants<WpText>() ?? [])
            {
                string current = text.Text;
                foreach (TextReplacement replacement in replacements)
                {
                    string updated = ReplaceWithCount(current, replacement.OldText, replacement.NewText, comparison, out int replacementsInText);
                    current = updated;
                    count += replacementsInText;
                }

                text.Text = current;
            }

            wordDocument?.Save();
            return count;
        }
        catch (OpenXmlPackageException ex)
        {
            throw new InvalidOperationException($"无法修改 Word 文档：{ex.Message}", ex);
        }
    }

    private static IReadOnlyList<TextReplacement> ParseTextReplacements(string rules)
    {
        var replacements = new List<TextReplacement>();
        foreach (string line in rules.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int index = line.IndexOf("=>", StringComparison.Ordinal);
            if (index <= 0)
            {
                continue;
            }

            string oldText = line[..index].Trim();
            string newText = line[(index + 2)..].Trim();
            if (!string.IsNullOrEmpty(oldText))
            {
                replacements.Add(new TextReplacement(oldText, newText));
            }
        }

        if (replacements.Count == 0)
        {
            throw new InvalidOperationException("请至少输入一条替换规则，格式：旧文本 => 新文本。");
        }

        return replacements;
    }

    private static string ReplaceWithCount(string input, string oldText, string newText, StringComparison comparison, out int count)
    {
        count = 0;
        int index = input.IndexOf(oldText, comparison);
        if (index < 0)
        {
            return input;
        }

        var builder = new StringBuilder();
        int start = 0;
        while (index >= 0)
        {
            builder.Append(input, start, index - start);
            builder.Append(newText);
            start = index + oldText.Length;
            count++;
            index = input.IndexOf(oldText, start, comparison);
        }

        builder.Append(input, start, input.Length - start);
        return builder.ToString();
    }

    private static int GetCellColumnIndex(string? cellReference)
    {
        if (string.IsNullOrWhiteSpace(cellReference))
        {
            return 0;
        }

        int column = 0;
        foreach (char c in cellReference)
        {
            if (!char.IsLetter(c))
            {
                break;
            }

            column = column * 26 + (char.ToUpperInvariant(c) - 'A' + 1);
        }

        return Math.Max(0, column - 1);
    }

    private static string ResolveDelimiter(string value)
    {
        return value switch
        {
            "制表符 (Tab)" => "\t",
            "分号 (;)" => ";",
            "竖线 (|)" => "|",
            _ => ","
        };
    }

    private static void EnsureExtension(string file, string expectedExtension)
    {
        if (!Path.GetExtension(file).Equals(expectedExtension, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"文件类型不匹配：{Path.GetFileName(file)} 需要是 {expectedExtension} 文件。");
        }
    }

    private static string GetUniqueOutputPath(string directory, string baseName, string extension)
    {
        string safeBaseName = SanitizeFileName(baseName);
        string path = Path.Combine(directory, safeBaseName + extension);
        int index = 1;
        while (File.Exists(path))
        {
            path = Path.Combine(directory, $"{safeBaseName}_{index}{extension}");
            index++;
        }

        return path;
    }

    private static string SanitizeFileName(string value)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(c, '_');
        }

        return string.IsNullOrWhiteSpace(value) ? "office_output" : value;
    }

    private static string GuessImageExtension(ImagePart imagePart)
    {
        string extension = Path.GetExtension(imagePart.Uri.ToString());
        if (!string.IsNullOrWhiteSpace(extension))
        {
            return extension;
        }

        return imagePart.ContentType.ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/gif" => ".gif",
            "image/bmp" => ".bmp",
            "image/tiff" => ".tiff",
            "image/x-emf" => ".emf",
            "image/x-wmf" => ".wmf",
            _ => ".bin"
        };
    }

    private sealed record ExcelSheetData(string Name, List<string[]> Rows);

    private sealed record TextReplacement(string OldText, string NewText);
}
