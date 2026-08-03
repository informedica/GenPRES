FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /workspace
COPY .config .config
RUN dotnet tool restore
COPY .paket .paket
COPY paket.references paket.references
COPY paket.dependencies paket.lock ./
# Each library's own Directory.Build.props imports this root file (via
# GetPathOfFileAbove) to share the single curated <Version>. Without it
# present at /workspace, that Import resolves to an empty path and MSBuild
# fails with MSB4020.
COPY Directory.Build.props .

FROM build AS app-build

# Install node
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

ENV HUSKY=0
COPY Build.fsproj .
COPY Build.fs .
COPY Helpers.fs .
COPY src/ src/
# The Bundle target copies the curated runtime data (the cache) from the
# repo-root data/ folder into deploy/data.
COPY data/cache data/cache
RUN dotnet run bundle


FROM mcr.microsoft.com/dotnet/aspnet:10.0

# Curated single version number for the whole app (server, client, libraries).
# Sourced from the root Directory.Build.props by the caller (see docker-local.sh /
# docker-amd64.sh templates in DEVELOPMENT.md) so the image label always matches
# what was actually built, without duplicating the version here.
ARG APP_VERSION=0.0.0
LABEL org.opencontainers.image.version="${APP_VERSION}"

COPY --from=app-build /workspace/deploy /app

ENV GENPRES_LOG=0
ENV GENPRES_PROD="1"
ENV GENPRES_DEBUG="0"

# Application root: the directory containing data/ (cache, config, logs).
# The deploy bundle is copied to /app, so /app/data is the resolved data root.
# Set explicitly so AppPath resolves without relying on the cwd fallback.
ENV GENPRES_ROOT="/app"

# SECURITY: GENPRES_URL_ID is a proprietary FAIR asset and MUST NOT be
# baked into the published image. Inject it at container runtime instead, e.g.:
#
#   docker run -e GENPRES_URL_ID="<your_url_id>" \
#              -e GENPRES_PASSWORD="<your_admin_password>" \
#              -p 8080:8085 halcwb/genpres
#
# Or via a Docker / Kubernetes secret. The server fails closed (no admin
# operations) when GENPRES_PASSWORD is unset, and refuses to start when
# GENPRES_URL_ID is unset.
ENV GENPRES_URL_ID=
ENV GENPRES_PASSWORD=

WORKDIR /app
EXPOSE 8085
ENTRYPOINT [ "dotnet", "Informedica.GenPRES.Server.dll" ]