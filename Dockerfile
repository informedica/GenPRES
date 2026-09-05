# Pinned to the exact SDK patch in global.json (not the floating `10.0` tag) so Docker builds use
# the same feature-band compiler as CI — see DEVELOPMENT.md's "Why the SDK is pinned tightly"
# section (issue #447) for why a floating tag is unsafe here.
FROM mcr.microsoft.com/dotnet/sdk:10.0.302 AS build

WORKDIR /workspace
COPY global.json .
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
# Sourced from the root Directory.Build.props by the caller (see the
# `DockerBuild` FAKE target in Build.fs / DEVELOPMENT.md) so the image label
# always matches what was actually built, without duplicating the version here.
ARG APP_VERSION=0.0.0
LABEL org.opencontainers.image.version="${APP_VERSION}"

COPY --from=app-build /workspace/deploy /app

ENV GENPRES_LOG=0
ENV GENPRES_PROD="0"
ENV GENPRES_DEBUG="0"

# Application root: the directory containing data/ (cache, config, logs).
# The deploy bundle is copied to /app, so /app/data is the resolved data root.
# Set explicitly so AppPath resolves without relying on the cwd fallback.
ENV GENPRES_ROOT="/app"

# The defaults above (GENPRES_PROD=0) plus the public demo sheet ID below make a
# bare `docker run -p 8080:8085 informedica/genpres` start a working demo with no
# secrets (issue #541). They match what the image ships: /app/data/cache holds
# only the *.demo files, and demo mode is what reads them. Admin operations stay
# disabled because GENPRES_PASSWORD is empty.
#
# Production is an explicit opt-in at container runtime and needs all four of:
#
#   -e GENPRES_PROD=1
#   -e GENPRES_URL_ID="<proprietary_url_id>"
#   -e GENPRES_PASSWORD="<admin_password, 16+ chars>"
#   -v "$PWD/data/cache:/app/data/cache"   (production reads *.cache, not shipped)
#
# `docker compose up -d` with the repo-root compose.yaml wires all four from .env.
#
# SECURITY: the proprietary production GENPRES_URL_ID is a FAIR asset and MUST
# NOT be baked into the published image; inject it at runtime, ideally via a
# Docker / Kubernetes secret. The ID below is the public demo sheet already
# published in .env.example and the release workflow, so it leaks nothing.
ENV GENPRES_URL_ID=1IZ3sbmrM4W4OuSYELRmCkdxpN9SlBI-5TLSvXWhHVmA
ENV GENPRES_PASSWORD=

WORKDIR /app
EXPOSE 8085
ENTRYPOINT [ "dotnet", "Informedica.GenPRES.Server.dll" ]