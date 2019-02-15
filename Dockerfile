FROM microsoft/dotnet:2.2-aspnetcore-runtime-alpine
COPY /deploy /
WORKDIR /Server
VOLUME [ "/data" ]
EXPOSE 8085
ENTRYPOINT [ "dotnet", "Server.dll" ]