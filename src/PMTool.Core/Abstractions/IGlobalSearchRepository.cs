using PMTool.Core.Models.Search;

namespace PMTool.Core.Abstractions;

/// <summary>
/// 全局关键词搜索（本地 SQLite）。当前产品实现为各表字段上的 LIKE 子串匹配；文档正文仅扫描前缀以控制读取成本。
/// 若需进一步提升深度（整篇正文稳定命中、相关性排序、更大结果集），可另立迭代评估 SQLite FTS5 虚拟表（触发器或应用层与业务表同步）
/// 或对用户指定的离线目录建立本地文本索引；二者均显著增加 schema 迁移与维护成本，需在 PRD 中单独约定范围与性能预算。
/// </summary>
public interface IGlobalSearchRepository
{
    Task<GlobalSearchResponse> SearchAsync(GlobalSearchRequest request, CancellationToken cancellationToken = default);
}
