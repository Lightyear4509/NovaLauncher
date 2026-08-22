using NovaLauncher.Application.Persistence;
using NovaLauncher.Domain.Profiles;

namespace NovaLauncher.Application.Profiles;

public sealed class ProfileCoordinator(IDocumentStore<ProfilesDocument> store, TimeProvider timeProvider) : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private ProfilesDocument _document = ProfilesDocument.CreateDefault(timeProvider.GetUtcNow());

    public IReadOnlyList<LocalProfile> Profiles => _document.Profiles;

    public Guid ActiveProfileId => _document.ActiveProfileId;

    public LocalProfile ActiveProfile => Profiles.Single(profile => profile.Id == ActiveProfileId);

    public event EventHandler? ActiveProfileChanged;

    public void Dispose() => _gate.Dispose();

    public async Task<DocumentLoadResult<ProfilesDocument>> LoadAsync(CancellationToken cancellationToken)
    {
        var result = await store.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (result.Status == DocumentLoadStatus.NotFound)
        {
            var created = ProfilesDocument.CreateDefault(timeProvider.GetUtcNow());
            var save = await store.SaveAsync(created, cancellationToken).ConfigureAwait(false);
            if (save.Status == DocumentSaveStatus.Saved) _document = created;
            return new(save.Status == DocumentSaveStatus.Saved ? DocumentLoadStatus.Loaded : DocumentLoadStatus.Unrecoverable,
                save.Status == DocumentSaveStatus.Saved ? created : null, save.Error);
        }
        if (result.Document is not null) _document = result.Document;
        return result;
    }

    public Task<DocumentSaveResult> CreateAsync(string name, CancellationToken cancellationToken)
    {
        var normalized = name?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 100)
            return Task.FromResult(new DocumentSaveResult(DocumentSaveStatus.Failed, "Profile name must contain 1–100 characters."));
        return MutateAsync(document =>
        {
            if (document.Profiles.Any(profile => string.Equals(profile.Name, normalized, StringComparison.OrdinalIgnoreCase))) return null;
            var now = timeProvider.GetUtcNow();
            return document with { Profiles = document.Profiles.Append(new LocalProfile(Guid.NewGuid(), normalized, now, now)).ToArray() };
        }, cancellationToken);
    }

    public Task<DocumentSaveResult> RenameAsync(Guid id, string name, CancellationToken cancellationToken)
    {
        var normalized = name?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 100)
            return Task.FromResult(new DocumentSaveResult(DocumentSaveStatus.Failed, "Profile name must contain 1–100 characters."));
        return MutateAsync(document =>
        {
            var index = document.Profiles.ToList().FindIndex(profile => profile.Id == id);
            if (index < 0 || document.Profiles.Any(profile => profile.Id != id && string.Equals(profile.Name, normalized, StringComparison.OrdinalIgnoreCase))) return null;
            var profiles = document.Profiles.ToArray();
            profiles[index] = profiles[index] with { Name = normalized, UpdatedAtUtc = timeProvider.GetUtcNow() };
            return document with { Profiles = profiles };
        }, cancellationToken);
    }

    public Task<DocumentSaveResult> SwitchAsync(Guid id, CancellationToken cancellationToken) => MutateAsync(
        document => document.Profiles.Any(profile => profile.Id == id) ? document with { ActiveProfileId = id } : null,
        cancellationToken,
        notifySwitch: true);

    public Task<DocumentSaveResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id == LocalProfileDefaults.DefaultProfileId || id == ActiveProfileId)
            return Task.FromResult(new DocumentSaveResult(DocumentSaveStatus.Failed, "The default or active profile cannot be deleted."));
        return MutateAsync(document =>
        {
            var profiles = document.Profiles.Where(profile => profile.Id != id).ToArray();
            return profiles.Length == document.Profiles.Count ? null : document with { Profiles = profiles };
        }, cancellationToken);
    }

    public Task<DocumentSaveResult> AddDiscoveryLocationAsync(string path, CancellationToken cancellationToken)
    {
        var normalized = NormalizeLocalPath(path, requireExisting: true);
        if (normalized is null) return Task.FromResult(new DocumentSaveResult(DocumentSaveStatus.Failed, "Choose an existing absolute local discovery folder."));
        return UpdateActiveProfileAsync(profile =>
        {
            var locations = (profile.DiscoveryLocations ?? []).Append(normalized).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            return locations.Length > 50 ? null : profile with { DiscoveryLocations = locations, UpdatedAtUtc = timeProvider.GetUtcNow() };
        }, cancellationToken);
    }

    public Task<DocumentSaveResult> RemoveDiscoveryLocationAsync(string path, CancellationToken cancellationToken) =>
        UpdateActiveProfileAsync(profile =>
        {
            var locations = (profile.DiscoveryLocations ?? []).Where(item => !string.Equals(item, path, StringComparison.OrdinalIgnoreCase)).ToArray();
            return profile with { DiscoveryLocations = locations.Length == 0 ? null : locations, UpdatedAtUtc = timeProvider.GetUtcNow() };
        }, cancellationToken);

    public Task<DocumentSaveResult> AddIgnoredPathAsync(string path, CancellationToken cancellationToken)
    {
        var normalized = NormalizeLocalPath(path, requireExisting: false);
        if (normalized is null) return Task.FromResult(new DocumentSaveResult(DocumentSaveStatus.Failed, "Choose an absolute local path to ignore."));
        return UpdateActiveProfileAsync(profile =>
        {
            var ignored = (profile.IgnoredPaths ?? []).Append(normalized).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            return ignored.Length > 200 ? null : profile with { IgnoredPaths = ignored, UpdatedAtUtc = timeProvider.GetUtcNow() };
        }, cancellationToken);
    }

    public Task<DocumentSaveResult> RemoveIgnoredPathAsync(string path, CancellationToken cancellationToken) =>
        UpdateActiveProfileAsync(profile =>
        {
            var ignored = (profile.IgnoredPaths ?? []).Where(item => !string.Equals(item, path, StringComparison.OrdinalIgnoreCase)).ToArray();
            return profile with { IgnoredPaths = ignored.Length == 0 ? null : ignored, UpdatedAtUtc = timeProvider.GetUtcNow() };
        }, cancellationToken);

    private Task<DocumentSaveResult> UpdateActiveProfileAsync(
        Func<LocalProfile, LocalProfile?> update,
        CancellationToken cancellationToken) => MutateAsync(document =>
        {
            var profiles = document.Profiles.ToArray();
            var index = Array.FindIndex(profiles, profile => profile.Id == document.ActiveProfileId);
            if (index < 0) return null;
            var updated = update(profiles[index]);
            if (updated is null) return null;
            profiles[index] = updated;
            return document with { Profiles = profiles };
        }, cancellationToken);

    private static string? NormalizeLocalPath(string path, bool requireExisting)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path)) return null;
        try
        {
            var normalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
            if (normalized.StartsWith("\\\\", StringComparison.Ordinal) || normalized.StartsWith("\\\\?\\", StringComparison.Ordinal) ||
                requireExisting && !Directory.Exists(normalized)) return null;
            return normalized;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    private async Task<DocumentSaveResult> MutateAsync(
        Func<ProfilesDocument, ProfilesDocument?> mutation,
        CancellationToken cancellationToken,
        bool notifySwitch = false)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var staged = mutation(_document);
            if (staged is null) return new(DocumentSaveStatus.Failed, "The profile operation is invalid or conflicts with an existing profile.");
            var save = await store.SaveAsync(staged, cancellationToken).ConfigureAwait(false);
            if (save.Status == DocumentSaveStatus.Saved)
            {
                var changed = staged.ActiveProfileId != _document.ActiveProfileId;
                _document = staged;
                if (notifySwitch && changed) ActiveProfileChanged?.Invoke(this, EventArgs.Empty);
            }
            return save;
        }
        finally { _gate.Release(); }
    }
}
