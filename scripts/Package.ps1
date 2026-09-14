param([ValidateSet('win-x64', 'linux-x64')][string]$Runtime = 'win-x64')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    & "$PSScriptRoot/Build.ps1" -Action Publish -Runtime $Runtime
    $version = (Get-Content VERSION -Raw).Trim()
    $output = "dist/$Runtime"
    $releases = 'dist/packages'
    $files = @()
    New-Item -ItemType Directory -Path $releases -Force | Out-Null
    Copy-Item -LiteralPath docs/Utilisation.txt -Destination "$output/Utilisation.txt" -Force
    Copy-Item -LiteralPath docs/Usage.txt -Destination "$output/Usage.txt" -Force
    if ($Runtime -eq 'win-x64') {
        $executable = "$releases/ZIP-Select-$version-$Runtime.exe"
        Copy-Item -LiteralPath "$output/ZIP-Select.exe" -Destination $executable -Force
        $files += $executable
        $package = "$releases/ZIP-Select-$version-$Runtime.zip"
        Compress-Archive -LiteralPath "$output/ZIP-Select.exe", "$output/Utilisation.txt", "$output/Usage.txt" -DestinationPath $package -Force
    } else {
        $package = "$releases/ZIP-Select-$version-$Runtime.tar.gz"
        & tar -czf $package -C $output ZIP-Select Utilisation.txt Usage.txt
        if ($LASTEXITCODE -ne 0) { throw 'Création du paquet Linux échouée.' }
    }
    $files += $package
    foreach ($file in $files) {
        $hash = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant()
        "$hash  $([IO.Path]::GetFileName($file))" | Set-Content -LiteralPath "$file.sha256" -Encoding ascii
    }
    Write-Host "Paquet prêt : $package"
} finally { Pop-Location }
