using NovaLauncher.Application.Persistence;
using NovaLauncher.Domain.Library;
using NovaLauncher.Domain.Profiles;

namespace NovaLauncher.Application.Library;

public sealed class CollectionCoordinator(IDocumentStore<CollectionsDocument> store, TimeProvider timeProvider) : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private GameCollection[] _collections = [];
    private GameCollection[] _allCollections = [];
    private Guid _activeProfileId = LocalProfileDefaults.DefaultProfileId;

    public IReadOnlyList<GameCollection> Collections => _collections;

    public void Dispose() => _gate.Dispose();

    public async Task<DocumentLoadResult<CollectionsDocument>> LoadAsync(CancellationToken cancellationToken)
    {
        var result = await store.LoadAsync(cancellationToken).ConfigureAwait(false);
        _allCollections = result.Document?.Collections.ToArray() ?? [];
        RefreshActiveCollections();
        return result;
    }

    public void SetActiveProfile(Guid profileId)
    {
        if (profileId == Guid.Empty) throw new ArgumentException("The active profile ID cannot be empty.", nameof(profileId));
        _activeProfileId = profileId;
        RefreshActiveCollections();
    }

    public async Task<DocumentSaveResult> CreateAsync(string name, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200)
        {
            return new DocumentSaveResult(DocumentSaveStatus.Failed, "Collection name is required and cannot exceed 200 characters.");
        }

        return await MutateAsync(current =>
        {
            var now = timeProvider.GetUtcNow();
            return current.Append(new GameCollection(GameCollectionId.New(), name.Trim(), [], now, now, _activeProfileId)).ToArray();
        }, cancellationToken).ConfigureAwait(false);
    }

    public Task<DocumentSaveResult> RenameAsync(GameCollectionId id, string name, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200)
        {
            return Task.FromResult(new DocumentSaveResult(DocumentSaveStatus.Failed, "Collection name is invalid."));
        }

        return UpdateAsync(id, collection => collection with { Name = name.Trim(), UpdatedAtUtc = timeProvider.GetUtcNow() }, cancellationToken);
    }

    public async Task<DocumentSaveResult> DeleteAsync(GameCollectionId id, CancellationToken cancellationToken)
    {
        return await MutateAsync(current =>
        {
            var staged = current.Where(collection => collection.Id != id).ToArray();
            return staged.Length == current.Length ? null : staged;
        }, cancellationToken).ConfigureAwait(false);
    }

    public Task<DocumentSaveResult> SetMembershipAsync(
        GameCollectionId id,
        GameId gameId,
        bool isMember,
        CancellationToken cancellationToken) =>
        UpdateAsync(id, collection =>
        {
            var ids = collection.GameIds.Where(existing => existing != gameId).ToList();
            if (isMember)
            {
                ids.Add(gameId);
            }

            return collection with { GameIds = ids, UpdatedAtUtc = timeProvider.GetUtcNow() };
        }, cancellationToken);

    public Task<DocumentSaveResult> SetMembershipsAsync(
        GameCollectionId id,
        IReadOnlyCollection<GameId> gameIds,
        bool isMember,
        CancellationToken cancellationToken)
    {
        var changedIds = gameIds.Distinct().ToHashSet();
        if (changedIds.Count is 0 or > 10_000)
            return Task.FromResult(new DocumentSaveResult(DocumentSaveStatus.Failed, "Select between 1 and 10,000 games."));
        return UpdateAsync(id, collection =>
        {
            var ids = collection.GameIds.Where(existing => !changedIds.Contains(existing)).ToList();
            if (isMember) ids.AddRange(changedIds);
            return collection with { GameIds = ids, UpdatedAtUtc = timeProvider.GetUtcNow() };
        }, cancellationToken);
    }

    public Task<DocumentSaveResult> ReplaceGameReferenceAsync(
        GameId duplicateId,
        GameId survivorId,
        CancellationToken cancellationToken) =>
        MutateAsync(current => current.Select(collection =>
        {
            if (!collection.GameIds.Contains(duplicateId)) return collection;
            var ids = collection.GameIds.Where(id => id != duplicateId && id != survivorId).Append(survivorId).ToArray();
            return collection with { GameIds = ids, UpdatedAtUtc = timeProvider.GetUtcNow() };
        }).ToArray(), cancellationToken);

    private async Task<DocumentSaveResult> UpdateAsync(
        GameCollectionId id,
        Func<GameCollection, GameCollection> update,
        CancellationToken cancellationToken)
    {
        return await MutateAsync(current =>
        {
            var index = Array.FindIndex(current, collection => collection.Id == id);
            if (index < 0)
            {
                return null;
            }

            var staged = current.ToArray();
            staged[index] = update(staged[index]);
            return staged;
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task<DocumentSaveResult> MutateAsync(
        Func<GameCollection[], GameCollection[]?> createStaged,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var staged = createStaged(_collections);
            if (staged is null)
            {
                return new DocumentSaveResult(DocumentSaveStatus.Failed, "Collection not found.");
            }

            var allStaged = _allCollections.Where(collection => EffectiveProfileId(collection) != _activeProfileId).Concat(staged).ToArray();
            var result = await store.SaveAsync(
                new CollectionsDocument(CollectionsDocument.CurrentSchemaVersion, allStaged),
                cancellationToken).ConfigureAwait(false);
            if (result.Status == DocumentSaveStatus.Saved)
            {
                _collections = staged;
                _allCollections = allStaged;
            }

            return result;
        }
        finally
        {
            _gate.Release();
        }
    }

    private void RefreshActiveCollections() => _collections = _allCollections
        .Where(collection => EffectiveProfileId(collection) == _activeProfileId)
        .ToArray();

    private static Guid EffectiveProfileId(GameCollection collection) => collection.ProfileId ?? LocalProfileDefaults.DefaultProfileId;
}
