# HattrickAI Web

HattrickAI'nin Android/MAUI kabuğu çıkarılmış, Render üzerinde çalışacak ASP.NET Core 9 web sürümü.

## İçerik

- HOEngine simülasyon ve öneri motoru doğrudan backend'de çalışır.
- CHPP OAuth 1.0a consumer secret sunucuda Environment Variable olarak tutulur.
- CHPP takım, fikstür ve rakip geçmişi endpoint'leri hazırdır.
- 100–10.000 arası simülasyon API'si vardır.
- `wwwroot` arayüzü mobil ve masaüstü tarayıcı için hazırlanmıştır.

## Render

Render'da **Web Service** olarak deploy edilir. Dockerfile .NET 9 SDK ile build eder ve ASP.NET Core uygulamasını `PORT`/10000 üzerinden yayınlar.

Environment Variables:

- `CHPP_CONSUMER_KEY`
- `CHPP_CONSUMER_SECRET`

CHPP ürün ayarındaki callback adresi:

`https://<render-domain>/auth/chpp/callback`

Bu callback adresi CHPP uygulamasında kayıtlı olmalıdır.
