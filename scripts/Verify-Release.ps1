[CmdletBinding()]
param(
    [switch]$SkipRestore
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repositoryRoot 'CoachHub.slnx'
$clientRoot = Join-Path $repositoryRoot 'client\coachhub-web'
$resultsRoot = Join-Path $repositoryRoot 'artifacts\test-results'

function Assert-LastCommandSucceeded([string]$description) {
    if ($LASTEXITCODE -ne 0) {
        throw "$description failed with exit code $LASTEXITCODE."
    }
}

Push-Location $repositoryRoot
try {
    $productionSettings = Get-Content -Raw 'src\CoachHub.API\appsettings.json' | ConvertFrom-Json
    if (-not [string]::IsNullOrWhiteSpace($productionSettings.Authentication.Jwt.SigningKey)) {
        throw 'Production JWT signing keys must be supplied by the deployment environment.'
    }
    if ($productionSettings.Authentication.BootstrapAdmin.Enabled) {
        throw 'Production administrator bootstrap must be disabled in tracked configuration.'
    }
    if ($productionSettings.Media.Provider -ne 'External') {
        throw 'Tracked production configuration must require private external media storage.'
    }

    if (-not $SkipRestore) {
        dotnet restore $solution
        Assert-LastCommandSucceeded 'Solution restore'
    }

    dotnet build $solution --configuration Release --no-restore
    Assert-LastCommandSucceeded 'Release build'

    if (Test-Path $resultsRoot) {
        Remove-Item -LiteralPath $resultsRoot -Recurse -Force
    }
    New-Item -ItemType Directory -Path $resultsRoot -Force | Out-Null

    dotnet test $solution `
        --configuration Release `
        --no-build `
        --logger 'trx' `
        --results-directory $resultsRoot `
        --collect 'XPlat Code Coverage'
    Assert-LastCommandSucceeded 'Backend tests'

    Push-Location $clientRoot
    try {
        if (-not $SkipRestore) {
            npm ci
            Assert-LastCommandSucceeded 'Angular dependency restore'
        }

        npm test -- --watch=false
        Assert-LastCommandSucceeded 'Angular tests'

        npm run build
        Assert-LastCommandSucceeded 'Angular production build'
    }
    finally {
        Pop-Location
    }

    Write-Host 'CoachHub release verification passed.' -ForegroundColor Green
}
finally {
    Pop-Location
}
