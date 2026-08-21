param(
    [string]$Configuration = 'Release',
    [string]$DotNetPath
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$dotnet = if ([string]::IsNullOrWhiteSpace($DotNetPath)) {
    Join-Path $root '.dotnet\dotnet.exe'
} else {
    $DotNetPath
}
$publish = Join-Path $root 'artifacts\publish\win-x64'
$release = Join-Path $root 'artifacts\release'
$iscc = Join-Path $root '.tools\InnoSetup\ISCC.exe'
if (-not (Test-Path $dotnet -PathType Leaf)) { throw "Pinned .NET SDK is missing at: $dotnet" }
$dotnetVersion = (& $dotnet --version).Trim()
if ($LASTEXITCODE -ne 0 -or $dotnetVersion -ne '10.0.302') {
    throw "Expected .NET SDK 10.0.302, but found '$dotnetVersion'."
}
if (-not (Test-Path $iscc)) { throw 'Verified Inno Setup compiler is missing.' }

foreach ($generatedPath in @($publish, $release)) {
    $expectedRoot = Join-Path $root 'artifacts'
    if (-not $generatedPath.StartsWith($expectedRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clear a path outside the artifacts directory: $generatedPath"
    }
    if (Test-Path $generatedPath) { Remove-Item -LiteralPath $generatedPath -Recurse -Force }
}
New-Item -ItemType Directory -Path $publish,$release -Force | Out-Null
& $dotnet publish (Join-Path $root 'src\NovaLauncher.App\NovaLauncher.App.csproj') -c $Configuration -r win-x64 --self-contained true --no-restore -p:DebugType=None -p:DebugSymbols=false -o $publish
if ($LASTEXITCODE -ne 0) { throw 'Self-contained publish failed.' }
Get-ChildItem -LiteralPath $publish -Recurse -Filter '*.pdb' | Remove-Item -Force
Copy-Item (Join-Path $root 'LICENSE') $publish -Force
Copy-Item (Join-Path $root 'THIRD-PARTY-NOTICES.md') $publish -Force
Copy-Item (Join-Path $root 'installer\UNSIGNED-PREVIEW.txt') $publish -Force

$portable = Join-Path $release 'NovaLauncher-0.5.0-experimental.1-win-x64-portable.zip'
if (Test-Path $portable) { Remove-Item -LiteralPath $portable -Force }
Compress-Archive -Path (Join-Path $publish '*') -DestinationPath $portable -CompressionLevel Optimal

& $iscc (Join-Path $root 'installer\NovaLauncher.iss')
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }

& (Join-Path $PSScriptRoot 'New-Sbom.ps1') -OutputPath (Join-Path $release 'NovaLauncher-0.5.0-experimental.1.cdx.json')
$artifacts = Get-ChildItem -LiteralPath $release -File | Where-Object Name -ne 'SHA256SUMS.txt' | Sort-Object Name
$lines = foreach ($artifact in $artifacts) {
    $hash = (Get-FileHash -LiteralPath $artifact.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $($artifact.Name)"
}
$lines | Set-Content -LiteralPath (Join-Path $release 'SHA256SUMS.txt') -Encoding ascii
$artifacts | Select-Object Name,Length,@{n='SHA256';e={(Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}}
