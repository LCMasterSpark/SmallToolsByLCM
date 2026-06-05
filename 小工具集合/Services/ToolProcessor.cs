// 文件作用：工具执行调度入口，把工具 Id 分派到对应 partial 处理器。
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml;
using CsvHelper;
using DocumentFormat.OpenXml.Packaging;
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
public sealed partial class ToolProcessor : IToolProcessor
{
    private const int InternetOptionRefresh = 37;
    private const int InternetOptionSettingsChanged = 39;
    // AES-GCM 载荷格式：前缀 + salt + nonce + tag + 密文。
    // 前缀用于区分文本加密和文件加密格式。
    private const int AesSaltSize = 16;
    private const int AesNonceSize = 12;
    private const int AesTagSize = 16;
    private const int AesKeySize = 32;
    private const int Pbkdf2Iterations = 100_000;
    private const string AesPackagePrefix = "AESG";
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan DnsLookupTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan PortUsageTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan PublicIpRequestTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan OnlineFunRequestTimeout = TimeSpan.FromSeconds(8);
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(60) };

    public ToolResult Execute(ToolRequest request)
    {
        try
        {
            // ToolCatalog 负责可见元数据；ToolHandlerRegistry 负责把工具 Id 路由到执行实现。
            string output = Handlers.GetRequired(request.ToolId).Execute(request);

            return ToolResult.Ok(output ?? string.Empty);
        }
        catch (Exception ex) when (ex is FormatException or JsonException or XmlException or CryptographicException or InvalidOperationException or ArgumentException or IOException or Win32Exception or HttpRequestException or TaskCanceledException or CsvHelperException or OpenXmlPackageException)
        {
            return ToolResult.Fail(ex.Message);
        }
    }

    public Task<ToolResult> ExecuteAsync(ToolRequest request, ToolExecutionContext context)
    {
        // 大多数工具都是短小的 CPU/字符串操作，可以复用同步执行路径。
        // 批处理能力由 handler 自己声明，避免 ViewModel/Processor 维护超长 toolId 清单。
        if (!Handlers.IsPausable(request.ToolId))
        {
            return Task.Run(() => Execute(request));
        }

        return Task.Run(() =>
        {
            try
            {
                string output = Handlers.GetRequired(request.ToolId).Execute(request, context);

                return ToolResult.Ok(output);
            }
            catch (Exception ex) when (ex is FormatException or CryptographicException or InvalidOperationException or ArgumentException or IOException or Win32Exception or HttpRequestException or TaskCanceledException or CsvHelperException or OpenXmlPackageException)
            {
                return ToolResult.Fail(ex.Message);
            }
        });
    }

    private static string ExecuteExcelCsvTool(ToolRequest request, ToolExecutionContext? context = null)
    {
        return request.OperationId switch
        {
            "excelToCsv" => ExportExcelToCsvBatch(request, context),
            "csvToExcel" => ConvertCsvToExcel(request, context),
            _ => throw new NotSupportedException("不支持的 Excel/CSV 操作。")
        };
    }

    private static string ExecutePdfTool(ToolRequest request, ToolExecutionContext? context = null)
    {
        return request.OperationId switch
        {
            "info" => InspectPdfInfo(request, context),
            "text" => ExtractPdfText(request, context),
            "images" => ExtractPdfImages(request, context),
            "wordLite" => ConvertPdfToWordLite(request, context),
            _ => throw new NotSupportedException("不支持的 PDF 操作。")
        };
    }

    private static string ExecuteLocalOfficeConvertTool(ToolRequest request, ToolExecutionContext? context = null)
    {
        return request.OperationId switch
        {
            "pdfToWord" => ConvertPdfToWordLocal(request, context),
            "officeToPdf" => ConvertOfficeToPdfLocal(request, context),
            "batch" => ConvertOfficeBatch(request, context),
            _ => throw new NotSupportedException("不支持的本机转换操作。")
        };
    }

}
