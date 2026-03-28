using System.Data;
using System.Data.Common;
using Microsoft.Data.Sqlite;
using PMTool.Core.Abstractions;

namespace PMTool.Infrastructure.Data;

public sealed class SqliteConnectionHolder(
    IDatabaseConnectionFactory connectionFactory) : ISqliteConnectionHolder
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private SqliteConnection? _connection;

    public async Task CloseAndReopenForCurrentAccountAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await DisposeConnectionAsync().ConfigureAwait(false);
            await ReopenCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<T> UseConnectionAsync<T>(Func<DbConnection, CancellationToken, Task<T>> work,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureOpenCoreAsync(cancellationToken).ConfigureAwait(false);
            return await work(_connection!, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RunExclusiveOnDatabaseFileAsync(Func<CancellationToken, Task> work,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await DisposeConnectionAsync().ConfigureAwait(false);
            await work(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            try
            {
                await ReopenCoreAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // 尽力恢复连接；原始异常已由 work 抛出
            }

            _gate.Release();
        }
    }

    private async Task ReopenCoreAsync(CancellationToken cancellationToken)
    {
        var cs = connectionFactory.CreateConnectionString();
        var conn = new SqliteConnection(cs);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnableForeignKeysAsync(conn, cancellationToken).ConfigureAwait(false);
        await DatabaseBootstrap.EnsureAsync(conn, cancellationToken).ConfigureAwait(false);
        _connection = conn;
    }

    private async Task EnsureOpenCoreAsync(CancellationToken cancellationToken)
    {
        if (_connection?.State == ConnectionState.Open)
        {
            return;
        }

        await DisposeConnectionAsync().ConfigureAwait(false);
        await ReopenCoreAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task EnableForeignKeysAsync(SqliteConnection conn, CancellationToken cancellationToken)
    {
        await using var pragma = conn.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON;";
        _ = await pragma.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task DisposeConnectionAsync()
    {
        if (_connection is null)
        {
            return;
        }

        await _connection.DisposeAsync().ConfigureAwait(false);
        _connection = null;
    }
}
