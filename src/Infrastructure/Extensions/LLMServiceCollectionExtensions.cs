using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EquipFlow.Infrastructure.Extensions;

/// <summary>
/// Provides dependency injection registrations for LLM providers.
/// </summary>
public static class LLMServiceCollectionExtensions
{
    /// <summary>
    /// Registers the configured LLM providers and their supporting services.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The configured service collection.</returns>
    public static IServiceCollection AddLLMProviders(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHttpClient();
        services.Configure<EquipFlow.Application.Options.OpenAIOptions>(
            configuration.GetSection("LLM:OpenAI"));
        services.Configure<EquipFlow.Application.Options.OllamaOptions>(
            configuration.GetSection("LLM:Ollama"));
        services.AddKeyedSingleton<EquipFlow.Application.Ports.LLM.ILLMGenerationPort, EquipFlow.Infrastructure.LLM.MockLLMGenerationAdapter>("Mock");
        services.AddKeyedSingleton<EquipFlow.Application.Ports.LLM.ILLMGenerationPort, EquipFlow.Infrastructure.LLM.OpenAILLMGenerationAdapter>("OpenAI");
        services.AddKeyedSingleton<EquipFlow.Application.Ports.LLM.ILLMGenerationPort, EquipFlow.Infrastructure.LLM.OllamaLLMGenerationAdapter>("Ollama");

        return services;
    }
}