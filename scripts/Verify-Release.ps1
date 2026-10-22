[CmdletBinding()]
param(
    [switch]$SkipRestore
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repositoryRoot 'CoachHub.slnx'
$clientRoot = Join-Path $repositoryRoot 'client/coachhub-web'
$resultsRoot = Join-Path $repositoryRoot 'artifacts/test-results'

# Runs a native command and fails on its exit code only. Tools such as npm write warnings to stderr; Windows
# PowerShell 5.1 turns that output into errors, which 'Stop' would make fatal even when the command succeeded (P10A).
# The same script therefore behaves the same in Windows PowerShell 5.1 and PowerShell 7.
function Invoke-Native([string]$description, [scriptblock]$command) {
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        & $command
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previous
    }
    if ($exitCode -ne 0) {
        throw "$description failed with exit code $exitCode."
    }
}

Push-Location $repositoryRoot
try {
    $productionSettings = Get-Content -Raw 'src/CoachHub.API/appsettings.json' | ConvertFrom-Json
    if (-not [string]::IsNullOrWhiteSpace($productionSettings.Authentication.Jwt.SigningKey)) {
        throw 'Production JWT signing keys must be supplied by the deployment environment.'
    }
    if ($productionSettings.Authentication.BootstrapAdmin.Enabled) {
        throw 'Production administrator bootstrap must be disabled in tracked configuration.'
    }
    if ($productionSettings.Media.Provider -ne 'S3') {
        throw 'Tracked production configuration must select private S3-compatible media storage.'
    }

    if (-not $SkipRestore) {
        Invoke-Native 'Solution restore' { dotnet restore $solution }
    }

    Invoke-Native 'Release build' { dotnet build $solution --configuration Release --no-restore }

    if (Test-Path $resultsRoot) {
        Remove-Item -LiteralPath $resultsRoot -Recurse -Force
    }
    New-Item -ItemType Directory -Path $resultsRoot -Force | Out-Null

    Invoke-Native 'Backend tests' {
        dotnet test $solution `
            --configuration Release `
            --no-build `
            --logger 'trx' `
            --results-directory $resultsRoot `
            --collect 'XPlat Code Coverage'
    }

    Push-Location $clientRoot
    try {
        if (-not $SkipRestore) {
            Invoke-Native 'Angular dependency restore' { npm ci --no-audit --prefer-offline }
        }

        Invoke-Native 'Angular tests' { npm test -- --watch=false --isolate=false }

        Invoke-Native 'Angular production build' { npm run build }
    }
    finally {
        Pop-Location
    }

    Write-Host 'CoachHub release verification passed.' -ForegroundColor Green
}
finally {
    Pop-Location
}
