FROM mcr.microsoft.com/dotnet/sdk:8.0 as build

# Install node
# RUN curl -sL https://deb.nodesource.com/setup_14.x | bash
# RUN apt-get update && apt-get install -y nodejs
RUN set -uex; \
    apt-get update; \
    apt-get install -y ca-certificates curl gnupg; \
    mkdir -p /etc/apt/keyrings; \
    curl -fsSL https://deb.nodesource.com/gpgkey/nodesource-repo.gpg.key \
     | gpg --dearmor -o /etc/apt/keyrings/nodesource.gpg; \
    NODE_MAJOR=18; \
    echo "deb [signed-by=/etc/apt/keyrings/nodesource.gpg] https://deb.nodesource.com/node_$NODE_MAJOR.x nodistro main" \
     > /etc/apt/sources.list.d/nodesource.list; \
    apt-get -qy update; \
    apt-get -qy install nodejs;


WORKDIR /workspace
COPY .config .config
RUN dotnet tool restore
COPY .paket .paket
COPY paket.dependencies paket.lock ./

FROM build as server-build
COPY src/Informedica.Utils.Lib src/Informedica.Utils.Lib
COPY src/Informedica.ZIndex.Lib src/Informedica.ZIndex.Lib
COPY src/Informedica.ZForm.Lib src/Informedica.ZForm.Lib
COPY src/Informedica.KinderFormularium.Lib src/Informedica.KinderFormularium.Lib
COPY src/Informedica.GenUnits.Lib src/Informedica.GenUnits.Lib
COPY src/Informedica.GenCore.Lib src/Informedica.GenCore.Lib
COPY src/Informedica.GenSolver.Lib src/Informedica.GenSolver.Lib
COPY src/Informedica.GenForm.Lib src/Informedica.GenForm.Lib
COPY src/Informedica.GenOrder.Lib src/Informedica.GenOrder.Lib
COPY src/Shared src/Shared
COPY src/Server src/Server
RUN cd src/Server && dotnet publish -c release -o ../../deploy


FROM build as client-build
COPY package.json package-lock.json ./
RUN npm install
COPY vite.config.mts ./
COPY src/Shared src/Shared
COPY src/Client src/Client
RUN npm run build


FROM mcr.microsoft.com/dotnet/aspnet:8.0
COPY --from=server-build /workspace/deploy /app
COPY --from=client-build /workspace/deploy /app/public
COPY src/Server/data /app/data

# setting this env variable disables file logging
ENV GENPRES_PROD="1"
WORKDIR /app
EXPOSE 8085
ENTRYPOINT [ "dotnet", "Server.dll" ]
