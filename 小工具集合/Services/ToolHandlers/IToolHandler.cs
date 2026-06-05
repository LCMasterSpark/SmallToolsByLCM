// 文件作用：定义工具执行 handler 抽象，让 ToolProcessor 只负责路由、异常包装和暂停协调。
using 小工具集合.Models;

namespace 小工具集合.Services.ToolHandlers;

public interface IToolHandler
{
    IReadOnlyCollection<string> ToolIds { get; }
    bool IsPausable { get; }
    string Execute(ToolRequest request, ToolExecutionContext? context = null);
}

public sealed class DelegateToolHandler : IToolHandler
{
    private readonly Func<ToolRequest, ToolExecutionContext?, string> execute;

    public DelegateToolHandler(IEnumerable<string> toolIds, bool isPausable, Func<ToolRequest, ToolExecutionContext?, string> execute)
    {
        ToolIds = toolIds.ToArray();
        IsPausable = isPausable;
        this.execute = execute;
    }

    public IReadOnlyCollection<string> ToolIds { get; }

    public bool IsPausable { get; }

    public string Execute(ToolRequest request, ToolExecutionContext? context = null)
    {
        return execute(request, context);
    }
}

public sealed class ToolHandlerRegistry
{
    private readonly Dictionary<string, IToolHandler> handlers;

    public ToolHandlerRegistry(IEnumerable<IToolHandler> handlers)
    {
        this.handlers = handlers
            .SelectMany(handler => handler.ToolIds.Select(id => (id, handler)))
            .ToDictionary(pair => pair.id, pair => pair.handler, StringComparer.Ordinal);
    }

    public bool IsPausable(string toolId)
    {
        return handlers.TryGetValue(toolId, out IToolHandler? handler) && handler.IsPausable;
    }

    public IToolHandler GetRequired(string toolId)
    {
        return handlers.TryGetValue(toolId, out IToolHandler? handler)
            ? handler
            : throw new NotSupportedException("暂不支持该工具。");
    }
}
