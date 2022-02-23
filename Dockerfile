FROM mcr.microsoft.com/dotnet/sdk:5.0 as build

# Install node
RUN curl -sL https://deb.nodesource.com/setup_14.x | bash
RUN apt-get update && apt-get install -y nodejs

WORKDIR /workspace
COPY .config .config
RUN dotnet tool restore
COPY .paket .paket
COPY paket.dependencies paket.lock ./

FROM build as server-build
COPY src/Shared src/Shared
COPY src/Server src/Server
RUN cd src/Server && dotnet publish -c release -o ../../deploy


FROM build as client-build
COPY package.json package-lock.json ./
RUN npm install
COPY webpack.config.js ./
COPY src/Shared src/Shared
COPY src/Client src/Client
RUN dotnet fable src/Client --run webpack


FROM mcr.microsoft.com/dotnet/aspnet:5.0-alpine
COPY --from=server-build /workspace/deploy /app
COPY --from=client-build /workspace/deploy /app
WORKDIR /app
EXPOSE 8085
ENTRYPOINT [ "dotnet", "Server.dll" ]
