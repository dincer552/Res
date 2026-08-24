# V2 CHPP integration plan

V2 keeps the new sync/analysis architecture while restoring the proven V1 CHPP OAuth flow.

Required environment variables:
- CHPP_CONSUMER_KEY
- CHPP_CONSUMER_SECRET
- CHPP_CALLBACK_URL (optional; otherwise use the request host callback)

Flow:
1. GET /api/status reports whether a valid CHPP session exists.
2. GET /auth/chpp/start redirects the browser to Hattrick OAuth.
3. GET /auth/chpp/callback stores the OAuth token/session and redirects to /.
4. The V2 UI polls /api/status and only enables sync/analysis after connected=true.
5. Until connected, diagnostics/progress/log panels stay hidden and data requests wait.
