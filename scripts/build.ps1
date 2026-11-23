[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$NoRestore
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$solutionPath = Join-Path $repositoryRoot 'TechCheck.sln'

function Invoke-DotNet {
    param([Parameter(Mandatory)][string[]]$Arguments)

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

Push-Location $repositoryRoot
try {
    & (Join-Path $PSScriptRoot 'generate-icon.ps1')

    if (-not $NoRestore) {
        Invoke-DotNet -Arguments @('restore', $solutionPath)
    }

    Invoke-DotNet -Arguments @(
        'build',
        $solutionPath,
        '--configuration', $Configuration,
        '--no-restore'
    )
}
finally {
    Pop-Location
}

Write-Host "TechCheck $Configuration build completed."
