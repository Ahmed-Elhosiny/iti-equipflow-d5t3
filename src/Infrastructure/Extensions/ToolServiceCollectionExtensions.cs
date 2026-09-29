using EquipFlow.Application.Tools.Ports;
using EquipFlow.Infrastructure.Tools;
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
    /// Registers the tool executors and dispatcher used by the EquipFlow agent tool system.
    /// </summary>
    /// <remarks>Dispatcher execution chain: Core -> Validation (TL-006) -> Authorization (TL-007/AG-003).</remarks>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance so additional registrations can be chained.</returns>
    public static IServiceCollection AddEquipFlowTools(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Dispatchers
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
        
        // Registry
        services.AddSingleton<IAgentToolRegistry, StaticAgentToolRegistry>();

        // Tool Executors
        
        // Register SearchManualsToolExecutor once and map both interfaces to the same scoped instance.
        // This prevents duplicate DI registrations and ensures the real hybrid retrieval is used (FR-4).
        services.AddScoped<SearchManualsToolExecutor>();
        services.AddScoped<IToolExecutor>(sp => sp.GetRequiredService<SearchManualsToolExecutor>());
        services.AddScoped<ISearchManualsTool>(sp => sp.GetRequiredService<SearchManualsToolExecutor>());

        services.AddScoped<IToolExecutor, QueryFaultHistoryExecutor>();
        services.AddScoped<IToolExecutor, GetEquipmentSpecsExecutor>();
        services.AddScoped<IToolExecutor, GenerateSafetyChecklistExecutor>();
        services.AddScoped<IToolExecutor, DraftWorkOrderExecutor>();
        services.AddScoped<IToolExecutor, CreateWorkOrderExecutor>(); 
        services.AddScoped<IToolExecutor, ValidateBudgetExecutor>();
        services.AddScoped<IToolExecutor, CheckApprovalStatusExecutor>();
        services.AddScoped<IToolExecutor, EmitAgentEventExecutor>();

        return services;
    }
}