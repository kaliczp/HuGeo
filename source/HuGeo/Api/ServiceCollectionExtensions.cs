using HuGeo.Core.Transformations;
using HuGeo.DataAccess.Repository;
using Microsoft.Extensions.DependencyInjection;

namespace HuGeo.Api;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHuGeo(this IServiceCollection services) =>
        AddHuGeo(services, TransformationMode.OfficialGrid);

    public static IServiceCollection AddHuGeo(this IServiceCollection services, TransformationMode mode)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<GridDataRepository>();
        services.AddSingleton<CoordinateTransformer>(serviceProvider =>
        {
            var repository = serviceProvider.GetRequiredService<GridDataRepository>();
            var transformer = new CoordinateTransformer(repository, mode);
            transformer.InitializeAsync().GetAwaiter().GetResult();
            return transformer;
        });
        services.AddSingleton<ICoordinateTransformer>(serviceProvider =>
            serviceProvider.GetRequiredService<CoordinateTransformer>());
        services.AddSingleton<ILegacyCoordinateTransformer>(serviceProvider =>
            serviceProvider.GetRequiredService<CoordinateTransformer>());

        return services;
    }
}
