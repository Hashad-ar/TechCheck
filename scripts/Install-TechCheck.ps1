[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$packageDirectory = Split-Path -Parent $PSCommandPath
$localApplicationData = [Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)
if ([string]::IsNullOrWhiteSpace($localApplicationData)) {
    throw 'Windows did not provide a per-user Local AppData directory.'
}

$installDirectory = Join-Path $localApplicationData 'TechCheck'
$requiredFiles = @(
    'TechCheck.exe',
    'techcheck.ico',
    'Uninstall-TechCheck.cmd',
    'Uninstall-TechCheck.ps1'
)

foreach ($requiredFile in $requiredFiles) {
    $sourcePath = Join-Path $packageDirectory $requiredFile
    if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
        throw "The package is incomplete: $requiredFile is missing. Extract the full ZIP before installing."
    }
}

[System.IO.Directory]::CreateDirectory($installDirectory) | Out-Null
foreach ($requiredFile in $requiredFiles) {
    Copy-Item `
        -LiteralPath (Join-Path $packageDirectory $requiredFile) `
        -Destination (Join-Path $installDirectory $requiredFile) `
        -Force
}

$shell = $null
$shortcut = $null
try {
    $shell = New-Object -ComObject WScript.Shell
    $desktopDirectory = [string]$shell.SpecialFolders.Item('Desktop')
    if ([string]::IsNullOrWhiteSpace($desktopDirectory)) {
        throw 'Windows did not provide a Desktop directory for the current user.'
    }

    $shortcutPath = Join-Path $desktopDirectory 'TechCheck.lnk'
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = Join-Path $installDirectory 'TechCheck.exe'
    $shortcut.WorkingDirectory = $installDirectory
    $shortcut.IconLocation = "$(Join-Path $installDirectory 'techcheck.ico'),0"
    $shortcut.Description = 'Open TechCheck PC health and diagnostics'
    $shortcut.WindowStyle = 1
    $shortcut.Save()
}
finally {
    if ($null -ne $shortcut) {
        [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shortcut)
    }
    if ($null -ne $shell) {
        [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell)
    }
}

Write-Host 'TechCheck was installed for the current user.'
Write-Host "Application: $installDirectory"
Write-Host 'Shortcut:    TechCheck on your Desktop'
