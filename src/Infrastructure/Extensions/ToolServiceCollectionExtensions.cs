using EquipFlow.Application.Tools.Ports;
using EquipFlow.Infrastructure.Tools.Dispatcher;
using EquipFlow.Infrastructure.Tools.Executors;
using Microsoft.Extensions.DependencyInjection;

namespace EquipFlow.Infrastructure.Extensions;

/// <summary>
/// Provides dependency injection registration helpers for EquipFlow tool infrastructure.
/// </summary>
public static class ToolServiceCollectionExtensions
{
    /// <summary>
    /// Registers the read-only tool executors and dispatcher used by the EquipFlow agent tool system.
    /// This wires the dispatcher to the available read-only executor implementations without registering
    /// any write or control tools yet.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance so additional registrations can be chained.</returns>
    public static IServiceCollection AddEquipFlowTools(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IToolDispatcher, ToolDispatcher>();
        services.AddScoped<IToolExecutor, SearchManualsExecutor>();
        services.AddScoped<IToolExecutor, QueryFaultHistoryExecutor>();
        services.AddScoped<IToolExecutor, GetEquipmentSpecsExecutor>();
        services.AddScoped<IToolExecutor, GenerateSafetyChecklistExecutor>();

        return services;
    }
}
