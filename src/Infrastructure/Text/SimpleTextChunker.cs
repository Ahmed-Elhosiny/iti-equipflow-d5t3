using EquipFlow.Application.Ports;

namespace EquipFlow.Infrastructure.Text;

public sealed class SimpleTextChunker : ITextChunker
{
    public IReadOnlyList<string> ChunkText(
        string text,
        int maxChunkSize = 500,
        int overlap = 50)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (maxChunkSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxChunkSize), "Chunk size must be greater than zero.");
        }

        if (overlap < 0 || overlap >= maxChunkSize)
        {
            throw new ArgumentOutOfRangeException(nameof(overlap), "Overlap must be non-negative and smaller than the chunk size.");
        }

        if (text.Length == 0)
        {
            return [];
        }

        var chunks = new List<string>();
        var start = 0;
        var previousEnd = -1;

        while (start < text.Length)
        {
            var limit = Math.Min(start + maxChunkSize, text.Length);
            var end = FindLastSentenceBoundary(text, start, limit, previousEnd);

            if (end == -1)
            {
                end = HasSentenceBoundaryAfter(text, limit)
                    ? limit
                    : FindLastWordBoundary(text, start, limit, previousEnd);
            }

            if (end <= start || end <= previousEnd)
            {
                end = limit;
            }

            chunks.Add(text[start..end]);

            if (end == text.Length)
            {
                break;
            }

            previousEnd = end;
            start = end - overlap;
        }

        return chunks;
    }

    private static int FindLastSentenceBoundary(
        string text,
        int start,
        int limit,
        int previousEnd)
    {
        for (var index = limit - 1; index >= start; index--)
        {
            if (index + 1 > previousEnd && IsSentenceTerminator(text[index]))
            {
                return index + 1;
            }
        }

        return -1;
    }

    private static bool HasSentenceBoundaryAfter(string text, int limit)
    {
        for (var index = limit; index < text.Length; index++)
        {
            if (IsSentenceTerminator(text[index]))
            {
                return true;
            }
        }

        return false;
    }

    private static int FindLastWordBoundary(string text, int start, int limit, int previousEnd)
    {
        for (var index = limit - 1; index > start; index--)
        {
            if (index + 1 > previousEnd && char.IsWhiteSpace(text[index]))
            {
                return index + 1;
            }
        }

        return limit;
    }

    private static bool IsSentenceTerminator(char character) =>
        character is '.' or '!' or '?';
}