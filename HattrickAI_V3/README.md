# HattrickAI V3

Temiz V3 iskeleti. Arayüz yalnızca maç dizilişini gösterir: rakibin son 11'i ve CHPP verilerinden hesaplanan bizim 11'imiz aynı tip saha üzerinde görünür. Sektör güçlü/zayıf hesapları kullanıcıya ayrı kart olarak gösterilmez.

## Render Docker settings

- Root Directory: boş bırak
- Environment: Docker
- Dockerfile Path: `HattrickAI_V3/Dockerfile`
- Docker Build Context Directory: `.`, yani repository root
- Docker Command: boş bırak

## Environment variables

- `CHPP_CONSUMER_SECRET`: mevcut Render değeriniz
- `CHPP_CONSUMER_KEY`: opsiyonel; boşsa mevcut V1 consumer key kullanılır

CHPP callback URL otomatik olarak `https://<servis-hostu>/auth/chpp/callback` şeklinde oluşturulur.

## V3 flow

CHPP -> takım/oyuncular -> yaklaşan maç -> rakibin son maçı -> rakip 11 -> arka planda sektör analizi -> hesaplanan 11 + saha yerleşimi.
