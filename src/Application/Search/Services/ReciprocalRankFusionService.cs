using EquipFlow.Application.Search.Models;

namespace EquipFlow.Application.Search.Services;

public sealed class ReciprocalRankFusionService
{
    public IReadOnlyList<RetrievedChunk> Fuse(
        IReadOnlyList<RetrievedChunk> vectorResults,
        IReadOnlyList<RetrievedChunk> keywordResults,
        int k = 60)
    {
        if (k < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(k), k, "RRF k must be greater than 0.");
        }

        var fusedScores = new Dictionary<Guid, double>();
        var preferredChunks = new Dictionary<Guid, RetrievedChunk>();

        AddRankedResults(vectorResults, k, fusedScores, preferredChunks);
        AddRankedResults(keywordResults, k, fusedScores, preferredChunks);

        return fusedScores
            .OrderByDescending(entry => entry.Value)
            .Select(entry =>
            {
                var chunk = preferredChunks[entry.Key];
                return new RetrievedChunk(
                    chunk.ChunkId,
                    chunk.DocumentId,
                    chunk.Content,
                    entry.Value,
                    chunk.Citation);
            })
            .ToList();
    }

    private static void AddRankedResults(
        IReadOnlyList<RetrievedChunk> results,
        int k,
        IDictionary<Guid, double> fusedScores,
        IDictionary<Guid, RetrievedChunk> preferredChunks)
    {
        for (var index = 0; index < results.Count; index++)
        {
            var chunk = results[index];
            var rank = index + 1;
            fusedScores.TryGetValue(chunk.ChunkId, out var currentScore);
            fusedScores[chunk.ChunkId] = currentScore + 1.0 / (k + rank);

            if (!preferredChunks.TryGetValue(chunk.ChunkId, out var preferredChunk)
                || chunk.Score > preferredChunk.Score)
            {
                preferredChunks[chunk.ChunkId] = chunk;
            }
        }
    }
}