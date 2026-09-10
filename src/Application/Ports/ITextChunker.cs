namespace EquipFlow.Application.Ports;

public interface ITextChunker
{
    IReadOnlyList<string> ChunkText(
        string text,
        int maxChunkSize = 500,
        int overlap = 50);
}