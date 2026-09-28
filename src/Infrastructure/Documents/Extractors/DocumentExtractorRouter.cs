using EquipFlow.Application.Ports;

namespace EquipFlow.Infrastructure.Documents.Extractors;

/// <summary>
/// Routes document extraction requests to the appropriate format-specific extractor
/// based on the file extension.
/// </summary>
public sealed class DocumentExtractorRouter : IDocumentExtractor
{
    private readonly PdfDocumentExtractor _pdfExtractor;
    private readonly DocxDocumentExtractor _docxExtractor;

    public DocumentExtractorRouter(
        PdfDocumentExtractor pdfExtractor,
        DocxDocumentExtractor docxExtractor)
    {
        _pdfExtractor = pdfExtractor;
        _docxExtractor = docxExtractor;
    }

    public Task<string> ExtractTextAsync(
        Stream fileStream,
        string fileName,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fileStream);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var extension = Path.GetExtension(fileName)?.ToLowerInvariant();

        return extension switch
        {
            ".pdf" => _pdfExtractor.ExtractTextAsync(fileStream, fileName, cancellationToken),
            ".docx" => _docxExtractor.ExtractTextAsync(fileStream, fileName, cancellationToken),
            _ => throw new NotSupportedException($"Document format '{extension}' is not supported. Only .pdf and .docx are allowed.")
        };
    }
}