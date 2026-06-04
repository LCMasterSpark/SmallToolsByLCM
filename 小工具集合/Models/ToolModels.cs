namespace 小工具集合.Models;

/// <summary>
/// 描述工具参数在 WPF 参数区中应该渲染成哪种控件。
/// </summary>
public enum ToolParameterKind
{
    Text,
    Password,
    Multiline,
    Combo,
    FileOpen,
    FileSave,
    FileList,
    Directory,
    Number,
    CheckBox,
    ReadOnly
}

/// <summary>
/// 左侧导航栏中展示的一级工具分组。
/// </summary>
public sealed class ToolGroup
{
    public required string Name { get; init; }
    public required string Icon { get; init; }
    public required IReadOnlyList<ToolDefinition> Tools { get; init; }
}

/// <summary>
/// 单个工具的静态元数据。运行时通过工具 Id 和所选操作交给 ToolProcessor 执行。
/// </summary>
public sealed class ToolDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string GroupName { get; init; }
    public required IReadOnlyList<ToolOperation> Operations { get; init; }
    public IReadOnlyList<ToolParameterDefinition> Parameters { get; init; } = [];
    public string Warning { get; init; } = string.Empty;
    public string InputWatermark { get; init; } = "请输入文本";
    public bool RequiresInput { get; init; } = true;
    public string InteractiveViewKey { get; init; } = string.Empty;
}

/// <summary>
/// 工具下可选择的操作，例如编码/解码、格式化/压缩。
/// </summary>
public sealed class ToolOperation
{
    public required string Id { get; init; }
    public required string Name { get; init; }
}

/// <summary>
/// 参数的静态元数据，UI 会根据它生成对应的输入控件。
/// </summary>
public sealed class ToolParameterDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public ToolParameterKind Kind { get; init; } = ToolParameterKind.Text;
    public string DefaultValue { get; init; } = string.Empty;
    public IReadOnlyList<string> Options { get; init; } = [];
}

/// <summary>
/// 与参数定义配套的运行时取值。
/// </summary>
public sealed class ToolParameterValue
{
    public required ToolParameterDefinition Definition { get; init; }
    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// ViewModel 调用处理器前组装出的不可变执行请求。
/// </summary>
public sealed class ToolRequest
{
    public required string ToolId { get; init; }
    public required string OperationId { get; init; }
    public required string Input { get; init; }
    public required IReadOnlyDictionary<string, string> Parameters { get; init; }
}

/// <summary>
/// 所有工具统一返回的执行结果，ViewModel 不需要了解每个工具内部的错误处理细节。
/// </summary>
public sealed class ToolResult
{
    public bool Success { get; init; }
    public string Output { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;

    public static ToolResult Ok(string output, string message = "完成") => new()
    {
        Success = true,
        Output = output,
        Message = message
    };

    public static ToolResult Fail(string message) => new()
    {
        Success = false,
        Message = message
    };
}
