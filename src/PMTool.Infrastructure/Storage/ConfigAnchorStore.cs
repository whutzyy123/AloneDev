using System.Text.Json;
using PMTool.Core.Abstractions;

namespace PMTool.Infrastructure.Storage;

public sealed class ConfigAnchorStore : IConfigAnchorStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public Task<string?> GetEffectiveDataRootAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(DataRootPaths.TryReadAnchorEffectiveRoot());
    }

    public async Task SetEffectiveDataRootAsync(string absolutePath, CancellationToken cancellationToken = default)
    {
        var dir = DataRootPaths.LocalAloneDevDir();
        _ = Directory.CreateDirectory(dir);
        var full = Path.GetFullPath(absolutePath);
        var dto = new { EffectiveDataRoot = full };
        await File.WriteAllTextAsync(
                DataRootPaths.AnchorFilePath(),
                JsonSerializer.Serialize(dto, JsonOptions),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
