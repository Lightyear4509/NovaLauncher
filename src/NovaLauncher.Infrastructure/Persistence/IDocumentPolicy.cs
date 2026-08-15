using NovaLauncher.Application.Persistence;

namespace NovaLauncher.Infrastructure.Persistence;

public interface IDocumentPolicy<TDocument>
    where TDocument : class, IVersionedDocument
{
    string FileName { get; }

    int CurrentSchemaVersion { get; }

    string? Validate(TDocument document);
}
