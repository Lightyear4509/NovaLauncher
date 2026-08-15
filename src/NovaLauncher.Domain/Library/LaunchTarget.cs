namespace NovaLauncher.Domain.Library;

public sealed record LaunchTarget(
    string Target,
    IReadOnlyList<string> Arguments,
    string? WorkingDirectory,
    LaunchTargetKind Kind);

public enum LaunchTargetKind
{
    Executable = 0,
    Uri = 1,
}
