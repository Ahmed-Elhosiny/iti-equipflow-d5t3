namespace EquipFlow.Application.Ports;

public interface IDocumentExtractor
{
    Task<string> ExtractTextAsync(
        Stream fileStream,
        string fileName,
        CancellationToken cancellationToken);
}