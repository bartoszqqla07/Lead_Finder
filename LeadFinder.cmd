@echo off
rem Uruchamia LeadFinder (aplikacja webowa na localhost) i otwiera przegladarke.
rem Pierwsze uruchomienie buduje aplikacje i instaluje zaleznosci frontendu - moze potrwac 1-2 minuty.
cd /d "%~dp0"
echo Uruchamiam LeadFinder...
dotnet run --project src\LeadFinder.Web -c Release --launch-profile LeadFinder
if errorlevel 1 pause
