using NovaLauncher.Domain.Library;

namespace NovaLauncher.Application.Library;

public sealed record ManualGameDraft(
    string Name,
    string Platform,
    string Target,
    IReadOnlyList<string> Arguments,
    string? WorkingDirectory,
    LaunchTargetKind TargetKind);

public sealed record DraftValidationResult(bool IsValid, IReadOnlyDictionary<string, string> Errors);
