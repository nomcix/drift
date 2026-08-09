FROM node:24.19.0-alpine AS web-build
WORKDIR /workspace
COPY src/DirectiveDrift.Web/package.json src/DirectiveDrift.Web/package-lock.json ./
RUN npm ci
COPY src/DirectiveDrift.Web/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0.202 AS api-build
WORKDIR /source
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/ ./src/
RUN dotnet restore src/DirectiveDrift.Api/DirectiveDrift.Api.csproj --locked-mode
RUN dotnet publish src/DirectiveDrift.Api/DirectiveDrift.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.6 AS final
WORKDIR /app
RUN mkdir -p /data && chown app:app /data
COPY --from=api-build --chown=app:app /app/publish ./
COPY --from=web-build --chown=app:app /workspace/dist ./wwwroot
USER app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "DirectiveDrift.Api.dll"]
