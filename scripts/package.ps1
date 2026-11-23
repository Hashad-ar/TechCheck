[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$SkipTests
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repositoryRoot 'src\TechCheck\TechCheck.csproj'
$outputRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot 'outputs'))
$packageName = 'TechCheck-win-x64'
$packageDirectory = [System.IO.Path]::GetFullPath((Join-Path $outputRoot $packageName))
$archivePath = [System.IO.Path]::GetFullPath((Join-Path $outputRoot "$packageName.zip"))
$checksumPath = [System.IO.Path]::GetFullPath((Join-Path $outputRoot "$packageName.sha256"))

if (-not $packageDirectory.StartsWith($outputRoot + [System.IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Refusing to package outside the repository outputs directory.'
}

Push-Location $repositoryRoot
try {
    if ($SkipTests) {
        & (Join-Path $PSScriptRoot 'build.ps1') -Configuration $Configuration
    }
    else {
        & (Join-Path $PSScriptRoot 'test.ps1') -Configuration $Configuration
    }

    [System.IO.Directory]::CreateDirectory($outputRoot) | Out-Null
    if (Test-Path -LiteralPath $packageDirectory) {
        Remove-Item -LiteralPath $packageDirectory -Recurse -Force
    }
    foreach ($generatedFile in @($archivePath, $checksumPath)) {
        if (Test-Path -LiteralPath $generatedFile) {
            Remove-Item -LiteralPath $generatedFile -Force
        }
    }
    [System.IO.Directory]::CreateDirectory($packageDirectory) | Out-Null

    & dotnet publish $projectPath `
        --configuration $Configuration `
        --runtime win-x64 `
        --self-contained true `
        --no-restore `
        --output $packageDirectory `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:DebugSymbols=false `
        -p:DebugType=None
    if ($LASTEXITCODE -ne 0) {
        throw "TechCheck publish failed with exit code $LASTEXITCODE."
    }

    $executablePath = Join-Path $packageDirectory 'TechCheck.exe'
    if (-not (Test-Path -LiteralPath $executablePath -PathType Leaf)) {
        throw "Publish did not create $executablePath."
    }

    Copy-Item -LiteralPath (Join-Path $repositoryRoot 'assets\techcheck.ico') -Destination $packageDirectory -Force
    $packageAssetsDirectory = Join-Path $packageDirectory 'assets'
    [System.IO.Directory]::CreateDirectory($packageAssetsDirectory) | Out-Null
    Copy-Item `
        -LiteralPath (Join-Path $repositoryRoot 'assets\techcheck-dashboard.png') `
        -Destination $packageAssetsDirectory `
        -Force
    foreach ($installerFile in @(
        'Install-TechCheck.cmd',
        'Install-TechCheck.ps1',
        'Uninstall-TechCheck.cmd',
        'Uninstall-TechCheck.ps1'
    )) {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot $installerFile) -Destination $packageDirectory -Force
    }
    Copy-Item -LiteralPath (Join-Path $repositoryRoot 'README.md') -Destination $packageDirectory -Force

    Compress-Archive -LiteralPath $packageDirectory -DestinationPath $archivePath -CompressionLevel Optimal
    $hash = Get-FileHash -LiteralPath $archivePath -Algorithm SHA256
    ('{0} *{1}' -f $hash.Hash.ToLowerInvariant(), [System.IO.Path]::GetFileName($archivePath)) |
        Set-Content -LiteralPath $checksumPath -Encoding Ascii
}
finally {
    Pop-Location
}

Write-Host "Portable package: $archivePath"
Write-Host "SHA-256 file:    $checksumPath"
