# PANDUAN MENJALANKAN — HANORA CAFE

**Tahap:** 10 (testing, hardening & dokumentasi production)
**Tanggal:** 8 November 2026

---

## 1. Prasyarat

| Perangkat | Versi minimum |
|---|---|
| .NET SDK | 10.0 |
| Node.js | 22.12 (disarankan 24 LTS) |
| npm | 10 |
| Docker Desktop / Docker Engine | 24 |
| Git | opsional |

Cek versi:

```bash
dotnet --version
node --version
npm --version
docker --version
```

---

## 2. Struktur

```
hanora-app/
├─ backend/
│  ├─ src/Hanora.Api
│  ├─ src/Hanora.Application
│  ├─ src/Hanora.Domain
│  ├─ src/Hanora.Infrastructure
│  └─ tests/
├─ frontend/
├─ database/
├─ docs/
├─ docker-compose.yml
└─ .env.example
```

Prototype lama tetap berada di root `HANORA-CAFE` dan tidak dipindahkan.

---

## 3. Opsi A — Development Lokal (disarankan)

### 3.1 Siapkan environment

Dari folder `hanora-app`:

```bash
copy .env.example .env
```

Di PowerShell:

```powershell
Copy-Item .env.example .env
```

Ganti `POSTGRES_PASSWORD` di `.env` dengan password lokal Anda. Jangan commit `.env`.

Isi juga akun owner awal:

```
SEED_OWNER_EMAIL=owner@hanoracafe.id
SEED_OWNER_PASSWORD=password-kuat-minimal-12-karakter
```

> Owner hanya dibuat jika belum ada akun dengan role Owner. Setelah dibuat, mengubah environment tidak mengubah password lama. Gunakan halaman **Ganti password** di aplikasi.

> Untuk development, `backend/src/Hanora.Api/appsettings.json` sudah memiliki connection string lokal dengan password `hanora_dev_password`. Jika Anda mengganti password di `.env`, samakan juga `ConnectionStrings__Default` atau set environment variable sebelum menjalankan backend.

### 3.2 Jalankan PostgreSQL

Pastikan Docker Desktop/daemon aktif.

```bash
docker compose up -d postgres
docker compose ps
```

PostgreSQL mendengarkan di `127.0.0.1:5432`.

Cek log jika gagal:

```bash
docker compose logs postgres
```

Migration dan seed otomatis aktif di environment Development (`Database:AutoMigrate=true`, `Database:Seed=true`). Saat backend dijalankan, EF Core akan menerapkan migration dan membuat role, pengaturan default, serta akun owner dari environment.

Jika ingin menjalankan migration secara eksplisit:

```bash
cd backend
dotnet ef database update \
  --project src/Hanora.Infrastructure \
  --startup-project src/Hanora.Api
```

Production harus memakai migration eksplisit; jangan mengandalkan auto-migrate.

### 3.3 Jalankan backend

PowerShell:

```powershell
cd backend
$env:SEED_OWNER_EMAIL="owner@hanoracafe.id"
$env:SEED_OWNER_PASSWORD="password-kuat-minimal-12-karakter"
dotnet run --project src/Hanora.Api
```

Bash:

```bash
cd backend
SEED_OWNER_EMAIL="owner@hanoracafe.id" \
SEED_OWNER_PASSWORD="password-kuat-minimal-12-karakter" \
dotnet run --project src/Hanora.Api
```

Alamat:

- API: <http://localhost:5080>
- Liveness: <http://localhost:5080/api/v1/health>
- Readiness: <http://localhost:5080/api/v1/health/ready>
- Dokumentasi OpenAPI (Development): <http://localhost:5080/scalar/v1>

Akun owner yang dibuat otomatis memiliki `mustChangePassword=true`. Login pertama akan diarahkan ke halaman ganti password.

### 3.4 Jalankan frontend

Buka terminal lain:

```bash
cd frontend
npm install
npm run dev
```

Alamat: <http://localhost:5173>

Vite mem-proxy `/api` ke `http://localhost:5080`, sehingga tidak ada masalah CORS pada development.

### 3.5 Login dan ganti password

1. Buka <http://localhost:5173/masuk>.
2. Masuk dengan `SEED_OWNER_EMAIL` dan `SEED_OWNER_PASSWORD`.
3. Pada login pertama, aplikasi mengarahkan ke `/ganti-password`.
4. Setelah password diganti, semua sesi lama dicabut dan Anda diminta login kembali.
5. Owner dapat membuat akun kasir melalui endpoint `POST /api/v1/owner/users` (UI manajemen user menyusul pada tahap owner).

### 3.6 Website publik dan katalog

Katalog awal sengaja kosong. Setelah login sebagai owner:

1. Buka `/owner/kategori`, buat kategori pertama.
2. Buka `/owner/produk`, tambahkan produk, harga, deskripsi, dan ketersediaan.
3. Gunakan tombol gambar di tabel produk untuk mengunggah foto (JPEG/PNG/WebP, maksimal 2 MB).
4. Buka `/owner/pengaturan` untuk mengisi alamat, jam buka, telepon, Instagram, dan URL peta.
5. Buka `/`, `/menu`, `/galeri`, dan `/lokasi` untuk melihat hasilnya.

Gambar disimpan di volume `uploads` dan disajikan melalui `/media/...`.

### 3.7 Meja, QR, dan pemesanan customer

1. Buka `/owner/meja`, tambahkan meja (misalnya nomor `01`, nama `Meja 01`).
2. Klik ikon QR untuk melihat dan mengunduh QR meja.
3. Pastikan `APP_BASE_URL` sesuai alamat yang dapat diakses customer:
   - Docker lokal: `http://localhost:8080` (hanya untuk browser di mesin yang sama).
   - HP di Wi-Fi yang sama: `http://<IP-LAN-laptop>:8080`.
   - Production: `https://domain-anda`.
4. Setelah `APP_BASE_URL` diubah, restart API dan unduh ulang QR.
5. Scan QR → pilih menu → keranjang → checkout.
6. Isi nama customer dan kirim pesanan. Pesanan tunai berstatus **Menunggu Konfirmasi**.
7. Buka `/kasir` sebagai owner/kasir untuk melihat antrean pesanan.
8. Customer dapat memantau status di `/pesanan/<trackingToken>`.

Catatan:
- QRIS sudah aktif di UI checkout; lihat bagian 3.8.
- Regenerasi QR mencabut QR lama secara langsung.

### 3.8 Pembayaran QRIS Midtrans

Secara default aplikasi berjalan pada **mode simulation**, sehingga pembayaran
QRIS dapat diuji tanpa akun Midtrans:

1. Checkout dengan metode **QRIS**.
2. Aplikasi membuka `/pesanan/{trackingToken}/simulasi` dengan banner
   **Mode simulasi**.
3. Tekan **Simulasikan pembayaran berhasil**.
4. Pesanan menjadi **Diproses**, pembayaran menjadi **Lunas**, dan income
   tercatat.

Untuk mode sandbox/production, isi environment berikut lalu restart API:

```env
MIDTRANS_MODE=sandbox
MIDTRANS_SERVER_KEY=SB-Mid-server-...
MIDTRANS_CLIENT_KEY=SB-Mid-client-...
MIDTRANS_MERCHANT_ID=...
MIDTRANS_QRIS_ENABLED=true
```

Daftarkan webhook di dashboard Midtrans:

```
https://domain-anda/api/v1/webhooks/midtrans
```

Panduan lengkap, checklist production, dan troubleshooting ada di
[docs/MIDTRANS_SETUP.md](MIDTRANS_SETUP.md).

### 3.9 Operasional kasir

1. Login sebagai owner atau kasir, buka `/kasir`.
2. Pilih pesanan pada tabel untuk membuka detail.
3. Untuk pesanan tunai berstatus **Menunggu Konfirmasi**, tekan **Konfirmasi Tunai**. Status berubah menjadi **Diproses**, pembayaran menjadi **Lunas**, dan income dicatat.
4. Setelah pesanan dibuat, tekan **Siap Disajikan**, lalu **Selesaikan**.
5. Gunakan tombol **Batalkan** untuk pesanan yang masih menunggu pembayaran/konfirmasi.
6. Buka `/kasir/riwayat` untuk melihat transaksi lunas dengan filter tanggal, metode, dan status.
7. Halaman tracking customer memperbarui status otomatis setiap 5 detik.

Halaman beranda menampilkan status API, database, dan penyimpanan secara langsung dari endpoint health. Jika PostgreSQL belum berjalan, status database akan menunjukkan **Belum berjalan** (bukan status palsu).

### 3.10 Dashboard owner, keuangan, dan laporan

1. Login sebagai owner dan buka `/owner`.
2. Pilih rentang tanggal atau preset **7 hari / 30 hari / Bulan ini**.
3. Ringkasan menampilkan pendapatan, pengeluaran, laba bersih, pesanan aktif,
   rata-rata transaksi, serta komposisi QRIS/tunai.
4. Grafik pendapatan dapat dilihat **harian** atau **bulanan**. Produk terlaris
   dihitung dari pesanan yang sudah lunas.
5. Buka `/owner/pengeluaran`:
   - tab **Pengeluaran** untuk mencatat biaya (Bahan baku, Operasional,
     Perlengkapan, SDM, Lainnya);
   - tab **Pemasukan** untuk mencatat pemasukan manual di luar pesanan.
   - Pemasukan dari pesanan tidak dapat dihapus manual; gunakan proses
     pembatalan/refund.
6. Buka `/owner/laporan`:
   - pilih rentang tanggal, isi laporan, dan format **PDF** atau **Excel**;
   - tekan **Unduh**. File dibuat di server dari data database.
   - Rentang maksimal 1 tahun per permintaan.
7. Buka `/owner/pengguna` untuk menambah kasir/owner, mengubah nama/email/role,
   atau menonaktifkan akun. Owner tidak dapat menonaktifkan akunnya sendiri.

> Export PDF memakai QuestPDF dan Excel memakai ClosedXML. Image Docker API
> sudah menyertakan fontconfig dan font dasar agar PDF dapat dibuat di Linux.

### 3.11 Hardening lokal

Development sekarang juga memakai kontrol production dasar:

- Rate limit aktif (`RATE_LIMIT_ENABLED=true`):
  - login/refresh 10/menit/IP;
  - endpoint publik 120/menit/IP;
  - pesanan customer 40/menit/IP;
  - webhook 120/menit/IP;
  - endpoint staf 300/menit/IP.
- Respons `429` memakai ProblemDetails dan header `Retry-After`.
- API mengirim header keamanan; halaman SPA memakai CSP.
- Body request dibatasi 6 MB; upload gambar tetap 2 MB.
- API container memiliki healthcheck dan kunci Data Protection persisten.
- Migrasi/seed bisa dijalankan eksplisit:

```bash
docker compose run --rm --no-deps api --migrate
docker compose run --rm --no-deps api --migrate --seed
```

Matikan rate limit hanya saat pengujian lokal:

```bash
$env:RATE_LIMIT_ENABLED="false"
docker compose up -d api
```

Panduan deploy VPS, Caddy/TLS, backup, restore, dan monitoring ada di
[PRODUCTION.md](PRODUCTION.md).

---

## 4. Opsi B — Full Docker

Pastikan `.env` sudah berisi `POSTGRES_PASSWORD`, `SEED_OWNER_EMAIL`, dan `SEED_OWNER_PASSWORD`, lalu dari folder `hanora-app`:

```bash
docker compose up -d --build
```

Container API akan menjalankan migration dan seed otomatis (mode Development). Login pertama owner diarahkan ke halaman ganti password.

| Layanan | Alamat |
|---|---|
| Web (nginx) | <http://localhost:8080> |
| API langsung | <http://localhost:5080> |
| PostgreSQL | `127.0.0.1:5432` |

Hentikan:

```bash
docker compose down
```

Hentikan dan hapus volume database (data hilang):

```bash
docker compose down -v
```

> Compose ini untuk development/demo lokal. Untuk production gunakan
> `docker-compose.prod.yml` + Caddy; panduan lengkap di
> [PRODUCTION.md](PRODUCTION.md).

---

## 5. Verifikasi Build dan Test

### Backend

```bash
cd backend
dotnet build Hanora.slnx
dotnet test Hanora.slnx
```

Hasil yang diharapkan:

- Build tanpa error.
- 22 unit test lulus.
- 40 integration test lulus (Testcontainers PostgreSQL): health, autentikasi/RBAC, katalog/upload/pengaturan, meja/QR, sesi meja, checkout, perhitungan harga, idempotency, tracking, pembatalan, konfirmasi tunai, transisi status, riwayat transaksi, simulasi QRIS, signature webhook, idempotensi webhook, nominal tidak cocok, dashboard owner, pengeluaran, pemasukan manual, export PDF/Excel, security headers, dan rate limiting.

### Frontend

```bash
cd frontend
npm run build
npm run lint
```

Hasil yang diharapkan: folder `dist/` terbentuk tanpa error TypeScript; lint bersih.

### Health endpoint

```bash
curl http://localhost:5080/api/v1/health
curl http://localhost:5080/api/v1/health/ready
```

Tanpa PostgreSQL, readiness mengembalikan `503` dengan `database: "down"`. Itu perilaku yang benar, bukan bug.

### Autentikasi

```bash
# Login
curl -i -X POST http://localhost:5080/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"owner@hanoracafe.id","password":"..."}'

# Tanpa token (harus 401)
curl -i http://localhost:5080/api/v1/owner/users

# Dengan token owner (harus 200)
curl -i http://localhost:5080/api/v1/owner/users \
  -H "Authorization: Bearer <accessToken>"
```

---

## 6. Environment Variable

| Variabel | Digunakan | Keterangan |
|---|---|---|
| `POSTGRES_DB` | Docker | Nama database |
| `POSTGRES_USER` | Docker | User database |
| `POSTGRES_PASSWORD` | Docker | Password database |
| `APP_ORIGIN` | Docker | Origin frontend untuk CORS |
| `ConnectionStrings__Default` | Backend | Connection string Npgsql |
| `DATABASE_URL` | Backend | Alternatif format `postgres://...` |
| `Cors__AllowedOrigins__0` | Backend | Origin yang diizinkan |
| `Storage__UploadPath` | Backend | Path upload gambar |
| `Jwt__Issuer` | Backend | Issuer JWT |
| `Jwt__Audience` | Backend | Audience access token staf |
| `Jwt__TableAudience` / `JWT_TABLE_AUDIENCE` | Backend | Audience token sesi meja |
| `Jwt:SigningKey` / `JWT_SIGNING_KEY` | Backend | Kunci tanda tangan JWT (minimal 32 byte) |
| `Jwt__AccessMinutes` / `JWT_ACCESS_MINUTES` | Backend | Umur access token (default 15) |
| `Jwt__RefreshDays` / `REFRESH_TOKEN_DAYS` | Backend | Umur refresh token (default 7) |
| `Jwt__TableSessionMinutes` / `TABLE_SESSION_MINUTES` | Backend | Umur sesi meja (default 120) |
| `App__BaseUrl` / `APP_BASE_URL` | Backend | Base URL publik untuk QR meja |
| `Database__AutoMigrate` | Backend | Terapkan migration saat startup (Development) |
| `Database__Seed` | Backend | Seed role/pengaturan/owner |
| `SEED_OWNER_EMAIL` | Backend | Email owner awal |
| `SEED_OWNER_PASSWORD` | Backend | Password owner awal |
| `MIDTRANS_MODE` | Backend | `simulation` (default), `sandbox`, atau `production` |
| `MIDTRANS_SERVER_KEY` | Backend | Server key Midtrans; rahasia, hanya backend |
| `MIDTRANS_CLIENT_KEY` | Backend | Client key Midtrans |
| `MIDTRANS_MERCHANT_ID` | Backend | Merchant ID Midtrans |
| `MIDTRANS_SNAP_URL` | Backend | Override endpoint Snap (opsional) |
| `MIDTRANS_QRIS_ENABLED` | Backend | Aktif/nonaktifkan QRIS (default `true`) |
| `RATE_LIMIT_ENABLED` | Backend | Aktif/nonaktifkan rate limiting (default `true`) |
| `RateLimiting__AuthPermitLimit` | Backend | Batas login/refresh per menit per IP |
| `RateLimiting__PublicPermitLimit` | Backend | Batas endpoint publik per menit per IP |
| `RateLimiting__OrderPermitLimit` | Backend | Batas endpoint pesanan per menit per IP |
| `RateLimiting__WebhookPermitLimit` | Backend | Batas webhook per menit per IP |
| `RateLimiting__StaffPermitLimit` | Backend | Batas global endpoint staf per menit per IP |
| `DataProtection__KeysPath` | Backend | Path kunci Data Protection (volume persisten) |

Jangan pernah menaruh nilai rahasia di file yang di-commit. Di production, `JWT_SIGNING_KEY` wajib diisi nilai acak yang kuat dan bukan nilai `dev-only-*`.

---

## 7. Perintah Berguna

```bash
# Status container
docker compose ps

# Log API dalam Docker
docker compose logs -f api

# Restart API
docker compose restart api

# Masuk psql
docker compose exec postgres psql -U hanora -d hanora

# Hentikan semua container
docker compose down
```

---

## 8. Troubleshooting

| Masalah | Solusi |
|---|---|
| `docker` gagal connect ke pipe | Jalankan Docker Desktop dan tunggu daemon siap |
| Port 5432 dipakai | Hentikan PostgreSQL lokal lain atau ubah port compose |
| Port 5080/5173 dipakai | Hentikan proses lain atau ubah port |
| Readiness `503` | PostgreSQL belum berjalan atau connection string salah |
| Frontend menampilkan API offline | Pastikan backend berjalan dan proxy Vite aktif |
| `dotnet ef` tidak dikenal | Jalankan `dotnet tool install --global dotnet-ef --version 10.0.4` |
| Build frontend gagal karena tipe | Jalankan `npm run build` dan perbaiki error TypeScript, jangan gunakan `any` tanpa alasan |
| Owner tidak dibuat | Pastikan `SEED_OWNER_EMAIL` dan `SEED_OWNER_PASSWORD` (minimal 12 karakter) diisi sebelum API pertama kali berjalan |
| Login owner gagal setelah ganti password | Gunakan password baru; password lama memang dicabut |
| Dashboard langsung kembali ke login setelah refresh | Pastikan cookie `hanora_session` dan `hanora_refresh` tidak diblokir browser; API harus berjalan di origin yang sama atau proxy Vite aktif |
| 403 saat membuka dashboard | Akun wajib ganti password, atau role tidak berhak (kasir membuka halaman owner) |
| QR tidak bisa dibuka dari HP | Isi `APP_BASE_URL` dengan IP LAN laptop/domain yang dapat diakses HP, restart API, lalu unduh ulang QR |
| QR lama tidak berlaku | QR sudah diregenerasi; cetak ulang QR terbaru dari `/owner/meja` |
| Pesanan tidak muncul di kasir | Pastikan checkout berhasil dan buka `/kasir`; antrean diperbarui setiap 5 detik |
| Sesi meja berakhir | Scan ulang QR meja; sesi meja berlaku 120 menit |
| PostgreSQL `too many clients` | Pool API dibatasi 20 koneksi; restart container API/PostgreSQL jika perlu |
| QRIS `502 provider_error` | Server key Midtrans salah atau Snap menolak | Periksa log API dan kredensial |
| Webhook `401 invalid_signature` | Server key backend berbeda dengan dashboard Midtrans | Samakan nilai `MIDTRANS_SERVER_KEY` |
| Webhook `amount_mismatch` | Nominal notifikasi tidak sama dengan pesanan | Periksa transaksi; payment ditandai review |
| Pembayaran QRIS lunas tetapi order belum berubah | Webhook belum diterima | Pastikan URL webhook publik/HTTPS dan cek log |
| `409 simulation_disabled` | Mode bukan simulation | Gunakan sandbox/production |
| `429 Too Many Requests` | Rate limit terlampaui | Tunggu satu window (±60 detik) atau sesuaikan `RateLimiting__*` |
| Production gagal start | Base URL HTTP, password DB dev, CORS localhost, mode simulation | Perbaiki `.env.production` dan restart |
| Export laporan gagal | Rentang lebih dari 1 tahun atau format salah | Gunakan `pdf`/`xlsx` dan rentang maksimal 366 hari |
| PDF tidak terbentuk di Docker | Fontconfig/font dasar tidak tersedia | Image API terbaru sudah memasang `libfontconfig1`, `libfreetype6`, dan `fonts-dejavu-core`; rebuild image |
| Pemasukan pesanan tidak bisa dihapus | Data berasal dari pembayaran lunas | Batalkan/refund pesanan; jangan hapus income manual |

---

## 9. Status Fitur

Tahap 10 menyediakan:

- ✅ Backend ASP.NET Core dengan health check.
- ✅ Frontend React + Vite dengan design token HANORA.
- ✅ PostgreSQL, EF Core Migration 17 tabel, enum native.
- ✅ Autentikasi JWT, refresh token, RBAC, wajib ganti password.
- ✅ Website publik: beranda, menu, galeri, lokasi, kontak.
- ✅ CRUD kategori/produk, upload gambar, pengaturan bisnis.
- ✅ Meja unik dengan satu QR aktif; QR PNG dapat diunduh.
- ✅ Sesi meja via JWT audience `table`.
- ✅ Keranjang dan checkout customer.
- ✅ Perhitungan server: subtotal, PB1 10%, total.
- ✅ Nomor pesanan harian, idempotency, tracking publik.
- ✅ Pembatalan pesanan oleh customer pada status awal.
- ✅ Dashboard kasir: detail pesanan, filter, timeline.
- ✅ Konfirmasi tunai + pencatatan income.
- ✅ Transisi status Diproses → Siap Disajikan → Selesai.
- ✅ Pembatalan oleh kasir.
- ✅ Riwayat transaksi.
- ✅ Sinkronisasi status ke tracking customer.
- ✅ QRIS Midtrans: mode simulation/sandbox/production.
- ✅ Snap transaction + redirect pembayaran.
- ✅ Webhook signature SHA-512 + idempotensi notifikasi.
- ✅ Deteksi nominal tidak cocok + penandaan review.
- ✅ Simulasi pembayaran lokal yang ditandai jelas.
- ✅ Bayar ulang QRIS dari halaman tracking.
- ✅ Dashboard owner: ringkasan pendapatan, pengeluaran, laba, pesanan aktif.
- ✅ Grafik tren penjualan harian/bulanan + produk terlaris.
- ✅ Komposisi pembayaran QRIS/tunai.
- ✅ CRUD pengeluaran dengan audit log.
- ✅ Pemasukan manual + proteksi pemasukan pesanan.
- ✅ Laporan PDF (QuestPDF) dan Excel (ClosedXML).
- ✅ Manajemen pengguna owner/kasir.
- ✅ Rate limiting per area + respons `429`.
- ✅ Security headers API dan CSP halaman.
- ✅ Validasi konfigurasi production saat startup.
- ✅ Data Protection keys persisten.
- ✅ Healthcheck Docker + batas body request.
- ✅ Stack production Caddy + TLS otomatis.
- ✅ Migrasi eksplisit `--migrate` / `--migrate --seed`.
- ✅ Script backup, restore, dan healthcheck.
- ✅ Panduan deploy VPS di `docs/PRODUCTION.md`.

Semua tahap 1–10 selesai. Langkah berikutnya bersifat operasional: deploy,
backup rutin, monitoring, dan rotasi kredensial.
