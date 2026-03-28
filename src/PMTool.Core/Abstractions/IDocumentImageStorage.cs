namespace PMTool.Core.Abstractions;

/// <summary>将图片写入当前账户目录 <c>Data/Images</c>，返回相对账户根的路径（如 <c>Images/xxx.png</c>）。</summary>
public interface IDocumentImageStorage
{
    /// <param name="maxBytes">PRD 单文件上限（如 50MB）。</param>
    Task<string> SaveForDocumentAsync(
        string documentId,
        byte[] imageBytes,
        string extensionHint,
        long maxBytes,
        CancellationToken cancellationToken = default);
}
