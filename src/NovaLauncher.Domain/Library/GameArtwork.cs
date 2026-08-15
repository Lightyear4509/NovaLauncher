namespace NovaLauncher.Domain.Library;

public enum ArtworkKind
{
    Cover,
    Hero,
    Logo,
    Background,
}

public sealed record ArtworkReference(
    ArtworkKind Kind,
    string Location,
    MetadataProvenance Provenance,
    bool IsPlaceholder);

public sealed record GameArtwork(
    ArtworkReference Cover,
    ArtworkReference Hero,
    ArtworkReference Logo,
    ArtworkReference Background);
