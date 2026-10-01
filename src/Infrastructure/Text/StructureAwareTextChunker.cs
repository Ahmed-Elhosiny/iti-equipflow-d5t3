using System.Text;
using System.Text.RegularExpressions;
using EquipFlow.Application.Ports;

namespace EquipFlow.Infrastructure.Text;

public sealed partial class StructureAwareTextChunker : ITextChunker
{
    // Regex to match Markdown headers: # Header 1, ## Header 2, etc.
    [GeneratedRegex(@"^(#{1,6})\s+(.+)$", RegexOptions.Multiline)]
    private static partial Regex HeaderRegex();

    public IReadOnlyList<ChunkResult> ChunkText(
        string text,
        int maxChunkSize = 500,
        int overlap = 50)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (maxChunkSize <= 0) throw new ArgumentOutOfRangeException(nameof(maxChunkSize));
        if (overlap < 0 || overlap >= maxChunkSize) throw new ArgumentOutOfRangeException(nameof(overlap));
        if (text.Length == 0) return [];

        var chunks = new List<ChunkResult>();
        var blocks = SplitByHeaders(text);

        foreach (var block in blocks)
        {
            var blockChunks = ChunkSingleBlock(block.Text, block.Section, maxChunkSize, overlap);
            chunks.AddRange(blockChunks);
        }

        return chunks;
    }

    private static List<(string Text, string? Section)> SplitByHeaders(string text)
    {
        var blocks = new List<(string Text, string? Section)>();
        var lines = text.Split('\n');
        
        var currentSection = (string?)null;
        var currentText = new StringBuilder();

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');
            var match = HeaderRegex().Match(line);
            
            if (match.Success)
            {
                if (currentText.Length > 0)
                {
                    blocks.Add((currentText.ToString().Trim(), currentSection));
                    currentText.Clear();
                }
                currentSection = match.Groups[2].Value.Trim();
            }
            else
            {
                currentText.AppendLine(line);
            }
        }

        if (currentText.Length > 0)
        {
            blocks.Add((currentText.ToString().Trim(), currentSection));
        }

        return blocks;
    }

    private static List<ChunkResult> ChunkSingleBlock(string text, string? section, int maxChunkSize, int overlap)
    {
        var chunks = new List<ChunkResult>();
        if (string.IsNullOrWhiteSpace(text)) return chunks;

        var start = 0;
        var previousEnd = -1;
        
        // Context prefix for the chunk based on section
        var contextPrefix = string.IsNullOrEmpty(section) ? "" : $"[Section: {section}]\n";

        while (start < text.Length)
        {
            var limit = Math.Min(start + maxChunkSize, text.Length);
            var end = FindLastSentenceBoundary(text, start, limit, previousEnd);

            if (end == -1)
            {
                end = HasSentenceBoundaryAfter(text, limit) ? limit : FindLastWordBoundary(text, start, limit, previousEnd);
            }

            if (end <= start || end <= previousEnd)
            {
                end = limit;
            }

            var chunkText = text[start..end].Trim();
            if (!string.IsNullOrWhiteSpace(chunkText))
            {
                chunks.Add(new ChunkResult(contextPrefix + chunkText, section));
            }

            if (end == text.Length) break;

            previousEnd = end;
            start = end - overlap;
            if (start < 0) start = 0;
        }

        return chunks;
    }

    private static int FindLastSentenceBoundary(string text, int start, int limit, int previousEnd)
    {
        for (var index = limit - 1; index >= start; index--)
        {
            if (index + 1 > previousEnd && IsSentenceTerminator(text[index]))
                return index + 1;
        }
        return -1;
    }

    private static bool HasSentenceBoundaryAfter(string text, int limit)
    {
        for (var index = limit; index < text.Length; index++)
        {
            if (IsSentenceTerminator(text[index])) return true;
        }
        return false;
    }

    private static int FindLastWordBoundary(string text, int start, int limit, int previousEnd)
    {
        for (var index = limit - 1; index > start; index--)
        {
            if (index + 1 > previousEnd && char.IsWhiteSpace(text[index]))
                return index + 1;
        }
        return limit;
    }

    private static bool IsSentenceTerminator(char character) => character is '.' or '!' or '?';
}