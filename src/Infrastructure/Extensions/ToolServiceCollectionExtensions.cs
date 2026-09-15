using EquipFlow.Application.Tools.Ports;
using EquipFlow.Infrastructure.Tools.Dispatcher;
using EquipFlow.Infrastructure.Tools.Executors;
using EquipFlow.Infrastructure.Tools.Registry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
    /// <remarks>Dispatcher execution chain: Core -> Validation (TL-006) -> Authorization (TL-007/AG-003).</remarks>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance so additional registrations can be chained.</returns>
    public static IServiceCollection AddEquipFlowTools(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<ToolDispatcher>();
        services.AddScoped<ValidatingToolDispatcher>(sp =>
        {
            var innerDispatcher = sp.GetRequiredService<ToolDispatcher>();
            var logger = sp.GetRequiredService<ILogger<ValidatingToolDispatcher>>();
            return new ValidatingToolDispatcher(innerDispatcher, logger);
        });
        services.AddScoped<IToolDispatcher>(sp =>
        {
            var innerDispatcher = sp.GetRequiredService<ValidatingToolDispatcher>();
            var agentToolRegistry = sp.GetRequiredService<IAgentToolRegistry>();
            var logger = sp.GetRequiredService<ILogger<AuthorizingToolDispatcher>>();
            return new AuthorizingToolDispatcher(innerDispatcher, agentToolRegistry, logger);
        });
        services.AddSingleton<IAgentToolRegistry, StaticAgentToolRegistry>();
        services.AddScoped<IToolExecutor, SearchManualsExecutor>();
        services.AddScoped<IToolExecutor, QueryFaultHistoryExecutor>();
        services.AddScoped<IToolExecutor, GetEquipmentSpecsExecutor>();
        services.AddScoped<IToolExecutor, GenerateSafetyChecklistExecutor>();

        return services;
    }
}
