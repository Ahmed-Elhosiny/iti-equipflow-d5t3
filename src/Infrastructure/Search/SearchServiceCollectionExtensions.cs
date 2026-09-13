using EquipFlow.Application.Search.Ports;
using EquipFlow.Application.Search.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EquipFlow.Infrastructure.Search;

public static class SearchServiceCollectionExtensions
{
    public static IServiceCollection AddRagSearchInfrastructure(
        this IServiceCollection services)
    {
        services.AddScoped<IVectorSearchPort, PgVectorSearchAdapter>();
        services.AddScoped<IKeywordSearchPort, PostgresKeywordSearchAdapter>();
        services.AddSingleton<ReciprocalRankFusionService>();
        services.AddScoped<IRerankerPort, NoOpReranker>();

        return services;
    }
}
