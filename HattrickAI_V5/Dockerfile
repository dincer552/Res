FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY HattrickAI_V5/HattrickAI.V5.csproj HattrickAI_V5/
RUN dotnet restore HattrickAI_V5/HattrickAI.V5.csproj
COPY HattrickAI_V5 HattrickAI_V5
RUN dotnet publish HattrickAI_V5/HattrickAI.V5.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:10000
ARG BUILD_SHA=dev
ENV V5_BUILD=${BUILD_SHA}
COPY --from=build /app/publish .
COPY HattrickAI_V5/wwwroot/tactic-display.js /app/wwwroot/tactic-display.js
COPY HattrickAI_V5/wwwroot/m9-prediction.js /app/wwwroot/m9-prediction.js
RUN sed -i 's#</body>#<script src="/tactic-display.js?v=1"></script></body>#' /app/wwwroot/index.html
ENTRYPOINT ["dotnet","HattrickAI.V5.dll","--hostBuilder:reloadConfigOnChange=false"]
