// 文件作用：保留 ToolCatalog 的统一类型入口；具体分组元数据拆分在 Services/Catalog 下的 partial 文件中。
using 小工具集合.Models;

namespace 小工具集合.Services;

/// <summary>
/// 应用中所有工具的统一注册入口。
/// 分组列表、兼容查找和各分组工厂方法分散在 Services/Catalog 的 partial 文件里。
/// </summary>
public static partial class ToolCatalog
{
}
