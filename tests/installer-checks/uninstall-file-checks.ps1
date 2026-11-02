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
$materialFile = Join-Path $testFolder 'custom-finish.g3dm'
$textureFile = Join-Path $testFolder 'custom-finish-albedo.png'
$unrelatedTextureFile = Join-Path $testFolder 'unused.png'
$unlistedMaterialFile = Join-Path $testFolder 'user-material.g3dm'
$unlistedTextureFile = Join-Path $testFolder 'user-material-normal.png'
$personalFile = Join-Path $testFolder 'personal.txt'
$outsidePath = Join-Path ([System.IO.Path]::GetTempPath()) ('guanacaste-outside-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testFolder -Force | Out-Null
try {
    Set-Content -LiteralPath $applicationFile -Value 'application'
    Set-Content -LiteralPath $projectFile -Value 'project'
    Set-Content -LiteralPath $materialFile -Value '{"version":1,"name":"Custom finish","albedo":"custom-finish-albedo.png"}'
    Set-Content -LiteralPath $textureFile -Value 'custom albedo'
    Set-Content -LiteralPath $unrelatedTextureFile -Value 'unrelated application image'
    # A user-created material may be absent from the installer manifest entirely.
    Set-Content -LiteralPath $unlistedMaterialFile -Value '{"version":1,"normal":"user-material-normal.png"}'
    Set-Content -LiteralPath $unlistedTextureFile -Value 'custom normal'
    Set-Content -LiteralPath $personalFile -Value 'personal'
    $rejected = $false
    try { Remove-RecordedInstallationFiles -InstallRoot $testFolder -Targets @($applicationFile, $outsidePath) -ControlFiles @('control') }
    catch { $rejected = $true }
    if (-not $rejected -or -not (Test-Path -LiteralPath $applicationFile)) { throw 'Unsafe paths must be rejected before any deletion.' }
    Remove-RecordedInstallationFiles -InstallRoot $testFolder -Targets @($applicationFile, $projectFile, $materialFile, $textureFile, $unrelatedTextureFile, $unlistedTextureFile) -ControlFiles @('control')
    if ((Test-Path -LiteralPath $applicationFile) -or (Test-Path -LiteralPath $unrelatedTextureFile) -or
        -not (Test-Path -LiteralPath $projectFile) -or -not (Test-Path -LiteralPath $materialFile) -or
        -not (Test-Path -LiteralPath $textureFile) -or -not (Test-Path -LiteralPath $unlistedMaterialFile) -or
        -not (Test-Path -LiteralPath $unlistedTextureFile) -or -not (Test-Path -LiteralPath $personalFile)) {
        throw 'Uninstallation deleted a native material or its texture, or retained an unrelated application file.'
    }
    Write-Output 'Uninstaller file safety checks passed.'
}
finally {
    foreach ($file in @($applicationFile, $projectFile, $materialFile, $textureFile, $unrelatedTextureFile, $unlistedMaterialFile, $unlistedTextureFile, $personalFile)) {
        if (Test-Path -LiteralPath $file -PathType Leaf) { Remove-Item -LiteralPath $file -Force }
    }
    if (Test-Path -LiteralPath $testFolder -PathType Container) { Remove-Item -LiteralPath $testFolder -Force }
}
