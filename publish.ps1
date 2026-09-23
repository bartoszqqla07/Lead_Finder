# Buduje samodzielną wersję LeadFindera do folderu dist\ (LeadFinder.Web.exe + wwwroot).
# Wymaga zainstalowanego .NET 8 Runtime na komputerze docelowym (framework-dependent, ~5 MB).
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

Write-Host 'Buduję frontend...'
Push-Location src\LeadFinder.Web\ClientApp
npm ci --no-audit --no-fund
npm run build
Pop-Location

# Frontend jest już w wwwroot, więc MSBuild nie musi go budować ponownie.
Write-Host 'Publikuję aplikację...'
dotnet publish src\LeadFinder.Web -c Release -r win-x64 --self-contained false `
    -p:PublishSingleFile=true -p:SkipClientBuild=true -o dist

Write-Host ''
Write-Host 'Gotowe: dist\LeadFinder.Web.exe (uruchom dwuklikiem).'
