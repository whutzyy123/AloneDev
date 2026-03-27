using PMTool.Infrastructure.Storage;
using Xunit;

namespace PMTool.Tests.Storage;

public sealed class DataRootProviderTests
{
    [Fact]
    public void GetDataRootPath_contains_pmproject_and_data_segment()
    {
        var sut = new DataRootProvider();
        var path = sut.GetDataRootPath();
        Assert.Contains("PMProjectTool", path, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("Data", Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar)), StringComparison.OrdinalIgnoreCase);
    }
}
