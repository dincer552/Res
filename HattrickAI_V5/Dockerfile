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
COPY HattrickAI_V5/wwwroot/motor-logs.js /app/wwwroot/motor-logs.js
COPY HattrickAI_V5/wwwroot/motor-render.js /app/wwwroot/motor-render.js
COPY HattrickAI_V5/wwwroot/match-select.js /app/wwwroot/match-select.js
COPY HattrickAI_V5/wwwroot/formation-competition.js /app/wwwroot/formation-competition.js
COPY HattrickAI_V5/wwwroot/m9-prediction.js /app/wwwroot/m9-prediction.js
RUN sed -i \
  -e "s#const motors = \['M3','M4','M5','M6','M7','M7.2','M8','M9','M10','M6-B','M11'\];#const motorNames = {'M3':'Oyuncu Analizi','M4':'Formasyon Üretimi','M5':'11 Adayı Üretimi','M6':'Global Arama','M7':'Bölgesel Rating','M7.2':'Taktik Senaryo','M8':'Şans \& Eşleşme','M9':'Maç Tahmini','M10':'Formasyon Kararı','M6-B':'İkinci Arama / İyileştirme','M11':'Final Seçici'}; const displayMotorText = value => String(value ?? '').replace(/M7\\.2/g,'Taktik Senaryo').replace(/M6-B/g,'İkinci Arama / İyileştirme').replace(/M11/g,'Final Seçici').replace(/M10/g,'Formasyon Kararı').replace(/M9/g,'Maç Tahmini').replace(/M8/g,'Şans \& Eşleşme').replace(/M7/g,'Bölgesel Rating').replace(/M6/g,'Global Arama').replace(/M5/g,'11 Adayı Üretimi').replace(/M4/g,'Formasyon Üretimi').replace(/M3/g,'Oyuncu Analizi'); const motors = ['M3','M4','M5','M6','M7','M7.2','M8','M9','M10','M6-B','M11'];#" \
  -e 's#🧠 V5 Motor Logları • M3 → M11#🧠 V5 Motorlar#g' \
  -e 's#\${m}</b>#\${motorNames[m] || m}</b>#g' \
  -e 's#\${active\.motor}#\${motorNames[active.motor] || active.motor}#g' \
  -e 's#\${failed\.motor}#\${motorNames[failed.motor] || failed.motor}#g' \
  -e 's#\${esc(x\.message || '\''Bekliyor'\'')}#\${esc(displayMotorText(x.message || '\''Bekliyor'\''))}#g' \
  -e 's#\${failed\.message}#\${displayMotorText(failed.message)}#g' \
  -e 's#\${active\.message}#\${displayMotorText(active.message)}#g' \
  -e 's#\${log\.finalMessage}#\${displayMotorText(log.finalMessage)}#g' \
  /app/wwwroot/motor-logs.js
RUN sed -i 's#</body>#<script src="/copy-teams.js?v=2"></script><script src="/motor-logs.js?v=3"></script><script src="/motor-render.js?v=2"></script><script src="/match-select.js?v=2"></script><script src="/formation-competition.js?v=2"></script><script src="/m9-prediction.js?v=2"></script></body>#' /app/wwwroot/index.html
ENTRYPOINT ["dotnet","HattrickAI.V5.dll","--hostBuilder:reloadConfigOnChange=false"]
