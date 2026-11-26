<#
.SYNOPSIS
    Analyse TradX avec SonarQube (local, Docker) : qualité, bugs, failles de sécurité, couverture des tests.

.DESCRIPTION
    Prérequis :
      1. docker compose --profile quality up -d     (SonarQube sur http://localhost:9000)
      2. Se connecter (admin / admin la première fois, changer le mot de passe),
         créer un projet « tradx » puis générer un jeton d'analyse (My Account > Security).
      3. Mettre le jeton dans la variable d'environnement SONAR_TOKEN (jamais dans le code ni dans Git) :
           $env:SONAR_TOKEN = "<votre jeton>"
      4. Lancer :  ./scripts/sonar.ps1

    Les migrations EF Core (code généré) sont exclues de l'analyse.
#>
param(
    [string]$HostUrl = "http://localhost:9000",
    [string]$ProjectKey = "tradx"
)

$ErrorActionPreference = "Stop"

if (-not $env:SONAR_TOKEN) {
    throw "La variable d'environnement SONAR_TOKEN est vide. Voir l'aide : Get-Help ./scripts/sonar.ps1"
}

Push-Location (Join-Path $PSScriptRoot "..")
try {
    Remove-Item -Recurse -Force TestResults -ErrorAction SilentlyContinue
    dotnet tool restore
    dotnet sonarscanner begin /k:"$ProjectKey" /d:sonar.host.url="$HostUrl" /d:sonar.token="$env:SONAR_TOKEN" `
        /d:sonar.cs.opencover.reportsPaths="TestResults/**/coverage.opencover.xml" `
        /d:sonar.exclusions="**/Migrations/**"
    dotnet build -c Release
    dotnet test --no-build -c Release --collect:"XPlat Code Coverage;Format=opencover" --results-directory TestResults
    dotnet sonarscanner end /d:sonar.token="$env:SONAR_TOKEN"
    Write-Host "Rapport disponible sur $HostUrl/dashboard?id=$ProjectKey"
}
finally {
    Pop-Location
}
