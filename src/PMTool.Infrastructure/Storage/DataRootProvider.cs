using PMTool.Core.Abstractions;

namespace PMTool.Infrastructure.Storage;

public sealed class DataRootProvider : IDataRootProvider
{
    public string GetDataRootPath()
    {
        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Path.Combine(docs, "PMProjectTool", "Data");
    }
}
