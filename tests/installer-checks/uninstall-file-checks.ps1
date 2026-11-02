<#
.SYNOPSIS
Checks that the uninstaller removes recorded application files without deleting user projects or unrelated files.
#>
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
. (Join-Path $repositoryRoot 'scripts\uninstall-file-cleanup.ps1')
$testFolder = Join-Path ([System.IO.Path]::GetTempPath()) ('guanacaste-uninstall-check-' + [guid]::NewGuid().ToString('N'))
$applicationFile = Join-Path $testFolder 'application.dll'
$projectFile = Join-Path $testFolder 'project.g3d'
$personalFile = Join-Path $testFolder 'personal.txt'
$outsidePath = Join-Path ([System.IO.Path]::GetTempPath()) ('guanacaste-outside-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testFolder -Force | Out-Null
try {
    Set-Content -LiteralPath $applicationFile -Value 'application'
    Set-Content -LiteralPath $projectFile -Value 'project'
    Set-Content -LiteralPath $personalFile -Value 'personal'
    $rejected = $false
    try { Remove-RecordedInstallationFiles -InstallRoot $testFolder -Targets @($applicationFile, $outsidePath) -ControlFiles @('control') }
    catch { $rejected = $true }
    if (-not $rejected -or -not (Test-Path -LiteralPath $applicationFile)) { throw 'Unsafe paths must be rejected before any deletion.' }
    Remove-RecordedInstallationFiles -InstallRoot $testFolder -Targets @($applicationFile, $projectFile) -ControlFiles @('control')
    if ((Test-Path -LiteralPath $applicationFile) -or -not (Test-Path -LiteralPath $projectFile) -or -not (Test-Path -LiteralPath $personalFile)) {
        throw 'Uninstallation did not preserve the project and unrecorded personal file.'
    }
    Write-Output 'Uninstaller file safety checks passed.'
}
finally {
    foreach ($file in @($applicationFile, $projectFile, $personalFile)) {
        if (Test-Path -LiteralPath $file -PathType Leaf) { Remove-Item -LiteralPath $file -Force }
    }
    if (Test-Path -LiteralPath $testFolder -PathType Container) { Remove-Item -LiteralPath $testFolder -Force }
}
