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
            // ToolCatalog 负责可见的工具元数据；这里把元数据 Id
            // 映射到运行时真正执行的实现。
            string output = request.ToolId switch
            {
                "base64" => Base64(request),
                "url" => request.OperationId switch
                {
                    "encode" => WebUtility.UrlEncode(request.Input),
                    "decode" => WebUtility.UrlDecode(request.Input),
                    "analyze" => AnalyzeUrl(request.Input),
                    _ => throw new NotSupportedException("暂不支持该 URL 操作。")
                },
                "html" => request.OperationId == "encode" ? WebUtility.HtmlEncode(request.Input) : WebUtility.HtmlDecode(request.Input),
                "unicode" => UnicodeEscape(request),
                "json" => Json(request),
                "xml" => Xml(request),
                "jwt" => ParseJwt(request.Input),
                "regexTest" => TestRegex(request),
                "textDiff" => DiffText(request),
                "sha256" => Hash(request.Input, SHA256.HashData),
                "sha512" => Hash(request.Input, SHA512.HashData),
                "md5" => Hash(request.Input, MD5.HashData),
                "hmacSha256" => HmacSha256(request),
                "aesGcm" => request.OperationId == "encrypt" ? EncryptAes(request) : DecryptAes(request),
                "encodeCrypto" => request.OperationId == "encrypt" ? EncryptAes(request, "ENCD") : DecryptAes(request, "ENCD"),
                "rsaOaep" => request.OperationId == "encrypt" ? EncryptRsa(request) : DecryptRsa(request),
                "fileEncode" => EncodeFiles(request),
                "mp4ToMp3" => ExtractMp3Batch(request),
                "imageConvert" => ConvertImages(request),
                "fileHash" => HashFiles(request),
                "imageCompress" => CompressImages(request),
                "csvCleaner" => CleanCsvFiles(request),
                "excelSheetMerge" => MergeExcelSheets(request),
                "wordTextExtract" => ExtractWordText(request),
                "officeImageExtract" => ExtractOfficeImages(request),
                "wordBatchReplace" => ReplaceWordTextBatch(request),
                "excelCsvTools" => ExecuteExcelCsvTool(request),
                "excelToCsvBatch" => ExportExcelToCsvBatch(request),
                "csvToExcel" => ConvertCsvToExcel(request),
                "wordMerge" => MergeWordDocuments(request),
                "pptTextExtract" => ExtractPptText(request),
                "officeMetadata" => InspectOfficeMetadata(request),
                "pdfTools" => ExecutePdfTool(request),
                "pdfInfo" => InspectPdfInfo(request),
                "pdfTextExtract" => ExtractPdfText(request),
                "pdfImageExtract" => ExtractPdfImages(request),
                "pdfToWordLite" => ConvertPdfToWordLite(request),
                "localOfficeEngineCheck" => CheckLocalOfficeEngines(),
                "localOfficeConvert" => ExecuteLocalOfficeConvertTool(request),
                "pdfToWordLocal" => ConvertPdfToWordLocal(request),
                "officeToPdfLocal" => ConvertOfficeToPdfLocal(request),
                "batchOfficeConvert" => ConvertOfficeBatch(request),
                "httpRequest" => SendHttpRequest(request),
                "urlParams" => ParseUrlParameters(request.Input),
                "headerFormat" => FormatHeaders(request.Input),
                "cookieFormat" => FormatCookies(request.Input),
                "ping" => PingHost(request),
                "portCheck" => CheckPort(request),
                "portUsage" => QueryPortUsage(request.Input),
                "dnsLookup" => LookupDns(request),
                "publicIp" => QueryPublicIp(),
                "curlGenerator" => GenerateCurl(request),
                "hostsReset" => ResetHosts(request),
                "proxyReset" => ResetProxy(request),
                "networkReset" => ResetNetwork(request),
                "uuid" => Guid.NewGuid().ToString("D"),
                "timestamp" => Timestamp(request),
                "passwordGenerator" => GeneratePasswords(request),
                "choicePicker" => PickChoice(request),
                "shuffleLines" => ShuffleLines(request),
                "randomNumber" => GenerateRandomNumbers(request),
                "diceRoller" => RollDice(request),
                "reverseText" => ReverseFunText(request),
                "emojiWrap" => WrapWithEmoji(request),
                "mockingText" => MockText(request),
                "zalgoText" => GlitchText(request),
                "commitMessage" => GenerateCommitMessages(request),
                "variableName" => GenerateVariableNames(request),
                "fakeLog" => GenerateFakeLog(request),
                "excuseGenerator" => GenerateExcuse(request),
                "onlineHitokoto" => QueryOnlineHitokoto(request),
                "onlinePoemLine" => QueryOnlinePoemLine(),
                "weatherCard" => QueryWeatherCard(request.Input),
                "ipInfoCard" => QueryIpInfoCard(request.Input),
                _ => throw new NotSupportedException("暂不支持该工具。")
            };

            return ToolResult.Ok(output ?? string.Empty);
        }
        catch (Exception ex) when (ex is FormatException or JsonException or XmlException or CryptographicException or InvalidOperationException or ArgumentException or IOException or Win32Exception or HttpRequestException or CsvHelperException or OpenXmlPackageException)
        {
            return ToolResult.Fail(ex.Message);
        }
    }

    public Task<ToolResult> ExecuteAsync(ToolRequest request, ToolExecutionContext context)
    {
        // 大多数工具都是短小的 CPU/字符串操作，可以复用同步执行路径。
        // 批量文件工具单独分支处理，便于在队列文件之间暂停。
        if (request.ToolId is not ("fileEncode" or "mp4ToMp3" or "imageConvert" or "fileHash" or "imageCompress" or "csvCleaner" or "excelSheetMerge" or "wordTextExtract" or "officeImageExtract" or "wordBatchReplace" or "excelCsvTools" or "excelToCsvBatch" or "csvToExcel" or "wordMerge" or "pptTextExtract" or "officeMetadata" or "pdfTools" or "pdfInfo" or "pdfTextExtract" or "pdfImageExtract" or "pdfToWordLite" or "localOfficeConvert" or "pdfToWordLocal" or "officeToPdfLocal" or "batchOfficeConvert"))
        {
            return Task.Run(() => Execute(request));
        }

        return Task.Run(() =>
        {
            try
            {
                string output = request.ToolId switch
                {
                    "fileEncode" => EncodeFiles(request, context),
                    "mp4ToMp3" => ExtractMp3Batch(request, context),
                    "imageConvert" => ConvertImages(request, context),
                    "fileHash" => HashFiles(request, context),
                    "imageCompress" => CompressImages(request, context),
                    "csvCleaner" => CleanCsvFiles(request, context),
                    "excelSheetMerge" => MergeExcelSheets(request, context),
                    "wordTextExtract" => ExtractWordText(request, context),
                    "officeImageExtract" => ExtractOfficeImages(request, context),
                    "wordBatchReplace" => ReplaceWordTextBatch(request, context),
                    "excelCsvTools" => ExecuteExcelCsvTool(request, context),
                    "excelToCsvBatch" => ExportExcelToCsvBatch(request, context),
                    "csvToExcel" => ConvertCsvToExcel(request, context),
                    "wordMerge" => MergeWordDocuments(request, context),
                    "pptTextExtract" => ExtractPptText(request, context),
                    "officeMetadata" => InspectOfficeMetadata(request, context),
                    "pdfTools" => ExecutePdfTool(request, context),
                    "pdfInfo" => InspectPdfInfo(request, context),
                    "pdfTextExtract" => ExtractPdfText(request, context),
                    "pdfImageExtract" => ExtractPdfImages(request, context),
                    "pdfToWordLite" => ConvertPdfToWordLite(request, context),
                    "localOfficeConvert" => ExecuteLocalOfficeConvertTool(request, context),
                    "pdfToWordLocal" => ConvertPdfToWordLocal(request, context),
                    "officeToPdfLocal" => ConvertOfficeToPdfLocal(request, context),
                    "batchOfficeConvert" => ConvertOfficeBatch(request, context),
                    _ => throw new NotSupportedException("暂不支持该工具。")
                };

                return ToolResult.Ok(output);
            }
            catch (Exception ex) when (ex is FormatException or CryptographicException or InvalidOperationException or ArgumentException or IOException or Win32Exception or CsvHelperException or OpenXmlPackageException)
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
