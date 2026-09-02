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
# Hattrick's Turkish team-spirit names are not the English enum translations.
# Keep the wire values stable, but render the official Turkish labels in V5.
RUN sed -i \
  -e 's#\[\x27Murderous\x27,\x27Öldürücü\x27\]#[\x27Murderous\x27,\x27Öldürücü\x27]#' \
  -e 's#\[\x27Furious\x27,\x27Öfkeli\x27\]#[\x27Furious\x27,\x27Köpürmüş\x27]#' \
  -e 's#\[\x27Irritated\x27,\x27Sinirli\x27\]#[\x27Irritated\x27,\x27Rahatsız\x27]#' \
  -e 's#\[\x27Composed\x27,\x27Soğukkanlı\x27\]#[\x27Composed\x27,\x27Kaynaşık\x27]#' \
  -e 's#\[\x27Calm\x27,\x27Sakin\x27\]#[\x27Calm\x27,\x27Huzurlu\x27]#' \
  -e 's#\[\x27Content\x27,\x27Memnun\x27\]#[\x27Content\x27,\x27Hoşnut\x27]#' \
  -e 's#\[\x27Satisfied\x27,\x27Tatmin olmuş\x27\]#[\x27Satisfied\x27,\x27Memnun\x27]#' \
  -e 's#\[\x27Delirious\x27,\x27Coşkulu\x27\]#[\x27Delirious\x27,\x27Coşkulu\x27]#' \
  -e 's#\[\x27WalkingOnClouds\x27,\x27Bulutların üzerinde\x27\]#[\x27WalkingOnClouds\x27,\x27Bulutların Üzerinde\x27]#' \
  -e 's#\[\x27ParadiseOnEarth\x27,\x27Cennette\x27\]#[\x27ParadiseOnEarth\x27,\x27Yeryüzünde Cennet\x27]#' \
  /app/wwwroot/index.html
RUN sed -i 's#</style>#.slot-code,.slot-desc,.slot:not(.filled) .slot-name,.slot:not(.filled) .slot-rating{display:none!important}</style>#' /app/wwwroot/index.html
COPY HattrickAI_V5/wwwroot/copy-teams.js /app/wwwroot/copy-teams.js
COPY HattrickAI_V5/wwwroot/offline-export.js /app/wwwroot/offline-export.js
COPY HattrickAI_V5/wwwroot/motor-logs.js /app/wwwroot/motor-logs.js
COPY HattrickAI_V5/wwwroot/motor-render.js /app/wwwroot/motor-render.js
COPY HattrickAI_V5/wwwroot/match-select.js /app/wwwroot/match-select.js
COPY HattrickAI_V5/wwwroot/formation-competition.js /app/wwwroot/formation-competition.js
RUN sed -i 's#</body>#<script src="/copy-teams.js"></script><script src="/offline-export.js"></script><script src="/motor-logs.js"></script><script src="/motor-render.js"></script><script src="/match-select.js"></script><script src="/formation-competition.js"></script></body>#' /app/wwwroot/index.html
ENTRYPOINT ["dotnet","HattrickAI.V5.dll","--hostBuilder:reloadConfigOnChange=false"]
