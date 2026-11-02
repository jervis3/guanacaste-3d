<#
.SYNOPSIS
Removes only files recorded by the per-user installer, preserving projects and unrelated files.
#>
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
$messages = Import-PowerShellDataFile (Join-Path $PSScriptRoot 'uninstall-messages.es.psd1')
. (Join-Path $PSScriptRoot 'uninstall-file-cleanup.ps1')
$installRoot = [System.IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Programs\Guanacaste 3D')).TrimEnd('\')
$rootPrefix = $installRoot + '\'
$executable = Join-Path $installRoot 'Guanacaste3D.Desktop.exe'
$manifest = Join-Path $installRoot 'installed-files.txt'
try {
    if ([System.IO.Path]::GetFullPath($PSScriptRoot).TrimEnd('\') -ine $installRoot) { throw 'Unexpected uninstaller location.' }
    $running = Get-CimInstance Win32_Process -Filter "Name='Guanacaste3D.Desktop.exe'" | Where-Object { $_.ExecutablePath -eq $executable }
    if ($running) { [void][System.Windows.Forms.MessageBox]::Show($messages.Running, $messages.Title); exit 1 }
    if (-not (Test-Path -LiteralPath $manifest)) { [void][System.Windows.Forms.MessageBox]::Show($messages.MissingManifest, $messages.Title); exit 1 }
    $targets = @(Get-Content -LiteralPath $manifest | Where-Object { $_ } | ForEach-Object {
        $path = [System.IO.Path]::GetFullPath((Join-Path $installRoot $_))
        if (-not $path.StartsWith($rootPrefix, [System.StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid installation manifest path.' }
        $path
    })
    $thumbnailScript = Join-Path $installRoot 'register-thumbnails.ps1'
    if (Test-Path -LiteralPath $thumbnailScript) { & $thumbnailScript -Unregister }
    $types = @{ '.g3d' = 'Project'; '.g3' = 'Project'; '.g3dt' = 'Template'; '.g3df' = 'Family'; '.g3dx' = 'Exchange' }
    foreach ($extension in $types.Keys) {
        $extensionKey = "HKCU:\Software\Classes\$extension"
        $type = 'Guanacaste3D.' + $types[$extension]
        if ((Test-Path -LiteralPath $extensionKey) -and (Get-Item -LiteralPath $extensionKey).GetValue('') -eq $type) {
            Set-Item -LiteralPath $extensionKey -Value ''
        }
        $typeKey = "HKCU:\Software\Classes\$type"
        $commandKey = "$typeKey\shell\open\command"
        if ((Test-Path -LiteralPath $commandKey) -and ([string](Get-Item -LiteralPath $commandKey).GetValue('')).Contains($executable)) {
            Remove-Item -LiteralPath $typeKey -Recurse -Force
        }
    }
    $shortcutPath = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Guanacaste 3D.lnk'
    if (Test-Path -LiteralPath $shortcutPath) {
        $shortcut = (New-Object -ComObject WScript.Shell).CreateShortcut($shortcutPath)
        if ($shortcut.TargetPath -ieq $executable) { Remove-Item -LiteralPath $shortcutPath -Force }
    }
    $uninstallerPath = Join-Path $installRoot 'uninstall-guanacaste3d.ps1'
    $messagesPath = Join-Path $installRoot 'uninstall-messages.es.psd1'
    $cleanupPath = Join-Path $installRoot 'uninstall-file-cleanup.ps1'
    $controlFiles = @($manifest, $uninstallerPath, $messagesPath, $cleanupPath, $thumbnailScript)
    Remove-RecordedInstallationFiles -InstallRoot $installRoot -Targets $targets -ControlFiles $controlFiles
    $directories = @($targets | ForEach-Object { Split-Path -Parent $_ } | Sort-Object -Unique | Sort-Object Length -Descending)
    foreach ($directory in $directories) {
        if ($directory.StartsWith($rootPrefix, [System.StringComparison]::OrdinalIgnoreCase) -and
            (Test-Path -LiteralPath $directory -PathType Container) -and -not (Get-ChildItem -LiteralPath $directory -Force | Select-Object -First 1)) {
            Remove-Item -LiteralPath $directory -Force
        }
    }
    $uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Guanacaste3D'
    foreach ($controlFile in $controlFiles) {
        if (Test-Path -LiteralPath $controlFile -PathType Leaf) { Remove-Item -LiteralPath $controlFile -Force }
    }
    if ((Test-Path -LiteralPath $installRoot) -and -not (Get-ChildItem -LiteralPath $installRoot -Force | Select-Object -First 1)) {
        Remove-Item -LiteralPath $installRoot -Force
    }
    if (Test-Path -LiteralPath $uninstallKey) { Remove-Item -LiteralPath $uninstallKey -Force }
    [void][System.Windows.Forms.MessageBox]::Show($messages.Complete, $messages.Title)
}
catch {
    [System.Diagnostics.Trace]::TraceError('Uninstallation failed: {0}', $_.Exception.Message)
    [void][System.Windows.Forms.MessageBox]::Show($messages.Failed, $messages.Title)
    exit 1
}
