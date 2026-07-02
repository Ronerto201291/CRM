FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Erp.Api.csproj tiene ~30 ProjectReference hacia backend/Modules/*/*.csproj
# (monolito modular) - un restore parcial copiando solo los 4 csproj del core
# falla porque dotnet restore no puede resolver esas referencias de proyecto.
# Se copia todo el código fuente antes de restaurar (se sacrifica la capa de
# caché de "solo csproj", pero garantiza que el restore funcione).
COPY backend/ .
RUN dotnet restore "Erp.Api/Erp.Api.csproj"

WORKDIR "/src/Erp.Api"
RUN dotnet build "Erp.Api.csproj" -c Release -o /app/build
RUN dotnet publish "Erp.Api.csproj" -c Release -o /app/publish

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

# Install Tesseract OCR + Spanish/English language packs for expense document processing
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        tesseract-ocr \
        tesseract-ocr-spa \
        tesseract-ocr-eng \
        curl \
    && rm -rf /var/lib/apt/lists/*

# Run as non-root user (Ubuntu base usa groupadd/useradd, no addgroup/adduser de Alpine)
RUN groupadd --system --gid 1001 appgroup \
    && useradd --system --uid 1001 --gid appgroup appuser

WORKDIR /app
COPY --from=build /app/publish .
RUN chown -R appuser:appgroup /app
USER appuser

# Puerto 8080 — sobreescrito por ASPNETCORE_URLS=http://+:8080 en .env/docker-compose
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
    CMD curl -f http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "Erp.Api.dll"]
