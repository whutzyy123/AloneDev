using Microsoft.Extensions.DependencyInjection;
using PMTool.Core.Abstractions;
using PMTool.Infrastructure.Data;
using PMTool.Infrastructure.Diagnostics;
using PMTool.Infrastructure.Export;
using PMTool.Infrastructure.Storage;

namespace PMTool.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPmToolInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IDataRootProvider>(_ =>
        {
            var initial = DataRootPaths.TryReadAnchorEffectiveRoot() ?? DataRootPaths.DefaultDocumentsDataRoot();
            return new MutableDataRootProvider(initial);
        });
        services.AddSingleton<ICurrentAccountContext, CurrentAccountContext>();
        services.AddSingleton<IErrorLogger, FileErrorLogger>();
        services.AddSingleton<IAccountCatalogRepository, AccountCatalogStore>();
        services.AddSingleton<IDatabaseConnectionFactory, SqliteConnectionFactory>();
        services.AddSingleton<ISqliteConnectionHolder, SqliteConnectionHolder>();
        services.AddSingleton<IIteration1ProbeRepository, Iteration1ProbeRepository>();
        services.AddSingleton<IProjectRepository, ProjectRepository>();
        services.AddSingleton<IProjectDeletionGuard, ProjectDeletionGuard>();
        services.AddSingleton<IFeatureRepository, FeatureRepository>();
        services.AddSingleton<IFeatureDeletionGuard, FeatureDeletionGuard>();
        services.AddSingleton<ITaskRepository, TaskRepository>();
        services.AddSingleton<IReleaseRepository, ReleaseRepository>();
        services.AddSingleton<IDocumentRepository, DocumentRepository>();
        services.AddSingleton<IDocumentImageStorage, DocumentImageStorage>();
        services.AddSingleton<IIdeaRepository, IdeaRepository>();
        services.AddSingleton<IGlobalSearchRepository, GlobalSearchRepository>();
        services.AddSingleton<IConfigAnchorStore, ConfigAnchorStore>();
        services.AddSingleton<IAppConfigStore, AppConfigStore>();
        services.AddSingleton<IDataRootMigrationService, DataRootMigrationService>();
        services.AddSingleton<IAccountBackupService, AccountBackupService>();
        services.AddSingleton<IDataExportService, DataExportService>();
        return services;
    }
}
