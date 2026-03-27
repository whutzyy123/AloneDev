using Microsoft.Extensions.DependencyInjection;
using PMTool.Application.Abstractions;
using PMTool.Application.Services;

namespace PMTool.Application.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPmToolApplication(this IServiceCollection services)
    {
        services.AddSingleton<IAppInitializationService, AppInitializationService>();
        return services;
    }
}
