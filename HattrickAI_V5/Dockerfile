FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY HattrickAI_V5/HattrickAI.V5.csproj HattrickAI_V5/
RUN dotnet restore HattrickAI_V5/HattrickAI.V5.csproj
COPY HattrickAI_V5 HattrickAI_V5
RUN dotnet publish HattrickAI_V5/HattrickAI.V5.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:10000
COPY --from=build /app/publish .
RUN sed -i 's#</style>#.slot-code,.slot-desc{display:none!important}</style>#' /app/wwwroot/index.html
ENTRYPOINT ["dotnet","HattrickAI.V5.dll","--hostBuilder:reloadConfigOnChange=false"]
