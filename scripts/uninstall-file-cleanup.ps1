function Remove-RecordedInstallationFiles {
    <#
    .SYNOPSIS
    Deletes recorded application files inside one installation root while retaining project files.
    .DESCRIPTION
    Validates all paths and rejects redirected directories before deleting any files. Files not listed
    in the manifest, project documents, and the uninstaller's control files are left untouched.
    #>
    param(
        [Parameter(Mandatory)][string]$InstallRoot,
        [Parameter(Mandatory)][string[]]$Targets,
        [Parameter(Mandatory)][string[]]$ControlFiles
    )
    $root = [System.IO.Path]::GetFullPath($InstallRoot).TrimEnd('\')
    $prefix = $root + '\'
    if ((Test-Path -LiteralPath $root) -and ((Get-Item -LiteralPath $root -Force).Attributes -band [System.IO.FileAttributes]::ReparsePoint)) {
        throw 'The installation directory is redirected.'
    }
    foreach ($target in $Targets) {
        $path = [System.IO.Path]::GetFullPath($target)
        if (-not $path.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid installation manifest path.' }
        $parent = Split-Path -Parent $path
        while ($parent -and $parent.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
            if ((Test-Path -LiteralPath $parent) -and ((Get-Item -LiteralPath $parent -Force).Attributes -band [System.IO.FileAttributes]::ReparsePoint)) {
                throw 'Installation manifest contains a redirected directory.'
            }
            $parent = Split-Path -Parent $parent
        }
    }
    foreach ($target in $Targets) {
        if ($target -in $ControlFiles -or [System.IO.Path]::GetExtension($target) -in @('.g3d', '.g3', '.g3dt', '.g3df', '.g3dx')) { continue }
        if (Test-Path -LiteralPath $target -PathType Leaf) { Remove-Item -LiteralPath $target -Force }
    }
}
