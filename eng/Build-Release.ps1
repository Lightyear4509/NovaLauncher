param(
    [string]$Configuration = 'Release',
    [string]$DotNetPath,
    [Parameter(Mandatory = $true)][string]$SigningCertificatePath,
    [Parameter(Mandatory = $true)][string]$SigningCertificatePassword
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
if (-not (Test-Path $SigningCertificatePath -PathType Leaf)) { throw 'The Phase 6 signing certificate is missing.' }
$certificate = [System.Security.Cryptography.X509Certificates.X509CertificateLoader]::LoadPkcs12FromFile(
    (Resolve-Path $SigningCertificatePath).Path,
    $SigningCertificatePassword,
    [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::EphemeralKeySet)
try {
    if (-not $certificate.HasPrivateKey) { throw 'The signing certificate does not contain a private key.' }
    $publisherPin = $certificate.GetCertHashString([System.Security.Cryptography.HashAlgorithmName]::SHA256).ToLowerInvariant()
} finally { $certificate.Dispose() }
$signTool = Get-ChildItem (Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin') -Filter signtool.exe -Recurse -File |
    Where-Object FullName -Match '\\x64\\signtool\.exe$' | Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
if ([string]::IsNullOrWhiteSpace($signTool)) { throw 'A Windows SDK x64 signtool.exe is required for Phase 6 packaging.' }

foreach ($generatedPath in @($publish, $release)) {
    $expectedRoot = Join-Path $root 'artifacts'
    if (-not $generatedPath.StartsWith($expectedRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clear a path outside the artifacts directory: $generatedPath"
    }
    if (Test-Path $generatedPath) { Remove-Item -LiteralPath $generatedPath -Recurse -Force }
}
New-Item -ItemType Directory -Path $publish,$release -Force | Out-Null
& $dotnet publish (Join-Path $root 'src\NovaLauncher.App\NovaLauncher.App.csproj') -c $Configuration -r win-x64 --self-contained true --no-restore -p:DebugType=None -p:DebugSymbols=false -p:NovaLauncherUpdatePublisherSha256=$publisherPin -o $publish
if ($LASTEXITCODE -ne 0) { throw 'Self-contained publish failed.' }
Get-ChildItem -LiteralPath $publish -Recurse -Filter '*.pdb' | Remove-Item -Force
Copy-Item (Join-Path $root 'LICENSE') $publish -Force
Copy-Item (Join-Path $root 'THIRD-PARTY-NOTICES.md') $publish -Force
& $signTool sign /fd SHA256 /td SHA256 /tr 'http://timestamp.digicert.com' /f $SigningCertificatePath /p $SigningCertificatePassword (Join-Path $publish 'NovaLauncher.App.exe')
if ($LASTEXITCODE -ne 0) { throw 'Application Authenticode signing failed.' }
$appSignature = Get-AuthenticodeSignature (Join-Path $publish 'NovaLauncher.App.exe')
if ($appSignature.Status -ne 'Valid') { throw "Signed application verification failed: $($appSignature.Status)." }

$portable = Join-Path $release 'NovaLauncher-1.0.0-win-x64-portable.zip'
if (Test-Path $portable) { Remove-Item -LiteralPath $portable -Force }
Compress-Archive -Path (Join-Path $publish '*') -DestinationPath $portable -CompressionLevel Optimal

& $iscc (Join-Path $root 'installer\NovaLauncher.iss')
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
& $signTool sign /fd SHA256 /td SHA256 /tr 'http://timestamp.digicert.com' /f $SigningCertificatePath /p $SigningCertificatePassword (Join-Path $release 'NovaLauncher-Setup-1.0.0-win-x64.exe')
if ($LASTEXITCODE -ne 0) { throw 'Installer Authenticode signing failed.' }
$installerSignature = Get-AuthenticodeSignature (Join-Path $release 'NovaLauncher-Setup-1.0.0-win-x64.exe')
if ($installerSignature.Status -ne 'Valid') { throw "Signed installer verification failed: $($installerSignature.Status)." }

& (Join-Path $PSScriptRoot 'New-Sbom.ps1') -OutputPath (Join-Path $release 'NovaLauncher-1.0.0.cdx.json')
$artifacts = Get-ChildItem -LiteralPath $release -File | Where-Object Name -ne 'SHA256SUMS.txt' | Sort-Object Name
$lines = foreach ($artifact in $artifacts) {
    $hash = (Get-FileHash -LiteralPath $artifact.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $($artifact.Name)"
}
$lines | Set-Content -LiteralPath (Join-Path $release 'SHA256SUMS.txt') -Encoding ascii
$artifacts | Select-Object Name,Length,@{n='SHA256';e={(Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}}
