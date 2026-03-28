namespace PMTool.Core.Models.DataManagement;

[Flags]
public enum DataExportModule
{
    None = 0,
    Projects = 1,
    Features = 2,
    Tasks = 4,
    Releases = 8,
    Documents = 16,
    Ideas = 32,
    All = Projects | Features | Tasks | Releases | Documents | Ideas,
}
