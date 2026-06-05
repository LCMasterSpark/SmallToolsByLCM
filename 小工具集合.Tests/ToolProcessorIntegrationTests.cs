// 文件作用：覆盖 ToolProcessor 中不依赖真实外部服务的端到端工具执行路径。
using System.Text.RegularExpressions;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;
using 小工具集合.Models;
using 小工具集合.Services;

namespace 小工具集合.Tests;

public sealed class ToolProcessorIntegrationTests
{
    private readonly ToolProcessor processor = new();

    [Fact]
    public void Base64_EncodesAndDecodesUtf8Text()
    {
        ToolResult encoded = Execute("base64", "encode", "LCMasterSpark 你好");
        Assert.True(encoded.Success);
        Assert.False(string.IsNullOrWhiteSpace(encoded.Output));

        ToolResult decoded = Execute("base64", "decode", encoded.Output);
        Assert.True(decoded.Success);
        Assert.Equal("LCMasterSpark 你好", decoded.Output);
    }

    [Fact]
    public void UrlTool_EncodesDecodesAndAnalyzesUrl()
    {
        ToolResult encoded = Execute("url", "encode", "a b&c=1");
        Assert.True(encoded.Success);
        Assert.Contains("a+b", encoded.Output, StringComparison.Ordinal);
        Assert.Contains("%26", encoded.Output, StringComparison.Ordinal);

        ToolResult decoded = Execute("url", "decode", "a%20b%26c%3D1");
        Assert.True(decoded.Success);
        Assert.Equal("a b&c=1", decoded.Output);

        ToolResult analyzed = Execute("url", "analyze", "https://example.com:8443/a/b?x=1&x=2&name=%E5%BC%A0#top");
        Assert.True(analyzed.Success);
        Assert.Contains("example.com", analyzed.Output, StringComparison.Ordinal);
        Assert.Contains("8443", analyzed.Output, StringComparison.Ordinal);
        Assert.Contains("x = 1", analyzed.Output, StringComparison.Ordinal);
        Assert.Contains("name = 张", analyzed.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void TextDiff_ReturnsUnifiedStyleDiff()
    {
        ToolResult result = Execute("textDiff", "diff", "one\ntwo", new Dictionary<string, string>
        {
            ["newText"] = "one\nthree"
        });

        Assert.True(result.Success);
        Assert.Contains("--- 原文本", result.Output, StringComparison.Ordinal);
        Assert.Contains("+++ 新文本", result.Output, StringComparison.Ordinal);
        Assert.Contains("- two", result.Output, StringComparison.Ordinal);
        Assert.Contains("+ three", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void RandomNumber_WithUniqueRangeReturnsEveryValueOnce()
    {
        ToolResult result = Execute("randomNumber", "generate", string.Empty, new Dictionary<string, string>
        {
            ["min"] = "1",
            ["max"] = "3",
            ["count"] = "3",
            ["unique"] = "true"
        });

        Assert.True(result.Success);
        string numbersLine = result.Output
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Last();
        int[] values = Regex.Matches(numbersLine, @"\d+")
            .Select(match => int.Parse(match.Value))
            .Order()
            .ToArray();
        Assert.Equal([1, 2, 3], values);
    }

    [Fact]
    public void DiceRoller_RejectsInvalidExpression()
    {
        ToolResult result = Execute("diceRoller", "roll", "totally-not-dice");

        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
    }

    [Fact]
    public void AesGcm_RoundTripsTextWithPassword()
    {
        var parameters = new Dictionary<string, string> { ["password"] = "correct horse battery staple" };

        ToolResult encrypted = Execute("aesGcm", "encrypt", "secret payload", parameters);
        Assert.True(encrypted.Success);
        Assert.NotEqual("secret payload", encrypted.Output);

        ToolResult decrypted = Execute("aesGcm", "decrypt", encrypted.Output, parameters);
        Assert.True(decrypted.Success);
        Assert.Equal("secret payload", decrypted.Output);
    }

    [Fact]
    public void CsvCleaner_TrimsEmptyRowsAndDuplicates()
    {
        string root = CreateTempDirectory();
        try
        {
            string input = Path.Combine(root, "messy.csv");
            string output = Path.Combine(root, "out");
            Directory.CreateDirectory(output);
            File.WriteAllText(input, "name,value\n Alice , 1 \n\n Alice , 1 \nBob,2\n");

            ToolResult result = Execute("csvCleaner", "clean", string.Empty, new Dictionary<string, string>
            {
                ["inputFiles"] = input,
                ["outputDirectory"] = output,
                ["delimiter"] = "逗号 (,)",
                ["trimCells"] = "true",
                ["removeEmptyRows"] = "true",
                ["distinctRows"] = "true"
            });

            Assert.True(result.Success, result.Message);
            string cleaned = Assert.Single(Directory.GetFiles(output, "*_cleaned.csv"));
            string content = File.ReadAllText(cleaned);
            Assert.Contains("Alice", content, StringComparison.Ordinal);
            Assert.Contains("Bob", content, StringComparison.Ordinal);
            Assert.Single(Regex.Matches(content, "Alice").Cast<Match>());
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void WordTextExtract_ReturnsDocxText()
    {
        string root = CreateTempDirectory();
        try
        {
            string input = Path.Combine(root, "sample.docx");
            CreateDocx(input, "Hello OfficeHelper", "Second paragraph");

            ToolResult result = Execute("wordTextExtract", "extract", string.Empty, new Dictionary<string, string>
            {
                ["inputFiles"] = input,
                ["outputDirectory"] = root,
                ["saveTxt"] = "false",
                ["includeFileHeaders"] = "false"
            });

            Assert.True(result.Success, result.Message);
            Assert.Contains("Hello OfficeHelper", result.Output, StringComparison.Ordinal);
            Assert.Contains("Second paragraph", result.Output, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void WordBatchReplace_WritesReplacedCopy()
    {
        string root = CreateTempDirectory();
        try
        {
            string input = Path.Combine(root, "replace-me.docx");
            string output = Path.Combine(root, "out");
            Directory.CreateDirectory(output);
            CreateDocx(input, "Hello OfficeHelper");

            ToolResult result = Execute("wordBatchReplace", "replace", string.Empty, new Dictionary<string, string>
            {
                ["inputFiles"] = input,
                ["outputDirectory"] = output,
                ["replacements"] = "OfficeHelper => LCM Toolbox",
                ["caseSensitive"] = "true"
            });

            Assert.True(result.Success, result.Message);
            string replaced = Assert.Single(Directory.GetFiles(output, "*_replaced.docx"));
            Assert.Contains("Hello LCM Toolbox", ReadDocxText(replaced), StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void CsvToExcel_AndExcelToCsv_WriteExpectedFiles()
    {
        string root = CreateTempDirectory();
        try
        {
            string csv = Path.Combine(root, "people.csv");
            string outDir = Path.Combine(root, "out");
            Directory.CreateDirectory(outDir);
            File.WriteAllText(csv, "Name,Score\nAlice,9\nBob,8\n");

            ToolResult toExcel = Execute("excelCsvTools", "csvToExcel", string.Empty, new Dictionary<string, string>
            {
                ["inputFiles"] = csv,
                ["outputDirectory"] = outDir,
                ["workbookName"] = "people_book"
            });

            Assert.True(toExcel.Success, toExcel.Message);
            string xlsx = Assert.Single(Directory.GetFiles(outDir, "people_book*.xlsx"));

            ToolResult toCsv = Execute("excelCsvTools", "excelToCsv", string.Empty, new Dictionary<string, string>
            {
                ["inputFiles"] = xlsx,
                ["outputDirectory"] = outDir
            });

            Assert.True(toCsv.Success, toCsv.Message);
            Assert.NotEmpty(Directory.GetFiles(outDir, "*people*.csv"));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void WordMerge_WritesCombinedDocx()
    {
        string root = CreateTempDirectory();
        try
        {
            string first = Path.Combine(root, "a.docx");
            string second = Path.Combine(root, "b.docx");
            string outDir = Path.Combine(root, "out");
            Directory.CreateDirectory(outDir);
            CreateDocx(first, "First document");
            CreateDocx(second, "Second document");

            ToolResult result = Execute("wordMerge", "merge", string.Empty, new Dictionary<string, string>
            {
                ["inputFiles"] = first + Environment.NewLine + second,
                ["outputDirectory"] = outDir,
                ["outputName"] = "merged",
                ["insertPageBreaks"] = "true"
            });

            Assert.True(result.Success, result.Message);
            string merged = Assert.Single(Directory.GetFiles(outDir, "merged*.docx"));
            string text = ReadDocxText(merged);
            Assert.Contains("First document", text, StringComparison.Ordinal);
            Assert.Contains("Second document", text, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void PptTextExtract_AndOfficeMetadata_ReturnText()
    {
        string root = CreateTempDirectory();
        try
        {
            string pptx = Path.Combine(root, "slides.pptx");
            CreatePptx(pptx, "Hello PPT");

            ToolResult text = Execute("pptTextExtract", "extract", string.Empty, new Dictionary<string, string>
            {
                ["inputFiles"] = pptx,
                ["outputDirectory"] = root,
                ["includeSlideHeaders"] = "true"
            });
            Assert.True(text.Success, text.Message);
            Assert.Contains("Hello PPT", text.Output, StringComparison.Ordinal);

            ToolResult metadata = Execute("officeMetadata", "inspect", string.Empty, new Dictionary<string, string>
            {
                ["inputFiles"] = pptx
            });
            Assert.True(metadata.Success, metadata.Message);
            Assert.Contains("slides.pptx", metadata.Output, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void PdfTools_ReadTextAndWriteLiteDocx()
    {
        string root = CreateTempDirectory();
        try
        {
            string pdf = Path.Combine(root, "sample.pdf");
            string outDir = Path.Combine(root, "out");
            Directory.CreateDirectory(outDir);
            CreatePdf(pdf, "Hello PDF Lite");

            ToolResult info = Execute("pdfTools", "info", string.Empty, new Dictionary<string, string>
            {
                ["inputFiles"] = pdf
            });
            Assert.True(info.Success, info.Message);
            Assert.Contains("sample.pdf", info.Output, StringComparison.Ordinal);

            ToolResult text = Execute("pdfTools", "text", string.Empty, new Dictionary<string, string>
            {
                ["inputFiles"] = pdf,
                ["outputDirectory"] = outDir
            });
            Assert.True(text.Success, text.Message);
            Assert.Contains("Hello PDF Lite", text.Output, StringComparison.Ordinal);

            ToolResult lite = Execute("pdfTools", "wordLite", string.Empty, new Dictionary<string, string>
            {
                ["inputFiles"] = pdf,
                ["outputDirectory"] = outDir
            });
            Assert.True(lite.Success, lite.Message);
            string docx = Assert.Single(Directory.GetFiles(outDir, "*_lite.docx"));
            Assert.Contains("Hello PDF Lite", ReadDocxText(docx), StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void LocalOfficeEngineCheck_ReturnsReportWithoutInstalledEngines()
    {
        ToolResult result = Execute("localOfficeEngineCheck", "check", string.Empty);

        Assert.True(result.Success, result.Message);
        Assert.Contains("Microsoft Office", result.Output, StringComparison.Ordinal);
        Assert.Contains("LibreOffice", result.Output, StringComparison.Ordinal);
        Assert.Contains("WPS", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void TextTranslator_UsesConfiguredProvider()
    {
        ToolResult result = Execute("textTranslator", "translate", "hello", new Dictionary<string, string>
        {
            ["provider"] = "Mock",
            ["sourceLanguage"] = "英文",
            ["targetLanguage"] = "中文"
        });

        Assert.True(result.Success, result.Message);
        Assert.Contains("[中文] hello", result.Output, StringComparison.Ordinal);
        Assert.Contains("Mock", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void FileTranslator_WritesTranslatedTextCopy()
    {
        string root = CreateTempDirectory();
        try
        {
            string input = Path.Combine(root, "note.txt");
            string outDir = Path.Combine(root, "out");
            Directory.CreateDirectory(outDir);
            File.WriteAllText(input, "hello file", Encoding.UTF8);

            ToolResult result = Execute("fileTranslator", "translate", string.Empty, new Dictionary<string, string>
            {
                ["inputFiles"] = input,
                ["outputDirectory"] = outDir,
                ["provider"] = "Mock",
                ["sourceLanguage"] = "英文",
                ["targetLanguage"] = "中文"
            });

            Assert.True(result.Success, result.Message);
            string output = Assert.Single(Directory.GetFiles(outDir, "*_translated.txt"));
            Assert.Equal("hello file", File.ReadAllText(input));
            Assert.Contains("[中文] hello file", File.ReadAllText(output), StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private ToolResult Execute(
        string toolId,
        string operationId,
        string input,
        IReadOnlyDictionary<string, string>? parameters = null)
    {
        return processor.Execute(new ToolRequest
        {
            ToolId = toolId,
            OperationId = operationId,
            Input = input,
            Parameters = parameters ?? new Dictionary<string, string>()
        });
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "LcmToolboxTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static void CreateDocx(string file, params string[] paragraphs)
    {
        using WordprocessingDocument document = WordprocessingDocument.Create(file, WordprocessingDocumentType.Document);
        MainDocumentPart mainPart = document.AddMainDocumentPart();
        var body = new Body();
        foreach (string paragraphText in paragraphs)
        {
            body.Append(new Paragraph(new DocumentFormat.OpenXml.Wordprocessing.Run(new DocumentFormat.OpenXml.Wordprocessing.Text(paragraphText))));
        }

        mainPart.Document = new DocumentFormat.OpenXml.Wordprocessing.Document(body);
        mainPart.Document.Save();
    }

    private static string ReadDocxText(string file)
    {
        using WordprocessingDocument document = WordprocessingDocument.Open(file, false);
        return document.MainDocumentPart?.Document?.Body?.InnerText ?? string.Empty;
    }

    private static void CreatePptx(string file, string text)
    {
        using PresentationDocument document = PresentationDocument.Create(file, PresentationDocumentType.Presentation);
        PresentationPart presentationPart = document.AddPresentationPart();
        presentationPart.Presentation = new P.Presentation();
        SlidePart slidePart = presentationPart.AddNewPart<SlidePart>();
        slidePart.Slide = new P.Slide(new P.CommonSlideData(new P.ShapeTree(
            new P.NonVisualGroupShapeProperties(new P.NonVisualDrawingProperties { Id = 1, Name = "Root" }, new P.NonVisualGroupShapeDrawingProperties(), new P.ApplicationNonVisualDrawingProperties()),
            new P.GroupShapeProperties(),
            new P.Shape(
                new P.NonVisualShapeProperties(new P.NonVisualDrawingProperties { Id = 2, Name = "Text" }, new P.NonVisualShapeDrawingProperties(), new P.ApplicationNonVisualDrawingProperties()),
                new P.ShapeProperties(),
                new P.TextBody(new A.BodyProperties(), new A.ListStyle(), new A.Paragraph(new A.Run(new A.Text(text))))))));
        slidePart.Slide.Save();
        string relationshipId = presentationPart.GetIdOfPart(slidePart);
        presentationPart.Presentation.Append(new P.SlideIdList(new P.SlideId { Id = 256U, RelationshipId = relationshipId }));
        presentationPart.Presentation.Save();
    }

    private static void CreatePdf(string file, string text)
    {
        var builder = new PdfDocumentBuilder();
        PdfPageBuilder page = builder.AddPage(595, 842);
        PdfDocumentBuilder.AddedFont font = builder.AddStandard14Font(Standard14Font.Helvetica);
        page.AddText(text, 12, new PdfPoint(50, 760), font);
        File.WriteAllBytes(file, builder.Build());
    }
}
