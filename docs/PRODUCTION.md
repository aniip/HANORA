# PANDUAN PRODUCTION — HANORA CAFE

**Tahap:** 10 (hardening & deployment)
**Target:** satu VPS Ubuntu 24.04 LTS dengan Docker Compose, Caddy, dan PostgreSQL.

---

## 1. Arsitektur Production

```
Internet
   │  443/80 (TLS otomatis)
   ▼
Caddy  ──►  web (nginx + React)  ──/api──►  api (ASP.NET Core)
                                              │
                                              ▼
                                         postgres 17
```

- Hanya Caddy yang membuka port `80` dan `443`.
- API, web, dan PostgreSQL berada di network internal Docker.
- PostgreSQL **tidak** dipublikasikan ke host.
- Semua upload, kunci Data Protection, dan data PostgreSQL memakai volume persisten.

File yang dipakai:

| File | Fungsi |
|---|---|
| `docker-compose.prod.yml` | Stack production |
| `deploy/Caddyfile` | TLS, reverse proxy, HSTS |
| `.env.production` | Rahasia production (tidak di-commit) |
| `scripts/backup.sh` | Backup database + uploads |
| `scripts/restore.sh` | Restore database + uploads |
| `scripts/healthcheck.sh` | Cek health endpoint via domain |

---

## 2. Prasyarat

- VPS Ubuntu 24.04 LTS, minimal 2 vCPU / 2 GB RAM / 20 GB disk.
- Domain yang diarahkan ke IP VPS:
  - `A` record `cafe.example.com` → IP VPS.
  - Port `80` dan `443` terbuka dari internet.
- Akses SSH key.
- Docker Engine + plugin Compose.

Pasang Docker (contoh resmi):

```bash
curl -fsSL https://get.docker.com | sh
sudo usermod -aG docker "$USER"
```

Logout/login ulang agar grup `docker` aktif.

---

## 3. Hardening VPS

```bash
sudo apt update && sudo apt upgrade -y
sudo apt install -y ufw fail2ban unattended-upgrades
```

SSH:

```bash
sudo sed -i 's/^#\?PermitRootLogin.*/PermitRootLogin no/' /etc/ssh/sshd_config
sudo sed -i 's/^#\?PasswordAuthentication.*/PasswordAuthentication no/' /etc/ssh/sshd_config
sudo systemctl restart ssh
```

Firewall:

```bash
sudo ufw default deny incoming
sudo ufw default allow outgoing
sudo ufw allow 22/tcp
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp
sudo ufw enable
```

Aktifkan update keamanan otomatis:

```bash
sudo dpkg-reconfigure --priority=low unattended-upgrades
```

---

## 4. Menyiapkan Aplikasi

```bash
sudo mkdir -p /opt/hanora-app
sudo chown "$USER":"$USER" /opt/hanora-app
git clone <URL-REPO> /opt/hanora-app
cd /opt/hanora-app
chmod +x scripts/*.sh
```

Jika aplikasi berada di subfolder `hanora-app`, masuk ke folder tersebut.

Salin dan isi environment:

```bash
cp .env.production.example .env.production
chmod 600 .env.production
nano .env.production
```

Wajib diisi:

- `DOMAIN` — domain tanpa `https://`.
- `ACME_EMAIL` — email untuk sertifikat Let's Encrypt.
- `POSTGRES_PASSWORD` — password acak panjang.
- `JWT_SIGNING_KEY` — hasil `openssl rand -base64 48`.
- `SEED_OWNER_EMAIL` / `SEED_OWNER_PASSWORD` — untuk seed awal.
- Kredensial Midtrans production.

> Jangan memakai `MIDTRANS_MODE=simulation` di production. Backend akan
> menolak start.

> Gunakan password database tanpa karakter `;` karena nilainya dirangkai ke
> connection string Npgsql. Contoh aman: `openssl rand -hex 24`.

---

## 5. Build, Migrasi, dan Start

```bash
cd /opt/hanora-app

# 1. Build image
docker compose --env-file .env.production -f docker-compose.prod.yml build

# 2. Nyalakan database
docker compose --env-file .env.production -f docker-compose.prod.yml up -d postgres

# 3. Terapkan migration + seed awal (owner, role, pengaturan)
docker compose --env-file .env.production -f docker-compose.prod.yml \
  run --rm api --migrate --seed

# 4. Nyalakan seluruh stack
docker compose --env-file .env.production -f docker-compose.prod.yml up -d
```

Cek status:

```bash
docker compose --env-file .env.production -f docker-compose.prod.yml ps
curl -fsS https://<domain>/api/v1/health/ready
```

Login pertama:

1. Buka `https://<domain>/masuk`.
2. Masuk dengan akun owner hasil seed.
3. Ganti password (wajib).
4. Setelah password diganti, **kosongkan** `SEED_OWNER_PASSWORD` di
   `.env.production` untuk mengurangi paparan rahasia.

> Setelah provisioning awal, `Database__AutoMigrate` dan `Database__Seed`
> sengaja `"false"`. Gunakan perintah `--migrate` eksplisit saat update.

---

## 6. Verifikasi Produksi

| Pemeriksaan | Perintah / cara |
|---|---|
| TLS | Buka `https://<domain>`, pastikan gembok valid |
| Redirect HTTP | `curl -I http://<domain>` → `308` ke HTTPS |
| HSTS | `curl -I https://<domain>` → `Strict-Transport-Security` |
| CSP | Header `Content-Security-Policy` ada |
| API health | `curl https://<domain>/api/v1/health/ready` → `ready` |
| PostgreSQL tidak terbuka | `ss -ltnp` tidak menampilkan `5432` publik |
| Rate limit | Login salah berulang → `429` |
| Webhook | Daftarkan `https://<domain>/api/v1/webhooks/midtrans` di Midtrans |
| PDF/XLSX | Unduh dari `/owner/laporan` |

Rate limit default:

| Area | Batas |
|---|---|
| Login/refresh | 10/menit/IP |
| Publik | 120/menit/IP |
| Pesanan customer | 40/menit/IP |
| Webhook Midtrans | 120/menit/IP |
| Endpoint staf (global) | 300/menit/IP |

Ubah lewat environment `RateLimiting__*` bila perlu.

### 6.1 QR meja setelah deploy

- `APP_BASE_URL` di production harus `https://<domain>` (bukan IP LAN).
- QR yang dibuat saat development (`http://192.168...`) **tidak berlaku** untuk production.
- Setelah deploy, login owner → `/owner/meja` → unduh/cetak ulang QR.
- Token QR tidak berubah; yang berubah hanya alamat di dalam QR.
- QR production bisa discan dari Wi-Fi mana pun atau data seluler, karena
  mengarah ke domain publik.
- Jangan membuka port `5080` atau `5432` ke internet. Cukup `80`/`443`.

### 6.2 Foto produk

- Aplikasi mendukung dua sumber foto:
  - upload lokal dari `/owner/produk` (disimpan di volume uploads), dan
  - URL absolut `https://...` (misalnya CDN).
- CSP nginx saat ini mengizinkan `https://images.unsplash.com` untuk foto demo.
  Jika memakai host lain, tambahkan host tersebut ke `img-src` di
  `frontend/nginx.conf` atau unggah fotonya secara lokal.
- Untuk production jangka panjang, sebaiknya unggah foto ke server agar tidak
  bergantung pada layanan pihak ketiga.

---

## 7. Midtrans Production

Di dashboard Midtrans production:

1. Aktifkan QRIS.
2. Daftarkan **Payment Notification URL**:
   `https://<domain>/api/v1/webhooks/midtrans`.
3. Pastikan server key dan client key production di `.env.production`.
4. Restart API setelah mengubah environment:

```bash
docker compose --env-file .env.production -f docker-compose.prod.yml up -d api
```

Panduan lengkap: [MIDTRANS_SETUP.md](MIDTRANS_SETUP.md).

---

## 8. Backup

Backup manual:

```bash
cd /opt/hanora-app
bash scripts/backup.sh
```

Hasil di folder `backups/`:

- `hanora-db-<timestamp>.sql.gz`
- `hanora-uploads-<timestamp>.tar.gz`

Retensi default 14 hari. Ubah dengan
`RETENTION_DAYS=30 bash scripts/backup.sh`.

Jadwalkan harian lewat cron:

```bash
crontab -e
```

```cron
0 2 * * * cd /opt/hanora-app && bash scripts/backup.sh >> /var/log/hanora-backup.log 2>&1
```

Simpan salinan terenkripsi di lokasi terpisah (object storage / komputer lain).
Uji restore minimal sebulan sekali.

### Restore

```bash
cd /opt/hanora-app
bash scripts/restore.sh \
  backups/hanora-db-20261108T020000Z.sql.gz \
  backups/hanora-uploads-20261108T020000Z.tar.gz
```

Ketik `RESTORE` saat diminta. Setelah restore:

```bash
curl -fsS https://<domain>/api/v1/health/ready
```

Lakukan login uji dan cek beberapa pesanan/laporan.

---

## 9. Update & Rollback

### Update

```bash
cd /opt/hanora-app
git pull

docker compose --env-file .env.production -f docker-compose.prod.yml build
docker compose --env-file .env.production -f docker-compose.prod.yml \
  run --rm api --migrate
docker compose --env-file .env.production -f docker-compose.prod.yml up -d
```

Selalu backup database sebelum migration yang mengubah skema.

### Rollback

```bash
git checkout <tag-atau-commit-sebelumnya>
docker compose --env-file .env.production -f docker-compose.prod.yml build
docker compose --env-file .env.production -f docker-compose.prod.yml up -d
```

Jika migration tidak kompatibel ke belakang, restore database dari backup
terakhir setelah kode lama berjalan.

---

## 10. Monitoring & Log

```bash
# Status container + healthcheck
docker compose --env-file .env.production -f docker-compose.prod.yml ps

# Log API
docker compose --env-file .env.production -f docker-compose.prod.yml logs -f api

# Log Caddy (TLS/akses)
docker compose --env-file .env.production -f docker-compose.prod.yml logs -f caddy

# Cek disk
df -h
docker system df
```

Rekomendasi:

- Pasang uptime monitor eksternal (Uptime Kuma, Better Stack, atau cron
  `bash scripts/healthcheck.sh`).
- Alert untuk: endpoint readiness gagal, error 5xx meningkat, disk > 80%,
  dan webhook signature invalid berulang.
- Log container dibatasi `10 MB × 5 file` per service.
- Log aplikasi juga tersimpan di volume `hanora_prod_logs` (`/app/logs`).

---

## 11. Keamanan Production

Checklist:

- [ ] `ASPNETCORE_ENVIRONMENT=Production`.
- [ ] `MIDTRANS_MODE=production` dengan kredensial production.
- [ ] `JWT_SIGNING_KEY` acak, minimal 32 byte, bukan nilai `dev-only-*`.
- [ ] `.env.production` permission `600`, tidak masuk Git.
- [ ] Hanya port 22/80/443 terbuka.
- [ ] PostgreSQL tidak dipublikasikan.
- [ ] HSTS, CSP, `X-Content-Type-Options`, `X-Frame-Options` aktif.
- [ ] Rate limit aktif (`RATE_LIMIT_ENABLED=true`).
- [ ] Volume `hanora_prod_keys` menyimpan kunci Data Protection; batasi akses
      dan pertimbangkan enkripsi disk VPS.
- [ ] Backup harian berjalan dan pernah direstore.
- [ ] Akun owner sudah mengganti password seed.
- [ ] Swagger/Scalar tidak aktif (hanya Development).
- [ ] `dotnet list package --vulnerable` dan `npm audit --omit=dev` diperiksa
      setiap update.

Hardening yang sudah diterapkan aplikasi:

- Rate limiting per IP dengan respons `429` + `Retry-After`.
- Security headers di API dan nginx.
- CSP untuk halaman SPA.
- Batas body request 6 MB (upload gambar tetap divalidasi 2 MB).
- Kestrel tidak mengirim header `Server`.
- Forwarded headers hanya dipakai di belakang proxy.
- Validasi konfigurasi production saat startup.
- Kunci Data Protection dipersistenkan di volume.
- Cookie refresh `HttpOnly`, `Secure` di production, `SameSite=Lax`.

---

## 12. Troubleshooting

| Gejala | Penyebab | Solusi |
|---|---|---|
| Caddy gagal dapat sertifikat | DNS belum mengarah ke VPS atau port 80 tertutup | Cek `dig <domain>`, buka port 80/443 |
| `502 Bad Gateway` | API/web belum sehat | `docker compose ... ps`, lihat log api |
| API gagal start di production | Konfigurasi ditolak (HTTP base URL, password dev, mode simulation) | Perbaiki `.env.production`; lihat log |
| `429 Too Many Requests` | Rate limit | Tunggu satu window (±60 detik) atau sesuaikan limit |
| PDF gagal dibuat | Fontconfig hilang | Pastikan image API terbaru (Dockerfile memasang font) |
| Upload gambar gagal | Volume permission | Pastikan volume `hanora_prod_uploads` dimiliki user container |
| Login berhasil tetapi langsung logout | Cookie `Secure` + akses via HTTP | Akses selalu melalui HTTPS |
| Webhook `401 invalid_signature` | Server key berbeda | Samakan `MIDTRANS_SERVER_KEY` dengan dashboard |
| Database penuh | Backup/log menumpuk | Bersihkan image lama, atur retensi backup |

---

## 13. Perintah Penting

```bash
COMPOSE="docker compose --env-file .env.production -f docker-compose.prod.yml"

$COMPOSE ps
$COMPOSE logs -f api
$COMPOSE restart api
$COMPOSE run --rm api --migrate
$COMPOSE down
```

Jangan menjalankan `down -v` di production kecuali Anda benar-benar ingin
menghapus database dan seluruh upload.
