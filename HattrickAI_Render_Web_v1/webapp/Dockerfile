FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore HattrickAI.Web.csproj
RUN dotnet publish HattrickAI.Web.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
# Render/container inotify limits can be exhausted by .NET configuration
# FileSystemWatcher. Polling keeps config reload support without consuming
# additional inotify instances.
ENV DOTNET_USE_POLLING_FILE_WATCHER=1
ENV ASPNETCORE_URLS=http://0.0.0.0:10000
COPY --from=build /app/publish .
EXPOSE 10000
ENTRYPOINT ["dotnet", "HattrickAI.Web.dll"]
