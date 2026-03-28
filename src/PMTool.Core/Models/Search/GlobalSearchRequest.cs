namespace PMTool.Core.Models.Search;

public sealed record GlobalSearchRequest(
    string NormalizedNeedle,
    GlobalSearchScope Scope,
    int PerModuleLimit = 120);
