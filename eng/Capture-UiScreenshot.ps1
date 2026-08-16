param(
    [Parameter(Mandatory = $true)][string]$OutputPath,
    [string]$Configuration = 'Release',
    [int]$TimeoutSeconds = 20
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$dotnet = Join-Path $root '.dotnet\dotnet.exe'
$assembly = Join-Path $root "src\NovaLauncher.App\bin\$Configuration\net10.0\NovaLauncher.App.dll"
if (-not (Test-Path -LiteralPath $dotnet -PathType Leaf)) { throw "Pinned .NET SDK is missing at: $dotnet" }
if (-not (Test-Path -LiteralPath $assembly -PathType Leaf)) { throw "Build NovaLauncher before capturing the UI: $assembly" }

$resolvedOutput = [IO.Path]::GetFullPath($OutputPath)
$outputDirectory = Split-Path -Parent $resolvedOutput
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$captureData = Join-Path ([IO.Path]::GetTempPath()) ("NovaLauncher-Capture-" + [Guid]::NewGuid().ToString('N'))
$previousDataRoot = $env:NOVALAUNCHER_TEST_DATA_ROOT
$env:NOVALAUNCHER_TEST_DATA_ROOT = $captureData

Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class NovaWindowCapture {
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr handle, out Rect rect);
}
'@

$process = $null
try {
    $process = Start-Process -FilePath $dotnet -ArgumentList @($assembly) -WorkingDirectory (Split-Path -Parent $assembly) -WindowStyle Hidden -PassThru
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    $handle = [IntPtr]::Zero
    while ([DateTime]::UtcNow -lt $deadline) {
        Start-Sleep -Milliseconds 150
        $process.Refresh()
        if ($process.HasExited) { throw "NovaLauncher exited before capture with code $($process.ExitCode)." }
        $handle = $process.MainWindowHandle
        if ($handle -ne [IntPtr]::Zero -and $process.Responding) { break }
    }
    if ($handle -eq [IntPtr]::Zero) { throw 'NovaLauncher did not expose a responsive window before the capture timeout.' }
    Start-Sleep -Milliseconds 700
    $rect = [NovaWindowCapture+Rect]::new()
    if (-not [NovaWindowCapture]::GetWindowRect($handle, [ref]$rect)) { throw 'Could not read the NovaLauncher window bounds.' }
    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top
    if ($width -lt 820 -or $height -lt 600) { throw "Captured window was below the responsive minimum: ${width}x${height}." }
    $bitmap = [Drawing.Bitmap]::new($width, $height)
    try {
        $graphics = [Drawing.Graphics]::FromImage($bitmap)
        try { $graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $bitmap.Size) } finally { $graphics.Dispose() }
        $bitmap.Save($resolvedOutput, [Drawing.Imaging.ImageFormat]::Png)
    } finally { $bitmap.Dispose() }
    Get-Item -LiteralPath $resolvedOutput | Select-Object FullName, Length
}
finally {
    if ($process -and -not $process.HasExited) { $process.Kill($true); $process.WaitForExit() }
    if (Test-Path -LiteralPath $captureData) { Remove-Item -LiteralPath $captureData -Recurse -Force }
    $env:NOVALAUNCHER_TEST_DATA_ROOT = $previousDataRoot
}
