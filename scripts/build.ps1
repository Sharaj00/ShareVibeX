param([ValidateSet('Debug','Release')][string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_CLI_HOME = Join-Path $projectRoot '.tools/cli'
$localSdk = Join-Path $projectRoot '.tools/dotnet/dotnet.exe'
$dotnetCommand = if (Test-Path -LiteralPath $localSdk) { $localSdk } else { (Get-Command dotnet -ErrorAction Stop).Source }
Push-Location $projectRoot
try {
    & $dotnetCommand restore ShareVibeX.slnx --locked-mode --nologo
    if ($LASTEXITCODE) { throw 'Dependency restore failed.' }
    & $dotnetCommand build ShareVibeX.slnx -c $Configuration --no-restore --nologo -m:1 /nodeReuse:false
    if ($LASTEXITCODE) { throw 'Build failed.' }
    & $dotnetCommand test ShareVibeX.slnx -c $Configuration --no-build --no-restore --nologo --blame-hang-timeout 30s
    if ($LASTEXITCODE) { throw 'Tests failed.' }
} finally { Pop-Location }
