# Erp.Api.csproj tiene ~30 ProjectReference hacia backend/Modules/*/*.csproj
# (monolito modular): dotnet restore necesita ver todos esos .csproj para
# resolver las referencias, pero copiar también el código fuente (.cs) en
# este punto invalidaría la capa de restore en cada cambio de código, aunque
# no cambie ningún .csproj. Este stage aísla solo los .csproj (preservando su
# estructura de carpetas) para que la capa de restore solo se invalide cuando
# cambian las referencias/paquetes, no en cada edición de código.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS csproj-only
WORKDIR /src
COPY backend/ .
# NTFS no distingue Api/API; Docker en Linux sí — alinear con las ProjectReference (*.csproj).
RUN if [ -d Modules/Inventory/API ]; then rm -rf Modules/Inventory/Api; mv Modules/Inventory/API Modules/Inventory/Api; fi
RUN find . -type f ! -name '*.csproj' -delete && find . -type d -empty -delete

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY --from=csproj-only /src .
RUN dotnet restore "Erp.Api/Erp.Api.csproj"

# Ahora sí se copia el código fuente completo para compilar/publicar.
COPY backend/ .
RUN if [ -d Modules/Inventory/API ]; then rm -rf Modules/Inventory/Api; mv Modules/Inventory/API Modules/Inventory/Api; fi

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
