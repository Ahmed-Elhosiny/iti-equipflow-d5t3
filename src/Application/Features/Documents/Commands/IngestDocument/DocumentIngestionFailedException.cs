namespace EquipFlow.Application.Features.Documents.Commands.IngestDocument;

public sealed class DocumentIngestionFailedException : Exception
{
    public DocumentIngestionFailedException(string fileName, Exception innerException)
        : base($"Document ingestion failed for '{fileName}'.", innerException)
    {
    }
}