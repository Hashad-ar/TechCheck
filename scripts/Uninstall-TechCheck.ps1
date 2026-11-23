[CmdletBinding()]
param(
    [switch]$KeepCommandLauncher
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$localApplicationData = [Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)
if ([string]::IsNullOrWhiteSpace($localApplicationData)) {
    throw 'Windows did not provide a per-user Local AppData directory.'
}

$installDirectory = [System.IO.Path]::GetFullPath((Join-Path $localApplicationData 'TechCheck'))
$installedExecutable = Join-Path $installDirectory 'TechCheck.exe'

$runningInstalledProcess = Get-Process -Name 'TechCheck' -ErrorAction SilentlyContinue | Where-Object {
    try {
        [string]::Equals(
            [System.IO.Path]::GetFullPath($_.Path),
            $installedExecutable,
            [StringComparison]::OrdinalIgnoreCase)
    }
    catch {
        $false
    }
}
if ($null -ne $runningInstalledProcess) {
    throw 'TechCheck is running. Close the application, then run the uninstaller again.'
}

$shell = $null
try {
    $shell = New-Object -ComObject WScript.Shell
    $desktopDirectory = [string]$shell.SpecialFolders.Item('Desktop')
    if (-not [string]::IsNullOrWhiteSpace($desktopDirectory)) {
        $shortcutPath = Join-Path $desktopDirectory 'TechCheck.lnk'
        if (Test-Path -LiteralPath $shortcutPath) {
            Remove-Item -LiteralPath $shortcutPath -Force
        }
    }
}
finally {
    if ($null -ne $shell) {
        [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell)
    }
}

if (Test-Path -LiteralPath $installDirectory) {
    if ($KeepCommandLauncher) {
        Get-ChildItem -LiteralPath $installDirectory -Force |
            Where-Object Name -ne 'Uninstall-TechCheck.cmd' |
            Remove-Item -Recurse -Force
    }
    else {
        Remove-Item -LiteralPath $installDirectory -Recurse -Force
    }
}

Write-Host 'TechCheck was removed for the current user.'
