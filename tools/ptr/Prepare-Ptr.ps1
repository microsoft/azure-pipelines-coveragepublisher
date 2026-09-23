[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $Destination,
    [string] $PackagePath = (Join-Path $PSScriptRoot 'PublishTestResults.zip')
)

$ErrorActionPreference = 'Stop'
if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) {
    throw 'This PTR package requires Windows and .NET Framework.'
}
$release = Get-ItemPropertyValue 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full' -Name Release
if ($release -lt 528040) {
    throw 'Install .NET Framework 4.8 or later before running this workflow.'
}

$expectedHash = '4529455C24F3524EB7D3E5DA6B002E0E8AFF66D72E45C0C58CF9D09C55C04906'
if ((Get-FileHash -LiteralPath $PackagePath -Algorithm SHA256).Hash -ne $expectedHash) {
    throw 'PTR package checksum mismatch. Refusing to extract the package.'
}
if (Test-Path -LiteralPath $Destination) {
    throw 'Use a new destination directory to avoid mixing publisher versions.'
}
Expand-Archive -LiteralPath $PackagePath -DestinationPath $Destination
foreach ($relative in @(
    'modules\TestResultsPublisher.exe',
    'modules\TestResultsPublisher.exe.config',
    'modules\TfsAssemblies\Microsoft.TeamFoundation.TestClient.PublishTestResults.dll'
)) {
    if (-not (Test-Path -LiteralPath (Join-Path $Destination $relative) -PathType Leaf)) {
        throw "PTR package is missing $relative."
    }
}
(Get-Item -LiteralPath (Join-Path $Destination 'modules\TestResultsPublisher.exe')).FullName
