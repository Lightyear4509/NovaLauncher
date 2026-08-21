param(
    [Parameter(Mandatory = $true)][string]$OutputPath,
    [string]$Configuration = 'Release',
    [string]$ExecutablePath,
    [string]$NavigationName,
    [int]$TimeoutSeconds = 20
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$dotnet = Join-Path $root '.dotnet\dotnet.exe'
$assembly = Join-Path $root "src\NovaLauncher.App\bin\$Configuration\net10.0\NovaLauncher.App.dll"
if (-not (Test-Path -LiteralPath $dotnet -PathType Leaf)) { throw "Pinned .NET SDK is missing at: $dotnet" }
if ([string]::IsNullOrWhiteSpace($ExecutablePath) -and -not (Test-Path -LiteralPath $assembly -PathType Leaf)) { throw "Build NovaLauncher before capturing the UI: $assembly" }
$launchPath = if ([string]::IsNullOrWhiteSpace($ExecutablePath)) { $dotnet } else { [IO.Path]::GetFullPath($ExecutablePath) }
if (-not (Test-Path -LiteralPath $launchPath -PathType Leaf)) { throw "The capture executable does not exist: $launchPath" }
$launchArguments = if ([string]::IsNullOrWhiteSpace($ExecutablePath)) { @($assembly, '--smoke-test') } else { @('--smoke-test') }

$resolvedOutput = [IO.Path]::GetFullPath($OutputPath)
$outputDirectory = Split-Path -Parent $resolvedOutput
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$captureData = Join-Path ([IO.Path]::GetTempPath()) ("NovaLauncher-Capture-" + [Guid]::NewGuid().ToString('N'))
$previousDataRoot = $env:NOVALAUNCHER_TEST_DATA_ROOT
$env:NOVALAUNCHER_TEST_DATA_ROOT = $captureData

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
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
    $process = Start-Process -FilePath $launchPath -ArgumentList $launchArguments -WorkingDirectory (Split-Path -Parent $launchPath) -PassThru
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
    [void](New-Object -ComObject WScript.Shell).AppActivate($process.Id)
    if (-not [string]::IsNullOrWhiteSpace($NavigationName)) {
        $rootElement = [System.Windows.Automation.AutomationElement]::FromHandle($handle)
        $condition = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty, $NavigationName)
        $navigation = $rootElement.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
        if ($null -eq $navigation) { throw "Could not find navigation control '$NavigationName'." }
        $invoke = $navigation.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
        $invoke.Invoke()
    }
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
