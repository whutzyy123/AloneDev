namespace PMTool.Core.Models.Search;

[System.Flags]
public enum GlobalSearchScope
{
    None = 0,
    Projects = 1 << 0,
    Features = 1 << 1,
    Tasks = 1 << 2,
    Documents = 1 << 3,
    Ideas = 1 << 4,

    All = Projects | Features | Tasks | Documents | Ideas,
}
