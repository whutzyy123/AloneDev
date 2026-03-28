using PMTool.Core.Abstractions;

namespace PMTool.Infrastructure.Storage;

public sealed class MutableDataRootProvider : IDataRootProvider
{
    private string _root;

    public MutableDataRootProvider(string initialAbsoluteRoot)
    {
        _root = Path.GetFullPath(initialAbsoluteRoot);
    }

    public string GetDataRootPath() => _root;

    public void SetDataRootPath(string absolutePath) =>
        _root = Path.GetFullPath(absolutePath);
}
