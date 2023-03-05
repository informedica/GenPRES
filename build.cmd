echo Restoring dotnet tools...
dotnet tool restore

REM dotnet fake build -t %*
dotnet test GenPres.sln