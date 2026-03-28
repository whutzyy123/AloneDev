using System.Data.Common;

namespace PMTool.Core.Abstractions;

/// <summary>当前账号下单一 SQLite 连接生命周期；换账号时关闭并重开。</summary>
public interface ISqliteConnectionHolder
{
    /// <summary>释放旧连接并按 <see cref="ICurrentAccountContext"/> 打开当前库；并确保迭代占位表存在。</summary>
    Task CloseAndReopenForCurrentAccountAsync(CancellationToken cancellationToken = default);

    /// <summary>在同一会话连接上执行工作（串行化访问）。</summary>
    Task<T> UseConnectionAsync<T>(Func<DbConnection, CancellationToken, Task<T>> work,
        CancellationToken cancellationToken = default);

    /// <summary>关闭连接、独占访问数据库文件（复制/替换）、结束后重新打开并迁移。</summary>
    Task RunExclusiveOnDatabaseFileAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken = default);
}
