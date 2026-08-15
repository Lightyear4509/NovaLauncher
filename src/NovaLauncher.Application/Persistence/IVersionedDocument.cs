namespace NovaLauncher.Application.Persistence;

public interface IVersionedDocument
{
    int SchemaVersion { get; }
}
