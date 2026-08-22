param(
    [string]$Configuration = 'Release',
    [string]$DotNetPath,
    [string]$OutputRoot
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$dotnet = if ([string]::IsNullOrWhiteSpace($DotNetPath)) {
    Join-Path $root '.dotnet\dotnet.exe'
} else {
    $DotNetPath
}
$outputBase = if ([string]::IsNullOrWhiteSpace($OutputRoot)) { Join-Path $root 'artifacts' } else { [IO.Path]::GetFullPath($OutputRoot) }
$publish = Join-Path $outputBase 'publish\win-x64'
$release = Join-Path $outputBase 'release'
$iscc = Join-Path $root '.tools\InnoSetup\ISCC.exe'
$version = '1.1.0'

if (-not (Test-Path $dotnet -PathType Leaf)) { throw "Pinned .NET SDK is missing at: $dotnet" }
$dotnetVersion = (& $dotnet --version).Trim()
if ($LASTEXITCODE -ne 0 -or $dotnetVersion -ne '10.0.302') {
    throw "Expected .NET SDK 10.0.302, but found '$dotnetVersion'."
}
if (-not (Test-Path $iscc -PathType Leaf)) { throw 'Verified Inno Setup compiler is missing.' }

foreach ($generatedPath in @($publish, $release)) {
    if (-not ([IO.Path]::GetFullPath($generatedPath)).StartsWith(([IO.Path]::GetFullPath($outputBase) + [IO.Path]::DirectorySeparatorChar), [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clear a path outside the selected output root: $generatedPath"
    }
    if (Test-Path $generatedPath) { Remove-Item -LiteralPath $generatedPath -Recurse -Force }
}
New-Item -ItemType Directory -Path $publish,$release -Force | Out-Null

& $dotnet publish (Join-Path $root 'src\NovaLauncher.App\NovaLauncher.App.csproj') -c $Configuration -r win-x64 --self-contained true --no-restore -p:DebugType=None -p:DebugSymbols=false -p:NovaLauncherUpdatePublisherSha256= -o $publish
if ($LASTEXITCODE -ne 0) { throw 'Unsigned self-contained publish failed.' }
Get-ChildItem -LiteralPath $publish -Recurse -Filter '*.pdb' | Remove-Item -Force
Copy-Item (Join-Path $root 'LICENSE') $publish -Force
Copy-Item (Join-Path $root 'THIRD-PARTY-NOTICES.md') $publish -Force
Copy-Item (Join-Path $root 'UNSIGNED-PREVIEW.md') $publish -Force

$application = Join-Path $publish 'NovaLauncher.App.exe'
$applicationSignature = Get-AuthenticodeSignature $application
if ($applicationSignature.Status -ne 'NotSigned') {
    throw "Unsigned preview application unexpectedly has signature status '$($applicationSignature.Status)'."
}
$applicationAssembly = [Reflection.Assembly]::LoadFile((Join-Path $publish 'NovaLauncher.App.dll'))
$publisherPins = $applicationAssembly.GetCustomAttributesData() | Where-Object {
    $_.AttributeType.FullName -eq 'System.Reflection.AssemblyMetadataAttribute' -and
    $_.ConstructorArguments.Count -eq 2 -and
    [string]$_.ConstructorArguments[0].Value -eq 'NovaLauncherUpdatePublisherSha256'
}
if ($publisherPins.Count -ne 0) {
    throw 'Unsigned preview unexpectedly contains a trusted update-publisher pin.'
}

$portable = Join-Path $release "NovaLauncher-$version-unsigned-win-x64-portable.zip"
Compress-Archive -Path (Join-Path $publish '*') -DestinationPath $portable -CompressionLevel Optimal

& $iscc '/DArtifactSuffix=-unsigned' '/DUnsignedPreview=1' "/DArtifactRoot=$publish" "/DInstallerOutputDir=$release" (Join-Path $root 'installer\NovaLauncher.iss')
if ($LASTEXITCODE -ne 0) { throw 'Unsigned preview installer compilation failed.' }
$installer = Join-Path $release "NovaLauncher-Setup-$version-unsigned-win-x64.exe"
$installerSignature = Get-AuthenticodeSignature $installer
if ($installerSignature.Status -ne 'NotSigned') {
    throw "Unsigned preview installer unexpectedly has signature status '$($installerSignature.Status)'."
}

Copy-Item (Join-Path $root 'UNSIGNED-PREVIEW.md') $release -Force
& (Join-Path $PSScriptRoot 'New-Sbom.ps1') -OutputPath (Join-Path $release "NovaLauncher-$version-unsigned.cdx.json")
$artifacts = Get-ChildItem -LiteralPath $release -File | Where-Object Name -ne 'SHA256SUMS.txt' | Sort-Object Name
$lines = foreach ($artifact in $artifacts) {
    $hash = (Get-FileHash -LiteralPath $artifact.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $($artifact.Name)"
}
$lines | Set-Content -LiteralPath (Join-Path $release 'SHA256SUMS.txt') -Encoding ascii
$artifacts | Select-Object Name,Length,@{n='SHA256';e={(Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}}
