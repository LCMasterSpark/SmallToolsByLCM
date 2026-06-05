// 文件作用：集中注册工具 Id 到执行 handler 的路由表，替代 ToolProcessor 中的超长 switch。
using System.Net;
using System.Security.Cryptography;
using 小工具集合.Models;
using 小工具集合.Services.ToolHandlers;

namespace 小工具集合.Services;

public sealed partial class ToolProcessor
{
    private static readonly ToolHandlerRegistry Handlers = new(CreateHandlers());

    public static bool IsPausableTool(string toolId)
    {
        return Handlers.IsPausable(toolId);
    }

    private static IReadOnlyList<IToolHandler> CreateHandlers()
    {
        return
        [
            Handler(["base64"], false, (request, _) => Base64(request)),
            Handler(["url"], false, (request, _) => request.OperationId switch
            {
                "encode" => WebUtility.UrlEncode(request.Input) ?? string.Empty,
                "decode" => WebUtility.UrlDecode(request.Input) ?? string.Empty,
                "analyze" => AnalyzeUrl(request.Input),
                _ => throw new NotSupportedException("暂不支持该 URL 操作。")
            }),
            Handler(["html"], false, (request, _) => request.OperationId == "encode" ? WebUtility.HtmlEncode(request.Input) : WebUtility.HtmlDecode(request.Input)),
            Handler(["unicode"], false, (request, _) => UnicodeEscape(request)),
            Handler(["json"], false, (request, _) => Json(request)),
            Handler(["xml"], false, (request, _) => Xml(request)),
            Handler(["jwt"], false, (request, _) => ParseJwt(request.Input)),
            Handler(["regexTest"], false, (request, _) => TestRegex(request)),
            Handler(["textDiff"], false, (request, _) => DiffText(request)),
            Handler(["sha256"], false, (request, _) => Hash(request.Input, SHA256.HashData)),
            Handler(["sha512"], false, (request, _) => Hash(request.Input, SHA512.HashData)),
            Handler(["md5"], false, (request, _) => Hash(request.Input, MD5.HashData)),
            Handler(["hmacSha256"], false, (request, _) => HmacSha256(request)),
            Handler(["aesGcm"], false, (request, _) => request.OperationId == "encrypt" ? EncryptAes(request) : DecryptAes(request)),
            Handler(["encodeCrypto"], false, (request, _) => request.OperationId == "encrypt" ? EncryptAes(request, "ENCD") : DecryptAes(request, "ENCD")),
            Handler(["rsaOaep"], false, (request, _) => request.OperationId == "encrypt" ? EncryptRsa(request) : DecryptRsa(request)),

            Handler(["fileEncode"], true, (request, context) => EncodeFiles(request, context)),
            Handler(["mp4ToMp3"], true, (request, context) => ExtractMp3Batch(request, context)),
            Handler(["imageConvert"], true, (request, context) => ConvertImages(request, context)),
            Handler(["fileHash"], true, (request, context) => HashFiles(request, context)),
            Handler(["imageCompress"], true, (request, context) => CompressImages(request, context)),
            Handler(["csvCleaner"], true, (request, context) => CleanCsvFiles(request, context)),
            Handler(["excelSheetMerge"], true, (request, context) => MergeExcelSheets(request, context)),
            Handler(["wordTextExtract"], true, (request, context) => ExtractWordText(request, context)),
            Handler(["officeImageExtract"], true, (request, context) => ExtractOfficeImages(request, context)),
            Handler(["wordBatchReplace"], true, (request, context) => ReplaceWordTextBatch(request, context)),
            Handler(["excelCsvTools", "excelToCsvBatch", "csvToExcel"], true, (request, context) => request.ToolId switch
            {
                "excelToCsvBatch" => ExportExcelToCsvBatch(request, context),
                "csvToExcel" => ConvertCsvToExcel(request, context),
                _ => ExecuteExcelCsvTool(request, context)
            }),
            Handler(["wordMerge"], true, (request, context) => MergeWordDocuments(request, context)),
            Handler(["pptTextExtract"], true, (request, context) => ExtractPptText(request, context)),
            Handler(["officeMetadata"], true, (request, context) => InspectOfficeMetadata(request, context)),
            Handler(["pdfTools", "pdfInfo", "pdfTextExtract", "pdfImageExtract", "pdfToWordLite"], true, (request, context) => request.ToolId switch
            {
                "pdfInfo" => InspectPdfInfo(request, context),
                "pdfTextExtract" => ExtractPdfText(request, context),
                "pdfImageExtract" => ExtractPdfImages(request, context),
                "pdfToWordLite" => ConvertPdfToWordLite(request, context),
                _ => ExecutePdfTool(request, context)
            }),
            Handler(["localOfficeEngineCheck"], false, (_, _) => CheckLocalOfficeEngines()),
            Handler(["localOfficeConvert", "pdfToWordLocal", "officeToPdfLocal", "batchOfficeConvert"], true, (request, context) => request.ToolId switch
            {
                "pdfToWordLocal" => ConvertPdfToWordLocal(request, context),
                "officeToPdfLocal" => ConvertOfficeToPdfLocal(request, context),
                "batchOfficeConvert" => ConvertOfficeBatch(request, context),
                _ => ExecuteLocalOfficeConvertTool(request, context)
            }),
            Handler(["textTranslator"], false, (request, _) => TranslateText(request)),
            Handler(["fileTranslator"], true, (request, context) => TranslateFiles(request, context)),

            Handler(["httpRequest"], false, (request, _) => SendHttpRequest(request)),
            Handler(["urlParams"], false, (request, _) => ParseUrlParameters(request.Input)),
            Handler(["headerFormat"], false, (request, _) => FormatHeaders(request.Input)),
            Handler(["cookieFormat"], false, (request, _) => FormatCookies(request.Input)),
            Handler(["ping"], false, (request, _) => PingHost(request)),
            Handler(["portCheck"], false, (request, _) => CheckPort(request)),
            Handler(["portUsage"], false, (request, _) => QueryPortUsage(request.Input)),
            Handler(["dnsLookup"], false, (request, _) => LookupDns(request)),
            Handler(["publicIp"], false, (_, _) => QueryPublicIp()),
            Handler(["curlGenerator"], false, (request, _) => GenerateCurl(request)),
            Handler(["hostsReset"], false, (request, _) => ResetHosts(request)),
            Handler(["proxyReset"], false, (request, _) => ResetProxy(request)),
            Handler(["networkReset"], false, (request, _) => ResetNetwork(request)),

            Handler(["uuid"], false, (_, _) => Guid.NewGuid().ToString("D")),
            Handler(["timestamp"], false, (request, _) => Timestamp(request)),
            Handler(["passwordGenerator"], false, (request, _) => GeneratePasswords(request)),
            Handler(["screenshotOcr"], false, (request, _) => RecognizeImageText(request)),

            Handler(["choicePicker"], false, (request, _) => PickChoice(request)),
            Handler(["shuffleLines"], false, (request, _) => ShuffleLines(request)),
            Handler(["randomNumber"], false, (request, _) => GenerateRandomNumbers(request)),
            Handler(["diceRoller"], false, (request, _) => RollDice(request)),
            Handler(["reverseText"], false, (request, _) => ReverseFunText(request)),
            Handler(["emojiWrap"], false, (request, _) => WrapWithEmoji(request)),
            Handler(["mockingText"], false, (request, _) => MockText(request)),
            Handler(["zalgoText"], false, (request, _) => GlitchText(request)),
            Handler(["commitMessage"], false, (request, _) => GenerateCommitMessages(request)),
            Handler(["variableName"], false, (request, _) => GenerateVariableNames(request)),
            Handler(["fakeLog"], false, (request, _) => GenerateFakeLog(request)),
            Handler(["excuseGenerator"], false, (request, _) => GenerateExcuse(request)),
            Handler(["onlineHitokoto"], false, (request, _) => QueryOnlineHitokoto(request)),
            Handler(["onlinePoemLine"], false, (_, _) => QueryOnlinePoemLine()),
            Handler(["weatherCard"], false, (request, _) => QueryWeatherCard(request.Input)),
            Handler(["ipInfoCard"], false, (request, _) => QueryIpInfoCard(request.Input))
        ];
    }

    private static IToolHandler Handler(
        IEnumerable<string> toolIds,
        bool isPausable,
        Func<ToolRequest, ToolExecutionContext?, string> execute)
    {
        return new DelegateToolHandler(toolIds, isPausable, execute);
    }
}
