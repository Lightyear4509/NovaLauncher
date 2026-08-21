using System.Globalization;
using NovaLauncher.Application.Lifecycle;

namespace NovaLauncher.Infrastructure.Lifecycle;

public sealed class CrashRecoveryService(string dataRoot, TimeProvider timeProvider) : ICrashRecoveryService
{
    private readonly string _marker = Path.Combine(Path.GetFullPath(dataRoot), "Lifecycle", "session.active");

    public CrashRecoveryState BeginSession()
    {
        var interrupted = File.Exists(_marker);
        Directory.CreateDirectory(Path.GetDirectoryName(_marker)!);
        var temporary = _marker + $".{Guid.NewGuid():N}.tmp";
        File.WriteAllText(temporary, timeProvider.GetUtcNow().ToString("O", CultureInfo.InvariantCulture));
        File.Move(temporary, _marker, true);
        return new(interrupted, interrupted ? "NovaLauncher detected an interrupted prior session. Your library data was not reset; inspect diagnostics if the problem repeats." : "No interrupted prior session was detected.");
    }

    public void CompleteSession() { if (File.Exists(_marker)) File.Delete(_marker); }
}
