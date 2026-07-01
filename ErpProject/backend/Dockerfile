FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj and restore as distinct layers
COPY ["backend/Erp.Api/Erp.Api.csproj", "Erp.Api/"]
COPY ["backend/Erp.Application/Erp.Application.csproj", "Erp.Application/"]
COPY ["backend/Erp.Domain/Erp.Domain.csproj", "Erp.Domain/"]
COPY ["backend/Erp.Infrastructure/Erp.Infrastructure.csproj", "Erp.Infrastructure/"]
RUN dotnet restore "Erp.Api/Erp.Api.csproj"

# Copy everything else and build
COPY backend/ .
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
