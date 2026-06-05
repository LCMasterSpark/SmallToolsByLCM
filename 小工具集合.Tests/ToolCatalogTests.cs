// 文件作用：验证工具目录中的关键分组、互动工具和元数据注册是否完整。
using 小工具集合.Services;

namespace 小工具集合.Tests;

public sealed class ToolCatalogTests
{
    [Fact]
    public void GenerationGroup_ContainsQrCodeAndScreenPointerInteractiveTools()
    {
        var generationGroup = Assert.Single(ToolCatalog.Groups, group => group.Name == "生成工具");

        var qrCode = Assert.Single(generationGroup.Tools, tool => tool.Id == "qrCode");
        Assert.Equal("生成 QR Code", qrCode.Name);
        Assert.Equal("qrCode", qrCode.InteractiveViewKey);
        Assert.False(qrCode.RequiresInput);

        var screenPointer = Assert.Single(generationGroup.Tools, tool => tool.Id == "screenPointer");
        Assert.Equal("screenPointer", screenPointer.InteractiveViewKey);
        Assert.False(screenPointer.RequiresInput);

        var ocr = Assert.Single(generationGroup.Tools, tool => tool.Id == "screenshotOcr");
        Assert.False(ocr.RequiresInput);
        Assert.NotEmpty(ocr.Parameters);
    }

    [Fact]
    public void OfficeHelperGroup_ContainsFirstWaveTools()
    {
        var officeGroup = Assert.Single(ToolCatalog.Groups, group => group.Name == "OfficeHelper");
        string[] expectedIds =
        [
            "csvCleaner",
            "excelSheetMerge",
            "wordTextExtract",
            "officeImageExtract",
            "wordBatchReplace",
            "excelCsvTools",
            "wordMerge",
            "pptTextExtract",
            "officeMetadata",
            "pdfTools",
            "localOfficeEngineCheck",
            "localOfficeConvert"
        ];

        Assert.All(expectedIds, id =>
        {
            var tool = Assert.Single(officeGroup.Tools, item => item.Id == id);
            Assert.False(tool.RequiresInput);
            if (id != "localOfficeEngineCheck")
            {
                Assert.NotEmpty(tool.Parameters);
            }
        });
    }

    [Fact]
    public void TranslationGroup_ContainsThreeTranslationTools()
    {
        var translationGroup = Assert.Single(ToolCatalog.Groups, group => group.Name == "翻译");

        var textTranslator = Assert.Single(translationGroup.Tools, tool => tool.Id == "textTranslator");
        Assert.True(textTranslator.RequiresInput);
        Assert.Contains(textTranslator.Parameters, parameter => parameter.Id == "provider");

        var fileTranslator = Assert.Single(translationGroup.Tools, tool => tool.Id == "fileTranslator");
        Assert.False(fileTranslator.RequiresInput);
        Assert.Contains(fileTranslator.Parameters, parameter => parameter.Id == "inputFiles");

        var liveTranslator = Assert.Single(translationGroup.Tools, tool => tool.Id == "liveTranslator");
        Assert.False(liveTranslator.RequiresInput);
        Assert.Equal("liveTranslator", liveTranslator.InteractiveViewKey);
    }

    [Fact]
    public void Catalog_AllToolIdsAreUniqueAndOperationsExist()
    {
        var tools = ToolCatalog.AllTools;

        Assert.NotEmpty(tools);
        Assert.Equal(tools.Count, tools.Select(tool => tool.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.All(tools, tool =>
        {
            Assert.False(string.IsNullOrWhiteSpace(tool.Name));
            Assert.False(string.IsNullOrWhiteSpace(tool.GroupName));
            Assert.NotEmpty(tool.Operations);
        });
    }

    [Fact]
    public void Catalog_FindToolKeepsMergedOfficeToolCompatibility()
    {
        Assert.Equal("excelCsvTools", ToolCatalog.FindTool("excelToCsvBatch")?.Id);
        Assert.Equal("excelCsvTools", ToolCatalog.FindTool("csvToExcel")?.Id);
        Assert.Equal("pdfTools", ToolCatalog.FindTool("pdfTextExtract")?.Id);
        Assert.Equal("localOfficeConvert", ToolCatalog.FindTool("officeToPdfLocal")?.Id);
    }

    [Fact]
    public void ToolProcessor_ExposesPausableToolsThroughHandlerRegistry()
    {
        Assert.True(ToolProcessor.IsPausableTool("fileTranslator"));
        Assert.True(ToolProcessor.IsPausableTool("excelCsvTools"));
        Assert.True(ToolProcessor.IsPausableTool("localOfficeConvert"));
        Assert.False(ToolProcessor.IsPausableTool("base64"));
        Assert.False(ToolProcessor.IsPausableTool("missing-tool"));
    }
}
