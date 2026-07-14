FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY iota-sync-docker/IotaSync.Docker.csproj iota-sync-docker/
RUN dotnet restore iota-sync-docker/IotaSync.Docker.csproj
COPY iota-sync-docker/ iota-sync-docker/
RUN dotnet publish iota-sync-docker/IotaSync.Docker.csproj -c Release -o /out --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /out ./
ENV ASPNETCORE_URLS=http://+:8080 IOTA_DATA=/data
VOLUME ["/data", "/servers"]
EXPOSE 8080
ENTRYPOINT ["dotnet", "IotaSync.Docker.dll"]
