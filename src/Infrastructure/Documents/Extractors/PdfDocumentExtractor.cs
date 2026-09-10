using System.Text;
using EquipFlow.Application.Ports;
using UglyToad.PdfPig;

namespace EquipFlow.Infrastructure.Documents.Extractors;

public sealed class PdfDocumentExtractor : IDocumentExtractor
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

            ValidatePdfSignature(documentStream, fileName);
            documentStream.Position = 0;

            using var pdfDocument = PdfDocument.Open(documentStream);
            var text = new StringBuilder();

            foreach (var page in pdfDocument.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (text.Length > 0)
                {
                    text.AppendLine();
                }

                text.Append(page.Text);
            }

            return text.ToString();
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
            throw new InvalidDataException($"Unable to extract text from PDF '{fileName}'.", exception);
        }
    }

    private static void ValidatePdfSignature(Stream documentStream, string fileName)
    {
        Span<byte> signature = stackalloc byte[5];
        if (documentStream.Read(signature) != signature.Length ||
            !signature.SequenceEqual("%PDF-"u8))
        {
            throw new InvalidDataException($"The file '{fileName}' is not a valid PDF.");
        }
    }
}