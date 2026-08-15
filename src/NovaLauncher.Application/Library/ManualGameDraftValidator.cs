using NovaLauncher.Domain.Library;

namespace NovaLauncher.Application.Library;

public sealed class ManualGameDraftValidator
{
    private readonly HashSet<string> _allowedUriSchemes =
        new(StringComparer.OrdinalIgnoreCase) { "steam", "goggalaxy", "com.epicgames.launcher" };

    public DraftValidationResult Validate(ManualGameDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var errors = new Dictionary<string, string>(StringComparer.Ordinal);

        ValidateRequiredLength(draft.Name, "Name", 500, errors);
        ValidateRequiredLength(draft.Platform, "Platform", 100, errors);

        if (draft.Arguments is null || draft.Arguments.Count > 100 || draft.Arguments.Any(static value => value.Length > 4_096))
        {
            errors["Arguments"] = "Use at most 100 arguments of no more than 4,096 characters each.";
        }

        if (draft.TargetKind == LaunchTargetKind.Executable)
        {
            if (string.IsNullOrWhiteSpace(draft.Target) || !Path.IsPathFullyQualified(draft.Target))
            {
                errors["Target"] = "Choose an absolute executable path.";
            }
            else if (!string.Equals(Path.GetExtension(draft.Target), ".exe", StringComparison.OrdinalIgnoreCase))
            {
                errors["Target"] = "The launch target must be a Windows executable (.exe).";
            }

            if (!string.IsNullOrWhiteSpace(draft.WorkingDirectory) && !Path.IsPathFullyQualified(draft.WorkingDirectory))
            {
                errors["WorkingDirectory"] = "The working directory must be an absolute path.";
            }
        }
        else if (!Uri.TryCreate(draft.Target, UriKind.Absolute, out var uri) || !_allowedUriSchemes.Contains(uri.Scheme))
        {
            errors["Target"] = "Use an allowlisted absolute launcher URI.";
        }

        return new DraftValidationResult(errors.Count == 0, errors);
    }

    private static void ValidateRequiredLength(
        string value,
        string field,
        int maximumLength,
        Dictionary<string, string> errors)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > maximumLength)
        {
            errors[field] = $"{field} is required and cannot exceed {maximumLength} characters.";
        }
    }
}
