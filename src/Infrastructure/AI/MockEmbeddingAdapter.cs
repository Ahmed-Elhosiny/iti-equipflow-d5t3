using EquipFlow.Application.Ports;
using Microsoft.Extensions.Logging;

namespace EquipFlow.Infrastructure.AI;

public sealed class MockEmbeddingAdapter(ILogger<MockEmbeddingAdapter> logger) : IEmbeddingPort
{
    public Task<ReadOnlyMemory<float>[]> GenerateEmbeddingsAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("MockEmbeddingAdapter: Generating dummy embeddings for {Count} texts.", texts.Count);
        
        // text-embedding-3-small uses 1536 dimensions
        var dummyEmbedding = new float[1536]; 
        var result = new ReadOnlyMemory<float>[texts.Count];
        
        for (int i = 0; i < texts.Count; i++)
        {
            result[i] = new ReadOnlyMemory<float>(dummyEmbedding);
        }
        
        return Task.FromResult(result);
    }
}