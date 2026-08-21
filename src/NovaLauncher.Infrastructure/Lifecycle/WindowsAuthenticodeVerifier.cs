using System.Diagnostics;
using NovaLauncher.Application.Lifecycle;

namespace NovaLauncher.Infrastructure.Lifecycle;

public sealed class WindowsAuthenticodeVerifier : IAuthenticodeVerifier
{
    public AuthenticodeVerification Verify(string path, IReadOnlySet<string> trustedCertificateSha256)
    {
        if (!OperatingSystem.IsWindows()) return new(false, "Authenticode verification is available only on Windows.");
        var start = new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe"),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add("-NoLogo"); start.ArgumentList.Add("-NoProfile"); start.ArgumentList.Add("-NonInteractive");
        start.ArgumentList.Add("-Command");
        start.ArgumentList.Add("$s=Get-AuthenticodeSignature -LiteralPath $args[0]; if($s.Status -eq 'Valid' -and $null -ne $s.SignerCertificate){'Valid|'+$s.SignerCertificate.GetCertHashString('SHA256')}else{[string]$s.Status+'|'}");
        start.ArgumentList.Add(Path.GetFullPath(path));
        using var process = Process.Start(start);
        if (process is null) return new(false, "Windows Authenticode verification could not start.");
        if (!process.WaitForExit(30_000)) { process.Kill(entireProcessTree: true); return new(false, "Windows Authenticode verification timed out."); }
        var output = process.StandardOutput.ReadToEnd(); var error = process.StandardError.ReadToEnd();
        if (process.ExitCode != 0 || !string.IsNullOrWhiteSpace(error)) return new(false, "Windows Authenticode verification failed.");
        var parts = output.Trim().Split('|', 2); if (parts.Length != 2 || !string.Equals(parts[0], "Valid", StringComparison.Ordinal)) return new(false, "Windows rejected the installer Authenticode signature or certificate chain.");
        var pin = parts[1].Trim().ToLowerInvariant();
        return pin.Length == 64 && pin.All(Uri.IsHexDigit) && trustedCertificateSha256.Contains(pin)
            ? new(true, "Authenticode signature and publisher pin are valid.")
            : new(false, "The installer signer does not match a pinned NovaLauncher publisher certificate.");
    }
}
