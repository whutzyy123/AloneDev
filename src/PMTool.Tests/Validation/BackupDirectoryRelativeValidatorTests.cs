using PMTool.Core.Validation;
using Xunit;

namespace PMTool.Tests.Validation;

public sealed class BackupDirectoryRelativeValidatorTests
{
    [Theory]
    [InlineData("Backup")]
    [InlineData("Backup/2024")]
    [InlineData(@"Sub\Dir")]
    public void Valid_paths_accepted(string input)
    {
        var n = BackupDirectoryRelativeValidator.NormalizeAndValidate(input);
        Assert.False(string.IsNullOrEmpty(n));
    }

    [Theory]
    [InlineData("../x")]
    [InlineData("..")]
    [InlineData("a/../b")]
    [InlineData(@"C:\abs")]
    public void Invalid_paths_throw(string input)
    {
        Assert.Throws<ArgumentException>(() => BackupDirectoryRelativeValidator.NormalizeAndValidate(input));
    }
}
