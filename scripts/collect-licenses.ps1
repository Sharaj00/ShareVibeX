$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$destination = Join-Path $projectRoot 'third-party-licenses'
New-Item -ItemType Directory -Path $destination -Force | Out-Null
$assets = Get-Content -LiteralPath (Join-Path $projectRoot 'src/ShareVibeX.App/obj/project.assets.json') -Raw | ConvertFrom-Json
foreach ($library in $assets.libraries.PSObject.Properties) {
    if ($library.Value.type -ne 'package') { continue }
    $packagePath = Join-Path $projectRoot ('.tools/packages/' + $library.Name.ToLowerInvariant())
    $target = Join-Path $destination ($library.Name.Replace('/','-'))
    New-Item -ItemType Directory -Path $target -Force | Out-Null
    Get-ChildItem -LiteralPath $packagePath -File | Where-Object {
        $_.Name -match 'license|notice|\.nuspec$'
    } | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $target }
}
