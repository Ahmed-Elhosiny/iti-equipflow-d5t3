using EquipFlow.Application.Options;
using EquipFlow.Application.Ports;
using Microsoft.Extensions.Options;

namespace EquipFlow.Infrastructure.LLM;

public sealed class BudgetAwareModelRouter : IModelRouter
{
    private readonly OpenAIOptions _openAiOptions;

    public BudgetAwareModelRouter(IOptions<OpenAIOptions> openAiOptions)
    {
        _openAiOptions = openAiOptions.Value;
    }

    public Task<ModelRoute?> GetCheaperModelAsync(
        decimal currentPricePerThousandTokens, 
        CancellationToken cancellationToken = default)
    {
        // Cascade: Expensive -> gpt-4o-mini -> Ollama (Free)
        if (currentPricePerThousandTokens > 0.001m)
        {
            var miniPrice = _openAiOptions.ModelPricing.TryGetValue("gpt-4o-mini", out var pricing) 
                ? pricing.Prompt 
                : 0.00015m;
            return Task.FromResult<ModelRoute?>(new ModelRoute("gpt-4o-mini", miniPrice));
        }

        if (currentPricePerThousandTokens > 0m)
        {
            return Task.FromResult<ModelRoute?>(new ModelRoute("Ollama", 0m));
        }

        return Task.FromResult<ModelRoute?>(null);
    }
}