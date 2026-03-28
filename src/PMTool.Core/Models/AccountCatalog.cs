namespace PMTool.Core.Models;

/// <summary>应用级账号目录（存于公共 LocalAppData，非各账号 pmtool.db）。</summary>
public sealed class AccountCatalog
{
    public List<string> Accounts { get; set; } = [];
    public string LastSelectedAccount { get; set; } = "";
}
