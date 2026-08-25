FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY HattrickAI_V3/HattrickAI.V3.csproj HattrickAI_V3/
RUN dotnet restore HattrickAI_V3/HattrickAI.V3.csproj
COPY . .
RUN dotnet publish HattrickAI_V3/HattrickAI.V3.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:10000
ENV DOTNET_USE_POLLING_FILE_WATCHER=1
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "HattrickAI.V3.dll"]
