using DocumentFormat.OpenXml.Packaging;
using EquipFlow.Application.Ports;

namespace EquipFlow.Infrastructure.Documents.Extractors;

public sealed class DocxDocumentExtractor : IDocumentExtractor
{
    public async Task<string> ExtractTextAsync(
        Stream fileStream,
        string fileName,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fileStream);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        try
        {
            using var documentStream = new MemoryStream();
            await fileStream.CopyToAsync(documentStream, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            ValidateDocxSignature(documentStream, fileName);
            documentStream.Position = 0;

            using var document = WordprocessingDocument.Open(documentStream, false);
            return document.MainDocumentPart?.Document?.Body?.InnerText ?? string.Empty;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new InvalidDataException($"Unable to extract text from DOCX '{fileName}'.", exception);
        }
    }

    private static void ValidateDocxSignature(Stream documentStream, string fileName)
    {
        Span<byte> signature = stackalloc byte[4];
        if (documentStream.Read(signature) != signature.Length ||
            signature[0] != 0x50 ||
            signature[1] != 0x4B ||
            signature[2] != 0x03 ||
            signature[3] != 0x04)
        {
            throw new InvalidDataException($"The file '{fileName}' is not a valid DOCX.");
        }
    }
}