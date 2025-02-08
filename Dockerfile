FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

# Install node
# RUN curl -sL https://deb.nodesource.com/setup_14.x | bash
# RUN apt-get update && apt-get install -y nodejs
# update the repository sources list
# and install dependencies
RUN mkdir /usr/local/nvm
ENV NVM_DIR /usr/local/nvm
ENV NODE_VERSION 22.11.0
RUN curl https://raw.githubusercontent.com/nvm-sh/nvm/v0.39.1/install.sh | bash \
    && . $NVM_DIR/nvm.sh \
    && nvm install $NODE_VERSION \
    && nvm alias default $NODE_VERSION \
    && nvm use default

ENV NODE_PATH $NVM_DIR/v$NODE_VERSION/lib/node_modules
ENV PATH $NVM_DIR/versions/node/v$NODE_VERSION/bin:$PATH

WORKDIR /workspace
COPY .config .config
RUN dotnet tool restore
COPY .paket .paket
COPY paket.references paket.references
COPY paket.dependencies paket.lock ./

FROM build AS app-build
COPY Build.fsproj .
COPY Build.fs .
COPY Helpers.fs .
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
COPY src/Client src/Client
RUN dotnet run bundle


FROM mcr.microsoft.com/dotnet/aspnet:9.0
COPY --from=app-build /workspace/deploy /app

ENV GENPRES_LOG="0"
ENV GENPRES_PROD="1"
ENV GENPRES_URL_ID="1IZ3sbmrM4W4OuSYELRmCkdxpN9SlBI-5TLSvXWhHVmA"
WORKDIR /app
EXPOSE 8085
ENTRYPOINT [ "dotnet", "Server.dll" ]
