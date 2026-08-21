namespace NovaLauncher.Application.Persistence;

public interface IVersionedDocument
{
    int SchemaVersion { get; }
}

public interface IDocumentMigrator<TDocument> where TDocument : class, IVersionedDocument
{
    TDocument? Migrate(TDocument document);
}
