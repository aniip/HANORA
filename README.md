# HANORA CAFE

Aplikasi full-stack HANORA CAFE: website publik, pemesanan via QR meja, dashboard kasir,
dashboard owner, laporan, dan pembayaran QRIS Midtrans.

## Struktur

```
backend/                 ASP.NET Core (.NET 10) + EF Core + PostgreSQL
frontend/                React 19 + Vite + TypeScript + Tailwind
database/                Script demo data & catatan database
deploy/                  Caddyfile untuk production
docs/                    Dokumentasi arsitektur, API, keamanan, produksi
scripts/                 Backup, restore, healthcheck
docker-compose.yml       Development (PostgreSQL + API + web)
docker-compose.prod.yml  Production (Caddy + API + web + PostgreSQL)
.env.example             Contoh environment development
.env.production.example  Contoh environment production/staging
```

## Menjalankan Development

### Backend

```bash
cd backend
dotnet restore
dotnet run --project src/Hanora.Api
```

API: `http://localhost:5080`

### Frontend

```bash
cd frontend
npm install
npm run dev
```

Web: `http://localhost:5173`

Vite mem-proxy `/api` ke `http://localhost:5080`.

### Database

Gunakan PostgreSQL 13+ (disarankan 17/18), lalu set:

```powershell
$env:ConnectionStrings__Default='Host=localhost;Port=5432;Database=hanora;Username=hanora;Password=...;Maximum Pool Size=20'
```

Migrasi dan seed awal:

```bash
cd backend
dotnet run --project src/Hanora.Api -- --migrate --seed
```

Data demo (opsional):

```bash
psql "host=... dbname=hanora user=... sslmode=require" \
  -v ON_ERROR_STOP=1 -f database/demo-data.sql
```

## Menjalankan Production / Staging

```bash
cp .env.production.example .env.production
chmod 600 .env.production
# isi DOMAIN, password DB, JWT_SIGNING_KEY, Midtrans, owner awal

docker compose --env-file .env.production -f docker-compose.prod.yml build
docker compose --env-file .env.production -f docker-compose.prod.yml up -d postgres
docker compose --env-file .env.production -f docker-compose.prod.yml run --rm api --migrate --seed
docker compose --env-file .env.production -f docker-compose.prod.yml up -d
```

Caddy menangani HTTPS otomatis. Hanya port 80/443 yang perlu dibuka.

Panduan lengkap: [docs/PRODUCTION.md](docs/PRODUCTION.md).

## Midtrans

- Default development/demo: **simulation** (tidak memanggil Midtrans).
- Untuk uji integrasi, ganti ke **sandbox** dan isi:
  - `MIDTRANS_MODE=sandbox`
  - `MIDTRANS_SERVER_KEY=SB-Mid-server-...`
  - `MIDTRANS_CLIENT_KEY=SB-Mid-client-...`
- Webhook: `https://<domain>/api/v1/webhooks/midtrans`.
- Setelah siap go-live, ganti ke `MIDTRANS_MODE=production` dengan kredensial production.
- Mode `simulation` **ditolak** saat `ASPNETCORE_ENVIRONMENT=Production`.

Panduan: [docs/MIDTRANS_SETUP.md](docs/MIDTRANS_SETUP.md).

## QR Meja

- QR dibuat dari `/owner/meja`.
- Development: `APP_BASE_URL` boleh IP LAN, misalnya `http://192.168.1.10:5173` (HP harus satu Wi-Fi).
- Production: `APP_BASE_URL` wajib domain HTTPS, misalnya `https://cafe.example.com`.
- Setelah deploy, unduh/cetak ulang QR agar berisi domain production.

## Test

```bash
cd backend
dotnet test Hanora.slnx
```

Integration test memakai Testcontainers dan membutuhkan Docker.

## Dokumentasi

| Dokumen | Isi |
|---|---|
| [docs/MENJALANKAN.md](docs/MENJALANKAN.md) | Panduan lokal, Docker, verifikasi |
| [docs/PRODUCTION.md](docs/PRODUCTION.md) | Deploy VPS, Caddy/TLS, backup, restore, monitoring |
| [docs/MIDTRANS_SETUP.md](docs/MIDTRANS_SETUP.md) | Sandbox/production Midtrans |
| [docs/API.md](docs/API.md) | Kontrak REST |
| [docs/ARSITEKTUR.md](docs/ARSITEKTUR.md) | Arsitektur sistem |
| [docs/DATABASE.md](docs/DATABASE.md) | Skema database |
| [docs/KEAMANAN.md](docs/KEAMANAN.md) | Kontrol keamanan |
| [docs/KEPUTUSAN_TEKNIS.md](docs/KEPUTUSAN_TEKNIS.md) | ADR |

## Catatan Keamanan

- Jangan commit `.env`, `.env.production`, password database, atau server key Midtrans.
- `appsettings.json` hanya berisi default development.
- Ganti password owner/cashier setelah login pertama.
- Jangan membuka port `5080` atau `5432` ke internet.
- Gunakan HTTPS di production.
