param(
    [ValidateSet('Build', 'Test', 'Publish')][string]$Action = 'Test',
    [ValidateSet('win-x64', 'linux-x64')][string]$Runtime = 'win-x64'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    $sdk = Join-Path $root '.tools/dotnet/dotnet.exe'
    if (Test-Path $sdk) {
        $env:DOTNET_CLI_HOME = Join-Path $root '.tools/cli-home'
        $env:NUGET_PACKAGES = Join-Path $root '.tools/nuget'
    } else { $sdk = (Get-Command dotnet -ErrorAction Stop).Source }
    $config = Join-Path $root 'NuGet.Config'
    if ($Action -eq 'Publish') {
        & $sdk publish src/Desktop/ZipSelect.Desktop.csproj -c Release -r $Runtime --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true "-p:RestoreConfigFile=$config" -o "dist/$Runtime"
    } else {
        & $sdk restore tests/ZipSelect.Tests.csproj --configfile $config
        if ($LASTEXITCODE -ne 0) { throw 'Restauration échouée.' }
        if ($Action -eq 'Test') { & $sdk run --project tests/ZipSelect.Tests.csproj --no-restore -- "artifacts/tests/$([Guid]::NewGuid())" }
        else { & $sdk build src/Desktop/ZipSelect.Desktop.csproj --no-restore }
    }
    if ($LASTEXITCODE -ne 0) { throw "Échec de l’action $Action." }
} finally { Pop-Location }
