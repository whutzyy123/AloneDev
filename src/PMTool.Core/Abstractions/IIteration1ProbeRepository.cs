using PMTool.Core.Models;

namespace PMTool.Core.Abstractions;

/// <summary>迭代 1 占位表仓储，用于验收两账号数据隔离（不同物理库文件）。</summary>
public interface IIteration1ProbeRepository
{
    Task InsertMarkerAsync(string payload, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Iteration1ProbeRow>> ListMarkersAsync(CancellationToken cancellationToken = default);
}
