param([ValidatePattern('^[0-9]+\.[0-9]+\.[0-9]+$')][string]$Version)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_CLI_HOME = Join-Path $projectRoot '.tools/cli'
$localSdk = Join-Path $projectRoot '.tools/dotnet/dotnet.exe'
$dotnetCommand = if (Test-Path -LiteralPath $localSdk) { $localSdk } else { (Get-Command dotnet -ErrorAction Stop).Source }
Push-Location $projectRoot
try {
    $appProject = 'src/ShareVibeX.App/ShareVibeX.App.csproj'
    $projectVersion = [string]([xml](Get-Content -LiteralPath $appProject -Raw)).Project.PropertyGroup.Version
    if (!$Version) { $Version = $projectVersion }
    if ($Version -ne $projectVersion) { throw "Package version $Version does not match project version $projectVersion." }
    $publishDirectory = Join-Path $projectRoot "dist/ShareVibeX-$Version-win-x64"
    # Use a dedicated temporary directory so a failed publish cannot replace a working package.
    $temporary = Join-Path $projectRoot ('dist/publish-' + [Guid]::NewGuid().ToString('N'))
    & $dotnetCommand restore src/ShareVibeX.App/ShareVibeX.App.csproj -r win-x64 --locked-mode --nologo
    if ($LASTEXITCODE) { throw 'Locked publish restore failed.' }
    & $dotnetCommand publish src/ShareVibeX.App/ShareVibeX.App.csproj -c Release -r win-x64 --self-contained true --no-restore -o (Join-Path $temporary 'libs') ('-p:PortableLauncherPath=' + (Join-Path $temporary 'ShareVibeX.App.exe')) --nologo -m:1 /nodeReuse:false
    if ($LASTEXITCODE) { throw 'Publish failed.' }
    New-Item -ItemType Directory -Path (Join-Path $temporary 'data') -Force | Out-Null
    foreach ($name in 'README.md','LINUX.md','CHANGELOG.md','LICENSE','THIRD-PARTY-NOTICES.md') {
        Copy-Item -LiteralPath (Join-Path $projectRoot $name) -Destination $temporary
    }
    Copy-Item -LiteralPath (Join-Path $projectRoot 'docs/testing/WGC-video-manual.md') -Destination (Join-Path $temporary 'VERIFY.md')
    New-Item -ItemType Directory -Path (Join-Path $temporary 'docs') -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $projectRoot 'docs/recording-settings.md') -Destination (Join-Path $temporary 'docs/recording-settings.md')
    Copy-Item -LiteralPath (Join-Path $projectRoot 'docs/ROADMAP.md') -Destination (Join-Path $temporary 'docs/ROADMAP.md')
    Copy-Item -LiteralPath (Join-Path $projectRoot 'docs/linux-portability-assessment.md') -Destination (Join-Path $temporary 'docs/linux-portability-assessment.md')
    Copy-Item -LiteralPath (Join-Path $projectRoot 'third-party-licenses') -Destination $temporary -Recurse
    $sourceZip = Join-Path $temporary 'ShareVibeX-source.zip'
    $archive = [System.IO.Compression.ZipFile]::Open($sourceZip, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        $sourceFiles = Get-Item -LiteralPath @(
            '.gitignore','Directory.Build.props','Directory.Packages.props',
            'global.json','LICENSE','LINUX.md','CHANGELOG.md','README.md','ShareVibeX.slnx','THIRD-PARTY-NOTICES.md'
        )
        foreach ($folder in 'src','tests','scripts','docs','third-party-licenses') {
            $sourceFiles += Get-ChildItem -LiteralPath (Join-Path $projectRoot $folder) -Recurse -File |
                Where-Object { $_.FullName -notmatch '[\\/](bin|obj|TestResults)[\\/]' }
        }
        foreach ($file in $sourceFiles) {
            $relative = [System.IO.Path]::GetRelativePath($projectRoot,$file.FullName).Replace('\','/')
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$file.FullName,$relative) | Out-Null
        }
    } finally { $archive.Dispose() }
    $zip = Join-Path $projectRoot "dist/ShareVibeX-$Version-win-x64.zip"
    $temporaryZip = Join-Path $projectRoot ('dist/package-' + [Guid]::NewGuid().ToString('N') + '.zip')
    Compress-Archive -Path (Join-Path $temporary '*') -DestinationPath $temporaryZip
    Move-Item -LiteralPath $temporaryZip -Destination $zip -Force
    # Preserve the previous build and any portable data; the stable path always points to the latest build.
    if (Test-Path -LiteralPath $publishDirectory) {
        $backup = $publishDirectory + '-previous-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff')
        $expectedParent = [System.IO.Path]::GetFullPath((Join-Path $projectRoot 'dist'))
        if ([System.IO.Path]::GetDirectoryName([System.IO.Path]::GetFullPath($publishDirectory)) -ne $expectedParent -or
            [System.IO.Path]::GetDirectoryName([System.IO.Path]::GetFullPath($backup)) -ne $expectedParent) { throw 'Publish path escaped dist.' }
        Move-Item -LiteralPath $publishDirectory -Destination $backup
    }
    Move-Item -LiteralPath $temporary -Destination $publishDirectory
    & (Join-Path $PSScriptRoot 'prune-builds.ps1') -Version $Version
    Write-Output "Portable app: $publishDirectory"
    Write-Output "Portable ZIP: $zip"
} finally { Pop-Location }







