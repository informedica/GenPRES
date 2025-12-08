FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

# Install node
# RUN curl -sL https://deb.nodesource.com/setup_14.x | bash
# RUN apt-get update && apt-get install -y nodejs
# update the repository sources list
# and install dependencies
RUN mkdir /usr/local/nvm
ENV NVM_DIR=/usr/local/nvm
ENV NODE_VERSION=22.12.0
RUN curl https://raw.githubusercontent.com/nvm-sh/nvm/v0.39.1/install.sh | bash \
    && . $NVM_DIR/nvm.sh \
    && nvm install $NODE_VERSION \
    && nvm alias default $NODE_VERSION \
    && nvm use default

ENV NODE_PATH=$NVM_DIR/v$NODE_VERSION/lib/node_modules
ENV PATH=$NVM_DIR/versions/node/v$NODE_VERSION/bin:$PATH

WORKDIR /workspace
COPY .config .config
RUN dotnet tool restore
COPY .paket .paket
COPY paket.references paket.references
COPY paket.dependencies paket.lock ./

FROM build AS app-build
ENV HUSKY=0
COPY Build.fsproj .
COPY Build.fs .
COPY Helpers.fs .
COPY src/Informedica.Utils.Lib src/Informedica.Utils.Lib
COPY src/Informedica.Agents.Lib src/Informedica.Agents.Lib
COPY src/Informedica.NLP.Lib src/Informedica.NLP.Lib
COPY src/Informedica.OTS.Lib src/Informedica.OTS.Lib
COPY src/Informedica.Logging.Lib src/Informedica.Logging.Lib
COPY src/Informedica.GenCORE.Lib src/Informedica.GenCORE.Lib
COPY src/Informedica.GenUNITS.Lib src/Informedica.GenUNITS.Lib
COPY src/Informedica.GenSOLVER.Lib src/Informedica.GenSOLVER.Lib
COPY src/Informedica.ZIndex.Lib src/Informedica.ZIndex.Lib
COPY src/Informedica.ZForm.Lib src/Informedica.ZForm.Lib
COPY src/Informedica.NKF.Lib src/Informedica.NKF.Lib
COPY src/Informedica.FTK.Lib src/Informedica.FTK.Lib
COPY src/Informedica.GenFORM.Lib src/Informedica.GenFORM.Lib
COPY src/Informedica.GenORDER.Lib src/Informedica.GenORDER.Lib
COPY src/Informedica.MCP.Lib src/Informedica.MCP.Lib
COPY src/Informedica.FHIR.Lib src/Informedica.FHIR.Lib
COPY src/Informedica.DataPlatform.Lib src/Informedica.DataPlatform.Lib
COPY src/Informedica.HIXConnect.Lib src/Informedica.HIXConnect.Lib
COPY src/Informedica.GenPRES.Shared src/Informedica.GenPRES.Shared
COPY src/Informedica.GenPRES.Server src/Informedica.GenPRES.Server
COPY src/Informedica.GenPRES.Client src/Informedica.GenPRES.Client
RUN dotnet run bundle


FROM mcr.microsoft.com/dotnet/aspnet:10.0
COPY --from=app-build /workspace/deploy /app

ENV GENPRES_LOG="1"
ENV GENPRES_PROD="1"
ENV GENPRES_DEBUG="0"
ARG GENPRES_URL_ARG
ENV GENPRES_URL_ID=$GENPRES_URL_ARG

WORKDIR /app
EXPOSE 8085
ENTRYPOINT [ "dotnet", "Informedica.GenPRES.Server.dll" ]