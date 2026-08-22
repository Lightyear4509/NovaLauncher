param([Parameter(Mandatory = $true)][string]$OutputPath)

$ErrorActionPreference = 'Stop'
$components = [System.Collections.Generic.Dictionary[string, object]]::new([System.StringComparer]::OrdinalIgnoreCase)
Get-ChildItem -Path (Join-Path $PSScriptRoot '..') -Filter packages.lock.json -Recurse |
    Where-Object { $_.FullName -notmatch '\\(bin|obj|artifacts|TestResults)\\' } |
    ForEach-Object {
        $lock = Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json
        foreach ($framework in $lock.dependencies.PSObject.Properties) {
            foreach ($dependency in $framework.Value.PSObject.Properties) {
                $version = [string]$dependency.Value.resolved
                if ([string]::IsNullOrWhiteSpace($version)) { continue }
                $key = "$($dependency.Name)@$version"
                $components[$key] = [ordered]@{
                    type = 'library'
                    name = $dependency.Name
                    version = $version
                    purl = "pkg:nuget/$([uri]::EscapeDataString($dependency.Name))@$version"
                    scope = if ($dependency.Value.type -eq 'Direct') { 'required' } else { 'optional' }
                }
            }
        }
    }

$sbom = [ordered]@{
    bomFormat = 'CycloneDX'
    specVersion = '1.6'
    serialNumber = "urn:uuid:$([guid]::NewGuid())"
    version = 1
    metadata = [ordered]@{
        timestamp = [DateTimeOffset]::UtcNow.ToString('O')
        component = [ordered]@{ type = 'application'; name = 'NovaLauncher'; version = '1.0.0' }
    }
    components = @($components.Values | Sort-Object name, version)
}

$directory = Split-Path -Parent $OutputPath
New-Item -ItemType Directory -Path $directory -Force | Out-Null
$sbom | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $OutputPath -Encoding utf8NoBOM
