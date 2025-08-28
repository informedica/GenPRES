set GENPRES_DEBUG=1


echo Restoring dotnet tools...
dotnet tool restore

dotnet run servertests
