namespace NovaLauncher.Domain.Library;

public sealed record LaunchTarget(
    string Target,
    IReadOnlyList<string> Arguments,
    string? WorkingDirectory,
    LaunchTargetKind Kind);

public sealed record GameLaunchAction(Guid Id, string Label, LaunchTarget Target);

public enum LaunchTargetKind
{
    Executable = 0,
    Uri = 1,
}
