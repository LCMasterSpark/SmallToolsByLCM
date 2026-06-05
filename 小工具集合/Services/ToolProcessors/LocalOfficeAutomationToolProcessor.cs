// 文件作用：实现 OfficeHelper 本机 Office/LibreOffice/WPS 自动化检测与高级转换工具。
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using 小工具集合.Models;

namespace 小工具集合.Services;

public sealed partial class ToolProcessor
{
    private static string CheckLocalOfficeEngines()
    {
        IReadOnlyList<LocalOfficeEngine> engines = DetectLocalOfficeEngines();
        var builder = new StringBuilder();
        builder.AppendLine("本机 Office 转换引擎检测：");
        builder.AppendLine();
        foreach (LocalOfficeEngine engine in engines)
        {
            builder.AppendLine($"{(engine.Available ? "[可用]" : "[不可用]")} {engine.Name}");
            builder.AppendLine($"  {engine.Detail}");
            builder.AppendLine($"  能力：{engine.Capabilities}");
        }

        builder.AppendLine("说明：WPS 自动化接口随版本差异较大，仅作为实验支持。");
        return builder.ToString();
    }

    private static string ConvertPdfToWordLocal(ToolRequest request, ToolExecutionContext? context = null)
    {
        string[] files = GetInputFiles(request);
        string outputDirectory = EnsureOutputDirectory(request, files[0]);
        string requestedEngine = GetParameter(request, "engine");
        TimeSpan timeout = GetOfficeTimeout(request);
        var builder = new StringBuilder();
        builder.AppendLine("PDF 转 Word 本机高级：");

        foreach (string file in files)
        {
            context?.WaitIfPaused();
            EnsureExtension(file, ".pdf");
            string outputPath = GetUniqueOutputPath(outputDirectory, Path.GetFileNameWithoutExtension(file), ".docx");
            ConversionAttempt attempt = TryConvertWithEngines(file, outputPath, "docx", requestedEngine, timeout);
            AppendConversionAttempt(builder, file, outputPath, attempt);
        }

        return builder.ToString();
    }

    private static string ConvertOfficeToPdfLocal(ToolRequest request, ToolExecutionContext? context = null)
    {
        string[] files = GetInputFiles(request);
        string outputDirectory = EnsureOutputDirectory(request, files[0]);
        string requestedEngine = GetParameter(request, "engine");
        TimeSpan timeout = GetOfficeTimeout(request);
        var builder = new StringBuilder();
        builder.AppendLine("Office 转 PDF 本机：");

        foreach (string file in files)
        {
            context?.WaitIfPaused();
            EnsureOfficeConvertibleExtension(file);
            string outputPath = GetUniqueOutputPath(outputDirectory, Path.GetFileNameWithoutExtension(file), ".pdf");
            ConversionAttempt attempt = TryConvertWithEngines(file, outputPath, "pdf", requestedEngine, timeout);
            AppendConversionAttempt(builder, file, outputPath, attempt);
        }

        return builder.ToString();
    }

    private static string ConvertOfficeBatch(ToolRequest request, ToolExecutionContext? context = null)
    {
        string[] files = GetInputFiles(request);
        string outputDirectory = EnsureOutputDirectory(request, files[0]);
        string requestedEngine = GetParameter(request, "engine");
        string targetFormat = GetParameter(request, "targetFormat").Equals("DOCX", StringComparison.OrdinalIgnoreCase) ? "docx" : "pdf";
        TimeSpan timeout = GetOfficeTimeout(request);
        var builder = new StringBuilder();
        builder.AppendLine($"Office/PDF 批量转换 -> {targetFormat.ToUpperInvariant()}：");

        foreach (string file in files)
        {
            context?.WaitIfPaused();
            string extension = Path.GetExtension(file).ToLowerInvariant();
            if (targetFormat == "docx" && extension != ".pdf")
            {
                builder.AppendLine($"[跳过] {Path.GetFileName(file)}：DOCX 目标仅支持 PDF 输入。");
                continue;
            }

            if (targetFormat == "pdf" && extension is not (".docx" or ".xlsx" or ".pptx"))
            {
                builder.AppendLine($"[跳过] {Path.GetFileName(file)}：PDF 目标仅支持 docx/xlsx/pptx 输入。");
                continue;
            }

            string outputPath = GetUniqueOutputPath(outputDirectory, Path.GetFileNameWithoutExtension(file), "." + targetFormat);
            ConversionAttempt attempt = TryConvertWithEngines(file, outputPath, targetFormat, requestedEngine, timeout);
            AppendConversionAttempt(builder, file, outputPath, attempt);
        }

        return builder.ToString();
    }

    private static ConversionAttempt TryConvertWithEngines(string inputPath, string outputPath, string targetExtension, string requestedEngine, TimeSpan timeout)
    {
        var failures = new List<string>();
        foreach (string engineId in ResolveEngineOrder(requestedEngine))
        {
            try
            {
                if (engineId == "office")
                {
                    ConvertWithMicrosoftOffice(inputPath, outputPath, targetExtension, timeout);
                }
                else if (engineId == "libreoffice")
                {
                    ConvertWithLibreOffice(inputPath, outputPath, targetExtension, timeout);
                }
                else
                {
                    ConvertWithWpsExperimental(inputPath, outputPath, targetExtension, timeout);
                }

                if (File.Exists(outputPath))
                {
                    return new ConversionAttempt(true, EngineDisplayName(engineId), []);
                }

                failures.Add($"{EngineDisplayName(engineId)}：转换命令完成，但未找到输出文件。");
            }
            catch (Exception ex) when (ex is InvalidOperationException or IOException or Win32Exception or COMException)
            {
                failures.Add($"{EngineDisplayName(engineId)}：{TrimMessage(ex.Message)}");
            }
        }

        return new ConversionAttempt(false, string.Empty, failures);
    }

    private static void ConvertWithMicrosoftOffice(string inputPath, string outputPath, string targetExtension, TimeSpan timeout)
    {
        string extension = Path.GetExtension(inputPath).ToLowerInvariant();
        if (extension == ".pdf" && targetExtension == "docx")
        {
            RunComConversionWithTimeout(() => ConvertWordLikeToDocx("Word.Application", inputPath, outputPath), timeout, "Microsoft Word");
            return;
        }

        if (targetExtension == "pdf")
        {
            if (extension == ".docx")
            {
                RunComConversionWithTimeout(() => ConvertWordLikeToPdf("Word.Application", inputPath, outputPath), timeout, "Microsoft Word");
                return;
            }

            if (extension == ".xlsx")
            {
                RunComConversionWithTimeout(() => ConvertExcelLikeToPdf("Excel.Application", inputPath, outputPath), timeout, "Microsoft Excel");
                return;
            }

            if (extension == ".pptx")
            {
                RunComConversionWithTimeout(() => ConvertPowerPointLikeToPdf("PowerPoint.Application", inputPath, outputPath), timeout, "Microsoft PowerPoint");
                return;
            }
        }

        throw new InvalidOperationException("Microsoft Office 不支持该输入/输出组合。");
    }

    private static void ConvertWithWpsExperimental(string inputPath, string outputPath, string targetExtension, TimeSpan timeout)
    {
        string extension = Path.GetExtension(inputPath).ToLowerInvariant();
        if (extension == ".pdf" && targetExtension == "docx")
        {
            RunComConversionWithTimeout(() => ConvertWordLikeToDocx("KWPS.Application", inputPath, outputPath), timeout, "WPS Writer");
            return;
        }

        if (targetExtension == "pdf" && extension == ".docx")
        {
            RunComConversionWithTimeout(() => ConvertWordLikeToPdf("KWPS.Application", inputPath, outputPath), timeout, "WPS Writer");
            return;
        }

        throw new InvalidOperationException("WPS 实验引擎当前仅尝试 PDF/DOCX 与 Writer 相关转换。");
    }

    private static void ConvertWithLibreOffice(string inputPath, string outputPath, string targetExtension, TimeSpan timeout)
    {
        string? soffice = FindLibreOfficeExecutable();
        if (soffice is null)
        {
            throw new InvalidOperationException("未找到 LibreOffice soffice.exe。");
        }

        string tempDirectory = Path.Combine(Path.GetDirectoryName(outputPath) ?? Environment.CurrentDirectory, ".lcm-office-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        try
        {
            RunProcess(soffice, $"--headless --nologo --nofirststartwizard --convert-to {targetExtension} --outdir {Quote(tempDirectory)} {Quote(inputPath)}", timeout);
            string produced = Path.Combine(tempDirectory, Path.GetFileNameWithoutExtension(inputPath) + "." + targetExtension);
            if (!File.Exists(produced))
            {
                string? first = Directory.GetFiles(tempDirectory, "*." + targetExtension).FirstOrDefault();
                if (first is null)
                {
                    throw new InvalidOperationException("LibreOffice 未生成目标文件。");
                }

                produced = first;
            }

            File.Move(produced, outputPath);
        }
        finally
        {
            try
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private static void ConvertWordLikeToDocx(string progId, string inputPath, string outputPath)
    {
        dynamic? app = null;
        dynamic? document = null;
        try
        {
            app = CreateComApplication(progId);
            app.Visible = false;
            app.DisplayAlerts = 0;
            document = app.Documents.Open(inputPath, ReadOnly: true, AddToRecentFiles: false, Visible: false);
            document.SaveAs2(outputPath, 16);
        }
        finally
        {
            CloseComDocument(document);
            QuitComApplication(app);
        }
    }

    private static void ConvertWordLikeToPdf(string progId, string inputPath, string outputPath)
    {
        dynamic? app = null;
        dynamic? document = null;
        try
        {
            app = CreateComApplication(progId);
            app.Visible = false;
            app.DisplayAlerts = 0;
            document = app.Documents.Open(inputPath, ReadOnly: true, AddToRecentFiles: false, Visible: false);
            document.ExportAsFixedFormat(outputPath, 17);
        }
        finally
        {
            CloseComDocument(document);
            QuitComApplication(app);
        }
    }

    private static void ConvertExcelLikeToPdf(string progId, string inputPath, string outputPath)
    {
        dynamic? app = null;
        dynamic? workbook = null;
        try
        {
            app = CreateComApplication(progId);
            app.Visible = false;
            app.DisplayAlerts = false;
            workbook = app.Workbooks.Open(inputPath, ReadOnly: true);
            workbook.ExportAsFixedFormat(0, outputPath);
        }
        finally
        {
            CloseComDocument(workbook);
            QuitComApplication(app);
        }
    }

    private static void ConvertPowerPointLikeToPdf(string progId, string inputPath, string outputPath)
    {
        dynamic? app = null;
        dynamic? presentation = null;
        try
        {
            app = CreateComApplication(progId);
            presentation = app.Presentations.Open(inputPath, WithWindow: 0);
            presentation.SaveAs(outputPath, 32);
        }
        finally
        {
            CloseComDocument(presentation);
            QuitComApplication(app);
        }
    }

    private static dynamic CreateComApplication(string progId)
    {
        Type? type = Type.GetTypeFromProgID(progId);
        if (type is null)
        {
            throw new InvalidOperationException($"未检测到 COM 引擎：{progId}");
        }

        return Activator.CreateInstance(type) ?? throw new InvalidOperationException($"无法启动 COM 引擎：{progId}");
    }

    private static void RunComConversionWithTimeout(Action action, TimeSpan timeout, string engineName)
    {
        Exception? exception = null;
        using var finished = new ManualResetEventSlim(false);
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
            finally
            {
                finished.Set();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();

        // COM 自动化没有可靠的中断点；超时后返回失败，避免主 UI 无限等待。
        if (!finished.Wait(timeout))
        {
            throw new InvalidOperationException($"{engineName} 转换超过 {timeout.TotalSeconds:0.#} 秒，已放弃等待。若 Office/WPS 留有弹窗，请手动关闭。");
        }

        if (exception is not null)
        {
            throw new InvalidOperationException(exception.Message, exception);
        }
    }

    private static IReadOnlyList<string> ResolveEngineOrder(string requestedEngine)
    {
        if (requestedEngine.Contains("Microsoft", StringComparison.OrdinalIgnoreCase))
        {
            return ["office"];
        }

        if (requestedEngine.Contains("LibreOffice", StringComparison.OrdinalIgnoreCase))
        {
            return ["libreoffice"];
        }

        if (requestedEngine.Contains("WPS", StringComparison.OrdinalIgnoreCase))
        {
            return ["wps"];
        }

        return ["office", "libreoffice", "wps"];
    }

    private static IReadOnlyList<LocalOfficeEngine> DetectLocalOfficeEngines()
    {
        return
        [
            new("office", "Microsoft Office COM", HasComApplication("Word.Application") || HasComApplication("Excel.Application") || HasComApplication("PowerPoint.Application"),
                $"Word: {ComStatus("Word.Application")}；Excel: {ComStatus("Excel.Application")}；PowerPoint: {ComStatus("PowerPoint.Application")}",
                "PDF->DOCX、DOCX/XLSX/PPTX->PDF"),
            new("libreoffice", "LibreOffice headless", FindLibreOfficeExecutable() is { } soffice,
                FindLibreOfficeExecutable() ?? "未在 PATH 或常见安装目录找到 soffice.exe",
                "Office->PDF、PDF->DOCX（效果取决于 LibreOffice）"),
            new("wps", "WPS COM 实验", HasComApplication("KWPS.Application"),
                $"Writer: {ComStatus("KWPS.Application")}；Spreadsheet: {ComStatus("Ket.Application")}；Presentation: {ComStatus("KWPP.Application")}",
                "PDF/DOCX Writer 相关转换，实验支持")
        ];
    }

    private static bool HasComApplication(string progId) => Type.GetTypeFromProgID(progId) is not null;

    private static string ComStatus(string progId) => HasComApplication(progId) ? "可创建" : "未检测到";

    private static string? FindLibreOfficeExecutable()
    {
        try
        {
            string output = RunProcess("where", "soffice", TimeSpan.FromSeconds(3));
            string? path = output.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault(File.Exists);
            if (path is not null)
            {
                return path;
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or IOException)
        {
        }

        string[] candidates =
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "LibreOffice", "program", "soffice.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "LibreOffice", "program", "soffice.exe")
        ];
        return candidates.FirstOrDefault(File.Exists);
    }

    private static void EnsureOfficeConvertibleExtension(string file)
    {
        if (Path.GetExtension(file).ToLowerInvariant() is not (".docx" or ".xlsx" or ".pptx"))
        {
            throw new InvalidOperationException($"文件类型不匹配：{Path.GetFileName(file)} 需要是 docx、xlsx 或 pptx。");
        }
    }

    private static TimeSpan GetOfficeTimeout(ToolRequest request)
    {
        int seconds = Math.Clamp(ParseOptionalInt(request, "timeoutSeconds", 120), 10, 600);
        return TimeSpan.FromSeconds(seconds);
    }

    private static string EngineDisplayName(string engineId) => engineId switch
    {
        "office" => "Microsoft Office",
        "libreoffice" => "LibreOffice",
        "wps" => "WPS 实验",
        _ => engineId
    };

    private static void AppendConversionAttempt(StringBuilder builder, string inputPath, string outputPath, ConversionAttempt attempt)
    {
        if (attempt.Success)
        {
            builder.AppendLine($"[成功] {Path.GetFileName(inputPath)} -> {outputPath}（{attempt.EngineName}）");
            return;
        }

        builder.AppendLine($"[失败] {Path.GetFileName(inputPath)}");
        foreach (string failure in attempt.Failures)
        {
            builder.AppendLine($"  - {failure}");
        }
    }

    private static void CloseComDocument(dynamic? document)
    {
        if (document is null)
        {
            return;
        }

        try
        {
            document.Close(false);
        }
        catch
        {
        }

        ReleaseComObject(document);
    }

    private static void QuitComApplication(dynamic? app)
    {
        if (app is null)
        {
            return;
        }

        try
        {
            app.Quit();
        }
        catch
        {
        }

        ReleaseComObject(app);
    }

    private static void ReleaseComObject(object instance)
    {
        try
        {
            if (Marshal.IsComObject(instance))
            {
                Marshal.FinalReleaseComObject(instance);
            }
        }
        catch
        {
        }
    }

    private sealed record ConversionAttempt(bool Success, string EngineName, IReadOnlyList<string> Failures);

    private sealed record LocalOfficeEngine(string Id, string Name, bool Available, string Detail, string Capabilities);
}
