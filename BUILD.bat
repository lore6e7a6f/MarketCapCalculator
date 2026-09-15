@echo off

echo Building the MarketCapCalculator
echo -------------------------------
echo.
dotnet clean
dotnet restore
dotnet build
dotnet run --project MarketCapCalculator
pause > nul