using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NovaLauncher.Application;
using NovaLauncher.Application.Library;
using NovaLauncher.Application.Launching;
using NovaLauncher.Application.Persistence;
using NovaLauncher.Application.Steam;
using NovaLauncher.Application.Enrichment;
using NovaLauncher.Infrastructure.Enrichment;
using NovaLauncher.Infrastructure.Logging;
using NovaLauncher.Infrastructure.Launching;
using NovaLauncher.Infrastructure.Persistence;
using NovaLauncher.Infrastructure.Steam;
using NovaLauncher.Application.Achievements;
using NovaLauncher.Infrastructure.Achievements;
using System.Security.Cryptography;
using System.Text;
using NovaLauncher.Application.Themes;
using NovaLauncher.Application.SaveSync;
using NovaLauncher.Infrastructure.SaveSync;

namespace NovaLauncher.App;

internal static class Program
{
    private static readonly Action<ILogger, Exception?> LogStarting = LoggerMessage.Define(
        LogLevel.Information,
        new EventId(1, "Starting"),
        "Starting NovaLauncher shell");

    private static readonly Action<ILogger, Exception?> LogUnexpectedTermination = LoggerMessage.Define(
        LogLevel.Critical,
        new EventId(2, "UnexpectedTermination"),
        "NovaLauncher terminated unexpectedly");

    public static IServiceProvider Services { get; private set; } = null!;

    [STAThread]
    public static int Main(string[] args)
    {
        var smokeTest = args.Contains("--smoke-test", StringComparer.Ordinal);
        var steamImportDiagnostic = args.Contains("--steam-import-diagnostic", StringComparer.Ordinal);
        using var serviceProvider = ConfigureServices(smokeTest || steamImportDiagnostic);
        Services = serviceProvider;

        var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Bootstrap");
        LogStarting(logger, null);

        try
        {
            if (steamImportDiagnostic)
            {
                return RunSteamImportDiagnosticAsync(serviceProvider).GetAwaiter().GetResult();
            }

            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception exception)
        {
            LogUnexpectedTermination(logger, exception);
            return 1;
        }
    }

    private static async Task<int> RunSteamImportDiagnosticAsync(IServiceProvider services)
    {
        var library = services.GetRequiredService<LibraryCoordinator>();
        var load = await library.LoadAsync(CancellationToken.None).ConfigureAwait(false);
        if (load.Document is null && load.Status != DocumentLoadStatus.NotFound)
        {
            Console.WriteLine($"library-load={load.Status} error={load.Warning}");
            return 2;
        }

        var importer = services.GetRequiredService<SteamImportCoordinator>();
        var preview = await importer.PreviewAsync(null, CancellationToken.None).ConfigureAwait(false);
        Console.WriteLine($"preview add={preview.Added} update={preview.Updated} unchanged={preview.Unchanged} skipped={preview.Failures.Count}");
        foreach (var failure in preview.Failures) Console.WriteLine($"skip path={failure.Path} reason={failure.Reason}");
        var commit = await importer.CommitAsync(CancellationToken.None).ConfigureAwait(false);
        Console.WriteLine($"commit status={commit.Status} imported={commit.Imported} error={commit.Error}");
        return commit.Status == SteamImportCommitStatus.Saved ? 0 : 3;
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont();

    private static ServiceProvider ConfigureServices(bool smokeTest)
    {
        var testDataRoot = smokeTest
            ? Environment.GetEnvironmentVariable("NOVALAUNCHER_TEST_DATA_ROOT")
            : null;
        var dataRoot = string.IsNullOrWhiteSpace(testDataRoot)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NovaLauncher")
            : Path.GetFullPath(testDataRoot);
        var logPath = Path.Combine(
            dataRoot,
            "Logs",
            "novalauncher.jsonl");

        var services = new ServiceCollection();
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<IThemeHost, AvaloniaThemeHost>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<ManualGameDraftValidator>();
        services.AddSingleton<LibraryCoordinator>();
        services.AddSingleton<CollectionCoordinator>();
        services.AddSingleton<LibraryWorkspaceViewModel>();
        services.AddSingleton<IGameEnrichmentService, GameEnrichmentService>();
        services.AddSingleton<IGameIdentityService, GameIdentityService>();
        services.AddSingleton<IApiKeySession>(_ => new ApiKeySession(
            Environment.GetEnvironmentVariable("NOVALAUNCHER_STEAMGRIDDB_API_KEY")));
        var steamApiKey = Environment.GetEnvironmentVariable("NOVALAUNCHER_STEAM_WEB_API_KEY");
        var steamId = Environment.GetEnvironmentVariable("NOVALAUNCHER_STEAM_ID");
        var accountFingerprint = string.IsNullOrWhiteSpace(steamId)
            ? "unconfigured-account"
            : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(steamId))).ToLowerInvariant();
        services.AddSingleton<IAchievementProvider>(provider => new SteamAchievementProvider(
            provider.GetRequiredService<IBoundedHttpClient>(),
            provider.GetRequiredService<TimeProvider>(),
            steamApiKey,
            steamId));
        services.AddSingleton<IAchievementService>(provider => new AchievementService(
            provider.GetServices<IAchievementProvider>(),
            provider.GetRequiredService<IDocumentStore<AchievementsDocument>>(),
            provider.GetRequiredService<LibraryCoordinator>(),
            provider.GetRequiredService<TimeProvider>(),
            accountFingerprint));
        services.AddSingleton(provider => new ProviderCache<MetadataSnapshot[]>(
            provider.GetRequiredService<TimeProvider>(), TimeSpan.FromHours(24), TimeSpan.FromDays(30), 10_000));
        services.AddSingleton(provider => new ProviderCache<ArtworkCandidate[]>(
            provider.GetRequiredService<TimeProvider>(), TimeSpan.FromHours(24), TimeSpan.FromDays(30), 10_000));
        services.AddSingleton<IMetadataProvider, SteamMetadataProvider>();
        services.AddSingleton<IArtworkProvider, SteamArtworkProvider>();
        services.AddSingleton<IArtworkProvider>(provider => SteamGridDbArtworkProvider.FromSession(
            provider.GetRequiredService<IBoundedHttpClient>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<IApiKeySession>()));
        services.AddSingleton<IGameIdentitySearchProvider, SteamGridDbIdentitySearchProvider>();
        services.AddHttpClient("Providers", client => client.Timeout = TimeSpan.FromSeconds(10))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = false,
                AutomaticDecompression = System.Net.DecompressionMethods.None,
            });
        services.AddSingleton<IBoundedHttpClient>(provider => new BoundedHttpClient(
            provider.GetRequiredService<IHttpClientFactory>().CreateClient("Providers"),
            provider.GetRequiredService<TimeProvider>()));
        services.AddSingleton(provider => new ManagedArtworkMaterializer(
            provider.GetRequiredService<IBoundedHttpClient>(),
            provider.GetRequiredService<IAtomicFileSystem>(),
            Path.Combine(dataRoot, "Cache", "Artwork")));
        services.AddSingleton<IArtworkMaterializer>(provider => provider.GetRequiredService<ManagedArtworkMaterializer>());
        services.AddSingleton<IManualCoverService>(provider => provider.GetRequiredService<ManagedArtworkMaterializer>());
        services.AddSingleton<SteamImportCoordinator>();
        services.AddSingleton<ISteamCatalogSource, SteamCatalogSource>();
        services.AddSingleton<ISteamRegistryReader, WindowsSteamRegistryReader>();
        services.AddSingleton<ISteamFileSystem, PhysicalSteamFileSystem>();
        services.AddSingleton<IGameLauncher, SafeGameLauncher>();
        services.AddSingleton<WindowsCredentialPairingSecretStore>();
        services.AddSingleton<IPairingSecretStore>(provider => provider.GetRequiredService<WindowsCredentialPairingSecretStore>());
        services.AddSingleton<IPeerCredentialStore>(provider => provider.GetRequiredService<WindowsCredentialPairingSecretStore>());
        services.AddSingleton<ISaveSyncTransport>(provider => new TailscaleTcpTransport(
            () => provider.GetRequiredService<ISaveSyncService>().Settings,
            provider.GetRequiredService<IPairingSecretStore>(),
            peerCredentials: provider.GetRequiredService<IPeerCredentialStore>()));
        services.AddSingleton<ISaveSyncService>(provider => new SaveSyncCoordinator(
            provider.GetRequiredService<IDocumentStore<SaveSyncDocument>>(),
            provider.GetRequiredService<ISaveSyncTransport>(),
            provider.GetRequiredService<IPairingSecretStore>(),
            provider.GetRequiredService<TimeProvider>(),
            dataRoot,
            peerCredentials: provider.GetRequiredService<IPeerCredentialStore>()));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IAtomicFileSystem, PhysicalAtomicFileSystem>();
        services.AddSingleton<IDocumentStore<GamesDocument>>(provider =>
            DocumentStoreFactory.CreateGamesStore(
                dataRoot,
                provider.GetRequiredService<IAtomicFileSystem>(),
                provider.GetRequiredService<TimeProvider>()));
        services.AddSingleton<IDocumentStore<CollectionsDocument>>(provider =>
            DocumentStoreFactory.CreateCollectionsStore(
                dataRoot,
                provider.GetRequiredService<IAtomicFileSystem>(),
                provider.GetRequiredService<TimeProvider>()));
        services.AddSingleton<IDocumentStore<SettingsDocument>>(provider =>
            DocumentStoreFactory.CreateSettingsStore(
                dataRoot,
                provider.GetRequiredService<IAtomicFileSystem>(),
                provider.GetRequiredService<TimeProvider>()));
        services.AddSingleton<IDocumentStore<AchievementsDocument>>(provider =>
            DocumentStoreFactory.CreateAchievementsStore(
                dataRoot,
                provider.GetRequiredService<IAtomicFileSystem>(),
                provider.GetRequiredService<TimeProvider>()));
        services.AddSingleton<IDocumentStore<SaveSyncDocument>>(provider =>
            DocumentStoreFactory.CreateSaveSyncStore(
                dataRoot,
                provider.GetRequiredService<IAtomicFileSystem>(),
                provider.GetRequiredService<TimeProvider>()));
        services.AddSingleton<IBackupArchiveService>(provider =>
            new BackupArchiveService(
                dataRoot,
                provider.GetRequiredService<IAtomicFileSystem>(),
                provider.GetRequiredService<TimeProvider>()));
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddJsonConsole(options => options.UseUtcTimestamp = true);
            builder.AddProvider(new JsonLinesFileLoggerProvider(logPath));
        });

        return services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }
}
