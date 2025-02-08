echo Restoring dotnet tools...
dotnet tool restore

dotnet run servertests
dotnet run bundle
