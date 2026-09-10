namespace EquipFlow.Application.Ports;

public interface IEmbeddingPort
{
    Task<ReadOnlyMemory<float>[]> GenerateEmbeddingsAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken);
}