using PMTool.Core.IO;
using Xunit;

namespace PMTool.Tests.IO;

public sealed class PathSecurityTests
{
    [Fact]
    public void Child_file_under_parent_is_within()
    {
        var root = Path.Combine(Path.GetTempPath(), "ps-" + Guid.NewGuid().ToString("n")[..8]);
        var sub = Path.Combine(root, "Backup");
        try
        {
            Directory.CreateDirectory(sub);
            var file = Path.Combine(sub, "a.db");
            File.WriteAllText(file, "x");
            Assert.True(PathSecurity.IsPathWithinDirectory(root, file));
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public void Sibling_directory_is_not_within_avoids_prefix_trap()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), "ps2-" + Guid.NewGuid().ToString("n")[..8]);
        var account = Path.Combine(baseDir, "Account");
        var accountEvil = Path.Combine(baseDir, "AccountEvil");
        var good = Path.Combine(account, "f.db");
        var bad = Path.Combine(accountEvil, "f.db");
        try
        {
            Directory.CreateDirectory(account);
            Directory.CreateDirectory(accountEvil);
            File.WriteAllText(good, "1");
            File.WriteAllText(bad, "2");
            Assert.True(PathSecurity.IsPathWithinDirectory(account, good));
            Assert.False(PathSecurity.IsPathWithinDirectory(account, bad));
        }
        finally
        {
            TryDelete(baseDir);
        }
    }

    [Fact]
    public void Parent_dotdot_escape_is_not_within()
    {
        var root = Path.Combine(Path.GetTempPath(), "ps3-" + Guid.NewGuid().ToString("n")[..8]);
        var nested = Path.Combine(root, "a", "b");
        var outside = Path.Combine(root, "outside");
        try
        {
            Directory.CreateDirectory(nested);
            Directory.CreateDirectory(outside);
            var escapeCandidate = Path.GetFullPath(Path.Combine(nested, "..", "..", "outside", "x.txt"));
            File.WriteAllText(escapeCandidate, "x");
            Assert.False(PathSecurity.IsPathWithinDirectory(nested, escapeCandidate));
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public void Same_directory_path_is_within()
    {
        var root = Path.Combine(Path.GetTempPath(), "ps4-" + Guid.NewGuid().ToString("n")[..8]);
        try
        {
            Directory.CreateDirectory(root);
            Assert.True(PathSecurity.IsPathWithinDirectory(root, root));
        }
        finally
        {
            TryDelete(root);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // test cleanup
        }
    }
}
