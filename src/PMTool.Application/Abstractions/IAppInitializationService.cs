namespace PMTool.Application.Abstractions;

/// <summary>Runs one-time startup work (directories, migrations) without UI types.</summary>
public interface IAppInitializationService
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
