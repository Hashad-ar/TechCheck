[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$NoBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$testProject = Join-Path $repositoryRoot 'tests\TechCheck.Tests\TechCheck.Tests.csproj'

Push-Location $repositoryRoot
try {
    if (-not $NoBuild) {
        & (Join-Path $PSScriptRoot 'build.ps1') -Configuration $Configuration
    }

    & dotnet run --project $testProject --configuration $Configuration --no-build
    if ($LASTEXITCODE -ne 0) {
        throw "TechCheck tests failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}

Write-Host 'All TechCheck tests passed.'
