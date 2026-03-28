using System.Data.Common;
using PMTool.Core.Abstractions;
using PMTool.Core.Models;

namespace PMTool.Infrastructure.Data;

public sealed class Iteration1ProbeRepository(ISqliteConnectionHolder holder) : IIteration1ProbeRepository
{
    public async Task InsertMarkerAsync(string payload, CancellationToken cancellationToken = default)
    {
        _ = await holder.UseConnectionAsync(async (db, ct) =>
        {
            await using var cmd = db.CreateCommand();
            cmd.CommandText =
                """
                INSERT INTO iteration1_account_probe (id, payload, created_at)
                VALUES ($id, $payload, $created_at);
                """;
            AddParameter(cmd, "$id", Guid.NewGuid().ToString("D"));
            AddParameter(cmd, "$payload", payload);
            AddParameter(cmd, "$created_at", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture));
            return await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<Iteration1ProbeRow>> ListMarkersAsync(CancellationToken cancellationToken = default)
    {
        return holder.UseConnectionAsync(async (db, ct) =>
        {
            await using var cmd = db.CreateCommand();
            cmd.CommandText =
                """
                SELECT id, payload, created_at FROM iteration1_account_probe ORDER BY created_at;
                """;
            var list = new List<Iteration1ProbeRow>();
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                list.Add(new Iteration1ProbeRow
                {
                    Id = reader.GetString(0),
                    Payload = reader.GetString(1),
                    CreatedAt = reader.GetString(2),
                });
            }

            return (IReadOnlyList<Iteration1ProbeRow>)list;
        }, cancellationToken);
    }

    private static void AddParameter(DbCommand cmd, string name, string value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value;
        cmd.Parameters.Add(p);
    }
}
