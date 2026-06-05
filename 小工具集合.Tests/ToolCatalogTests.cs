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
}
