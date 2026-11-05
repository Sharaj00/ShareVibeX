param([Parameter(Mandatory)][ValidatePattern('^[0-9]+\.[0-9]+\.[0-9]+$')][string]$Version)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$dist = Get-Item -LiteralPath (Join-Path $root 'dist')
if ($dist.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Linked dist directory is unsupported.' }
$pattern = '^ShareVibeX-(\d+\.\d+\.\d+)-win-x64(?:\.zip(?:\.sha256)?|-previous-\d{8}-\d{6}-\d{3})?$'
$builds = foreach ($item in Get-ChildItem -LiteralPath $dist.FullName -Force) {
    if ($item.Name -match $pattern) { [pscustomobject]@{ Item = $item; Version = [version]$Matches[1] } }
}
$current = [version]$Version
$previous = $builds | Where-Object { $_.Version -lt $current -and $_.Item.Name -notlike '*-previous-*' } |
    Sort-Object Version -Descending | Select-Object -First 1 -ExpandProperty Version
$active = Join-Path $dist.FullName "ShareVibeX-$Version-win-x64"
foreach ($build in $builds) {
    $item = $build.Item
    $replacement = $item.PSIsContainer -and $item.Name -like '*-previous-*'
    if ($build.Version -gt $current -or (!$replacement -and $build.Version -in $current,$previous)) { continue }
    if ((Split-Path -Parent $item.FullName) -ne $dist.FullName -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw "Unsafe build path: $($item.Name)"
    }
    if ($item.PSIsContainer) {
        $backups = Get-ChildItem -LiteralPath $item.FullName -File -Filter 'settings.json.*.bak' | Select-Object -ExpandProperty Name
        foreach ($name in @('data','tools','settings.json') + @($backups | Where-Object { $_ })) {
            $content = Get-Item -LiteralPath (Join-Path $item.FullName $name) -ErrorAction SilentlyContinue
            if (!$content) { continue }
            if ($content.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Linked portable folder: $($content.FullName)" }
            if ($content.PSIsContainer -and !(Get-ChildItem -LiteralPath $content.FullName -Force | Select-Object -First 1)) { continue }
            $target = Join-Path $active $name
            if ($build.Version -eq $current -and !(Get-ChildItem -LiteralPath $target -Force -ErrorAction SilentlyContinue | Select-Object -First 1)) {
                if (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target }
                Move-Item -LiteralPath $content.FullName -Destination $target
            } else {
                $archive = Join-Path $root ('data/previous-builds/' + $item.Name)
                foreach ($part in 'data','data/previous-builds',('data/previous-builds/' + $item.Name)) {
                    $parent = Get-Item -LiteralPath (Join-Path $root $part) -ErrorAction SilentlyContinue
                    if ($parent -and ($parent.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw "Linked backup directory: $($parent.FullName)" }
                }
                New-Item -ItemType Directory -Path $archive -Force | Out-Null
                $target = Join-Path $archive $name
                if (Test-Path -LiteralPath $target) { throw "Portable data backup already exists: $target" }
                Move-Item -LiteralPath $content.FullName -Destination $target
            }
        }
    }
    Remove-Item -LiteralPath $item.FullName -Recurse -Force
}
