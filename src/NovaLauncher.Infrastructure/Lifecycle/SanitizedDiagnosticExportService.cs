using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using NovaLauncher.Application.Lifecycle;
using NovaLauncher.Domain;

namespace NovaLauncher.Infrastructure.Lifecycle;

public sealed partial class SanitizedDiagnosticExportService(string dataRoot, TimeProvider timeProvider) : IDiagnosticExportService
{
    public const long MaximumLogBytes = 16L * 1024 * 1024;
    public async Task<DiagnosticExportResult> ExportAsync(string destinationPath, CancellationToken cancellationToken)
    {
        try
        {
            var destination = Path.GetFullPath(destinationPath);
            if (!string.Equals(Path.GetExtension(destination), ".zip", StringComparison.OrdinalIgnoreCase)) return new(false, null, "Choose a .zip destination for diagnostics.");
            if (File.Exists(destination)) return new(false, null, "Refusing to overwrite an existing diagnostic export.");
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            using var archive = new ZipArchive(output, ZipArchiveMode.Create);
            var summary = archive.CreateEntry("summary.txt", CompressionLevel.Fastest);
            await using (var writer = new StreamWriter(summary.Open(), new UTF8Encoding(false)))
            {
                await writer.WriteLineAsync($"NovaLauncher {ProductIdentity.Version}").ConfigureAwait(false);
                await writer.WriteLineAsync($"Exported UTC: {timeProvider.GetUtcNow():O}").ConfigureAwait(false);
                await writer.WriteLineAsync($"OS: {Environment.OSVersion.VersionString}").ConfigureAwait(false);
                await writer.WriteLineAsync($"Runtime: {Environment.Version}").ConfigureAwait(false);
                await writer.WriteLineAsync("No settings, API keys, pairing credentials, save contents, or library documents are included.").ConfigureAwait(false);
            }
            var log = Path.Combine(dataRoot, "Logs", "novalauncher.jsonl");
            if (File.Exists(log) && new FileInfo(log).Length <= MaximumLogBytes)
            {
                var entry = archive.CreateEntry("sanitized-log.jsonl", CompressionLevel.Fastest); await using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
                foreach (var line in await File.ReadAllLinesAsync(log, cancellationToken).ConfigureAwait(false)) await writer.WriteLineAsync(Sanitize(line).AsMemory(), cancellationToken).ConfigureAwait(false);
            }
            return new(true, destination, "Sanitized diagnostics were exported locally. Review the archive before sharing it.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException) { return new(false, null, $"Diagnostic export failed safely: {exception.Message}"); }
    }

    internal static string Sanitize(string value) => WindowsPath().Replace(IpAddress().Replace(GuidValue().Replace(value, "[redacted-id]"), "[redacted-address]"), "[redacted-path]");
    [GeneratedRegex(@"(?i)\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b", RegexOptions.CultureInvariant)] private static partial Regex GuidValue();
    [GeneratedRegex(@"(?i)(?:\b(?:\d{1,3}\.){3}\d{1,3}\b|\b(?:[0-9a-f]{0,4}:){2,}[0-9a-f]{0,4}\b)", RegexOptions.CultureInvariant)] private static partial Regex IpAddress();
    [GeneratedRegex(@"(?i)\b[a-z]:\\[^\""\r\n]*", RegexOptions.CultureInvariant)] private static partial Regex WindowsPath();
}
