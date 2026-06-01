FROM node:22-bookworm-slim AS frontend
ARG BASE_PATH=""
WORKDIR /src
COPY frontend/package*.json ./frontend/
RUN cd frontend && npm ci
COPY frontend/ ./frontend/
RUN mkdir -p backend && cd frontend && VITE_BASE="${BASE_PATH}/" npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend
WORKDIR /src
COPY backend/ ./backend/
COPY --from=frontend /src/backend/wwwroot ./backend/wwwroot
RUN dotnet publish backend/Voixla.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS runtime
ARG BASE_PATH=""

RUN apt-get update \
    && apt-get install -y --no-install-recommends python3 python3-venv python3-pip curl bash ca-certificates \
    && python3 -m venv /opt/piper \
    && /opt/piper/bin/pip install --no-cache-dir piper-tts \
    && rm -rf /var/lib/apt/lists/*

COPY scripts/download-voices.sh /tmp/download-voices.sh
RUN bash /tmp/download-voices.sh /app/voices && rm /tmp/download-voices.sh

COPY --from=backend /app /app
WORKDIR /app

ENV ASPNETCORE_URLS=http://0.0.0.0:5005 \
    ASPNETCORE_ENVIRONMENT=Production \
    Piper__ExecutablePath=/opt/piper/bin/piper \
    Piper__VoicesDir=/app/voices \
    Piper__CacheDir=/app/cache \
    PathBase=${BASE_PATH}

EXPOSE 5005
ENTRYPOINT ["dotnet", "Voixla.Api.dll"]
