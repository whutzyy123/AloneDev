using PMTool.Core.Abstractions;

namespace PMTool.Infrastructure.Storage;

public sealed class DocumentImageStorage(ICurrentAccountContext accountContext) : IDocumentImageStorage
{
    public Task<string> SaveForDocumentAsync(
        string documentId,
        byte[] imageBytes,
        string extensionHint,
        long maxBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);
        ArgumentNullException.ThrowIfNull(imageBytes);
        if (imageBytes.Length == 0)
        {
            throw new ArgumentException("图片数据为空。", nameof(imageBytes));
        }

        if (imageBytes.Length > maxBytes)
        {
            throw new InvalidOperationException(
                $"图片超过允许大小（最大 {maxBytes / (1024 * 1024)} MB）。请选择较小的图片。");
        }

        var ext = NormalizeExtension(extensionHint);
        var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff", System.Globalization.CultureInfo.InvariantCulture);
        var fileName = $"{documentId}_{stamp}{ext}";
        var relative = Path.Combine("Images", fileName).Replace('\\', '/');
        var root = accountContext.GetAccountDirectoryPath();
        var dir = Path.Combine(root, "Images");
        Directory.CreateDirectory(dir);
        var full = Path.Combine(dir, fileName);

        return WriteAllBytesAsync(full, imageBytes, relative, cancellationToken);
    }

    private static async Task<string> WriteAllBytesAsync(
        string fullPath,
        byte[] bytes,
        string relative,
        CancellationToken cancellationToken)
    {
        try
        {
            await File.WriteAllBytesAsync(fullPath, bytes, cancellationToken).ConfigureAwait(false);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new InvalidOperationException("无法写入图片：目录无写入权限或文件被占用。", ex);
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException("无法写入图片：磁盘错误或路径不可访问。", ex);
        }

        return relative;
    }

    private static string NormalizeExtension(string extensionHint)
    {
        var e = (extensionHint ?? ".png").Trim();
        if (e.Length == 0)
        {
            return ".png";
        }

        if (!e.StartsWith(".", StringComparison.Ordinal))
        {
            e = "." + e;
        }

        return e.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" or ".bmp" => e.ToLowerInvariant(),
            _ => ".png",
        };
    }
}
