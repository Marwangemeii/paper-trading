# Build context: repository root (see docker-compose.yml).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first so this layer is cached until a project file changes.
COPY global.json Directory.Build.props .editorconfig ./
COPY src/PaperTrading.Domain/PaperTrading.Domain.csproj src/PaperTrading.Domain/
COPY src/PaperTrading.Api/PaperTrading.Api.csproj src/PaperTrading.Api/
RUN dotnet restore src/PaperTrading.Api/PaperTrading.Api.csproj

COPY src/ src/
RUN dotnet publish src/PaperTrading.Api/PaperTrading.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

# SQLite lives on a separate path so it can be mounted as a volume. Run as the image's non-root user.
RUN mkdir -p /app/data && chown -R $APP_UID /app/data
USER $APP_UID

ENV ASPNETCORE_HTTP_PORTS=8080 \
    ConnectionStrings__Trading="Data Source=/app/data/trading.db"
EXPOSE 8080
ENTRYPOINT ["dotnet", "PaperTrading.Api.dll"]
