FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore HattrickAI.Web.csproj
RUN dotnet publish HattrickAI.Web.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
ENV DOTNET_USE_POLLING_FILE_WATCHER=1
ENV ASPNETCORE_URLS=http://0.0.0.0:10000
ENV HATTRICKAI_VERSION_SOURCE=/app/VERSION
COPY --from=build /app/publish .
COPY VERSION /app/VERSION
EXPOSE 10000
# The UI no longer trusts the old semantic-version replacement system.
# Render's own deployed commit is written directly into wwwroot and becomes
# the single authoritative build identifier shown in the browser.
ENTRYPOINT ["sh", "-c", "printf '%s' \"${RENDER_GIT_COMMIT:-unknown}\" > /app/wwwroot/render-commit.txt && exec dotnet HattrickAI.Web.dll"]
