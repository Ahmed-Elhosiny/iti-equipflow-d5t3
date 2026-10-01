namespace EquipFlow.Application.Ports;

public record ChunkResult(string Text, string? Section);

public interface ITextChunker
{
    IReadOnlyList<ChunkResult> ChunkText(
        string text,
        int maxChunkSize = 500,
        int overlap = 50);
}