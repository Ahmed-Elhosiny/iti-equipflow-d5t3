using System.Security.Cryptography;
using System.Text;

namespace EquipFlow.Application.Features.Documents.Commands.IngestDocument;

/// <summary>
/// Computes a deterministic content hash used to enforce idempotent document ingestion (FR-010).
/// </summary>
public static class DocumentContentHasher
{
    /// <summary>
    /// Computes a SHA-256 hash of the supplied content and returns it as a lowercase hex string.
    /// </summary>
    public static string ComputeHash(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}