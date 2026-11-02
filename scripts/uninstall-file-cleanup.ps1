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
    # A .g3dm is a JSON manifest whose texture maps live beside it. Some images may
    # also be recorded in the installation manifest (e.g. after an update), so keeping
    # only the .g3dm file would leave a broken user material.
    $protectedTextures = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
    $materialDirectories = @($Targets | ForEach-Object { Split-Path -Parent $_ } | Sort-Object -Unique)
    foreach ($directory in $materialDirectories) {
        if (-not (Test-Path -LiteralPath $directory -PathType Container)) { continue }
        foreach ($material in @(Get-ChildItem -LiteralPath $directory -Filter '*.g3dm' -File -Force)) {
            # Never inspect a linked manifest outside the validated installation tree.
            if ($material.Attributes -band [System.IO.FileAttributes]::ReparsePoint) { continue }
            if ($material.Length -gt 1048576) { continue }
            try {
                $data = Get-Content -LiteralPath $material.FullName -Raw -ErrorAction Stop | ConvertFrom-Json -ErrorAction Stop
                foreach ($channel in @('albedo', 'normal', 'roughnessMap', 'metallicMap', 'heightMap')) {
                    $textureName = $data.$channel
                    if ($textureName -isnot [string] -or [string]::IsNullOrWhiteSpace($textureName)) { continue }
                    # The package writer emits file names only; never follow a path from JSON.
                    if ($textureName -match '[\\/]' -or [System.IO.Path]::GetFileName($textureName) -cne $textureName) { continue }
                    if ([System.IO.Path]::GetExtension($textureName).ToLowerInvariant() -notin @('.png', '.jpg', '.jpeg', '.bmp', '.tif', '.tiff')) { continue }
                    $texturePath = [System.IO.Path]::GetFullPath((Join-Path $directory $textureName))
                    if ($texturePath.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
                        [void]$protectedTextures.Add($texturePath)
                    }
                }
            }
            catch {
                # An invalid material manifest is not an instruction to delete other files.
                # Keep the manifest itself; only valid local texture references are protected.
            }
        }
    }
    # Native PBR material manifests (.g3dm) are user assets, not disposable binaries.
    foreach ($target in $Targets) {
        if ($target -in $ControlFiles -or $protectedTextures.Contains([System.IO.Path]::GetFullPath($target)) -or [System.IO.Path]::GetExtension($target) -in @('.g3d', '.g3', '.g3dt', '.g3df', '.g3dx', '.g3dm')) { continue }
        if (Test-Path -LiteralPath $target -PathType Leaf) { Remove-Item -LiteralPath $target -Force }
    }
}
