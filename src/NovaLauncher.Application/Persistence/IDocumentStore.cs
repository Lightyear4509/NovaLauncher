namespace NovaLauncher.Application.Persistence;

public interface IDocumentStore<TDocument>
    where TDocument : class, IVersionedDocument
{
    Task<DocumentLoadResult<TDocument>> LoadAsync(CancellationToken cancellationToken);

    Task<DocumentSaveResult> SaveAsync(TDocument document, CancellationToken cancellationToken);
}
