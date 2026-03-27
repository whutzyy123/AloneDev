using Microsoft.Extensions.DependencyInjection;
using PMTool.Core.Abstractions;
using PMTool.Infrastructure.Data;
using PMTool.Infrastructure.Diagnostics;
using PMTool.Infrastructure.Storage;

namespace PMTool.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPmToolInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IDataRootProvider, DataRootProvider>();
        services.AddSingleton<ICurrentAccountContext, CurrentAccountContext>();
        services.AddSingleton<IErrorLogger, FileErrorLogger>();
        services.AddSingleton<IDatabaseConnectionFactory, SqliteConnectionFactory>();
        return services;
    }
}
