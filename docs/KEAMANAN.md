# KEAMANAN SISTEM — HANORA CAFE

**Versi:** 2.0 (Tahap 10 — hardening production)
**Prinsip:** pertahanan berlapis, least privilege, jangan percaya input client, jangan simpan rahasia di frontend.

---

## 1. Model Ancaman Ringkas

| Ancaman | Contoh | Mitigasi |
|---|---|---|
| Spoofing | Menebak token staf | JWT bertanda tangan, umur pendek, refresh rotation |
| Tampering | Mengubah harga/total di request | Server menghitung ulang dari `productId` + `quantity` |
| Repudiation | Kasir menyangkal mengubah status | `transaction_histories` + `audit_logs` |
| Information disclosure | Server Key bocor ke browser | Key hanya di backend/env; tidak pernah di API response/log |
| Denial of service | Spam order/login | Rate limit, body size limit, validasi |
| Elevation of privilege | Customer mengakses endpoint owner | Policy role + audience token terpisah |
| Payment fraud | Webhook palsu / replay | Signature SHA-512, nominal, idempotency, row lock |
| File upload attack | Webshell/gambar berbahaya | Validasi magic bytes, decode ulang, nama acak, non-executable |
| IDOR | Menebak ID pesanan | UUID + token tracking acak; otorisasi role |
| Credential stuffing | Login berulang | Rate limit login, password policy, audit |

---

## 2. Autentikasi

### 2.1 Staf (Owner/Kasir)

- Login dengan email + password.
- Access token JWT HS256, umur **15 menit**, disimpan di memori frontend.
- Refresh token acak 32 byte, disimpan sebagai **SHA-256 hash** di `refresh_tokens`, dikirim via cookie:
  - `HttpOnly`
  - `Secure` (production)
  - `SameSite=Lax`
  - `Path=/api/v1/auth`
- Cookie penanda `hanora_session=1` (non-httpOnly, `Path=/`) hanya menandakan bahwa sesi mungkin ada; tidak berisi token dan tidak dipakai untuk otorisasi.
- Rotasi setiap refresh; token lama dicabut.
- Logout mencabut refresh token dan menghapus cookie.
- Akun nonaktif (`is_active=false`) ditolak saat login dan saat refresh.

### 2.2 Customer (guest)

- Tidak ada akun/password.
- Sesi meja adalah JWT audience `table`, umur **2 jam**, berisi `table_id`, `qr_code_id`, `role=Customer`.
- Endpoint staf menolak token audience `table`; endpoint customer menolak token staf.
- Tracking pesanan memakai token acak terpisah (disimpan sebagai hash).

### 2.3 Password

- Minimal 12 karakter; tidak ada aturan komposisi yang memaksa simbol (mengikuti NIST).
- Hash memakai ASP.NET Core `PasswordHasher<T>` (PBKDF2-HMAC-SHA512 + salt acak, iterasi tinggi).
- Rehash otomatis bila parameter berubah.
- Akun owner pertama wajib mengganti password setelah login pertama.
- Selama `must_change_password = true`, backend hanya mengizinkan endpoint auth (`/auth/me`, `/auth/change-password`, `/auth/logout`); endpoint staf lain ditolak `403 password-change-required`.
- Tidak ada password default di kode atau dokumentasi publik.

---

## 3. Otorisasi (RBAC)

| Area | Publik | Customer (sesi meja) | Kasir | Owner |
|---|:---:|:---:|:---:|:---:|
| Website publik (site, kategori, produk) | ✅ | ✅ | ✅ | ✅ |
| Validasi QR meja | ✅ | ✅ | ✅ | ✅ |
| Buat pesanan | – | ✅ | – | – |
| Tracking pesanan (token) | ✅ | ✅ | – | – |
| Batalkan pesanan sendiri | – | ✅ | – | – |
| Ulangi pembayaran QRIS | – | ✅ | – | – |
| Webhook Midtrans | ✅ (signature) | – | – | – |
| Antrean & detail kasir | – | – | ✅ | ✅ |
| Ubah status / konfirmasi tunai | – | – | ✅ | ✅ |
| Riwayat transaksi kasir | – | – | ✅ | ✅ |
| Dashboard & laporan owner | – | – | – | ✅ |
| CRUD produk/kategori/meja | – | – | – | ✅ |
| Pengeluaran/pemasukan manual | – | – | – | ✅ |
| Pengaturan bisnis | – | – | – | ✅ |
| Manajemen user | – | – | – | ✅ |
| Audit log | – | – | – | ✅ |

Aturan tambahan:
- Owner tidak dapat menonaktifkan akunnya sendiri.
- Kasir tidak dapat mengelola user, produk, meja, atau pengaturan.
- Semua endpoint staf wajib `[Authorize]` + policy eksplisit; tidak ada endpoint "lupa diberi proteksi".
- UI menyembunyikan menu yang tidak berhak, tetapi backend tetap menolak (`403`).

---

## 4. Validasi Input

- Semua request memakai DTO; tidak ada binding langsung ke entity (mencegah mass assignment).
- DataAnnotations untuk batas dasar + validasi service untuk aturan bisnis.
- Batas panjang mengikuti skema database.
- Enum diverifikasi terhadap daftar nilai yang diizinkan.
- UUID divalidasi formatnya.
- Nominal dari client **tidak pernah dipercaya**; server menghitung ulang.
- `pageSize` maksimum 100.
- Body request dibatasi **6 MB** di Kestrel; upload gambar tetap divalidasi 2 MB.
- Upload dibatasi 2 MB.
- Error validasi tidak membocorkan stack trace.
- Endpoint dengan atribut `[EnableRateLimiting]` memakai kebijakan terpisah.

---

## 5. Injection & Data Exposure

- EF Core memakai parameterized query; tidak ada string concatenation SQL.
- Jika raw SQL diperlukan, gunakan parameter, bukan interpolasi.
- React melakukan output encoding; `dangerouslySetInnerHTML` dilarang.
- Respons API tidak menyertakan `password_hash`, token mentah, `ServerKey`, atau data internal.
- Pesan error produksi tidak memuat detail internal.
- Swagger/Scalar hanya aktif di Development.
- Pesanan customer tidak menampilkan ID database, catatan internal, atau token pembayaran.

---

## 6. Pembayaran & Webhook

| Kontrol | Implementasi |
|---|---|
| Server Key | Hanya di environment backend; tidak pernah dikirim ke frontend |
| Signature | `SHA512(order_id + status_code + gross_amount + ServerKey)` |
| Perbandingan | Constant-time |
| Nominal | Bandingkan `decimal` dengan `payments.gross_amount` |
| Idempotency | `notification_hash` unik + `SELECT ... FOR UPDATE` |
| Status final | `paid` tidak dapat diturunkan oleh notifikasi terlambat |
| Metode | Hanya QRIS (`payment_type=qris` atau `other_qris`) diproses sebagai pembayaran |
| Mode | `simulation` / `sandbox` / `production` dari env |
| Simulasi | Endpoint simulasi hanya aktif saat `Midtrans:Mode=simulation`; mode ini ditolak saat production |
| Review | Nominal tidak cocok / pembayaran telat → `needs_review=true` + audit |
| Logging | Payload disimpan untuk audit, tetapi tidak memuat credential |
| Rekonsiliasi | Owner dapat memeriksa transaksi `pending`/`needs_review` |

Aturan penting:
- Jangan mengaktifkan endpoint simulasi di production.
- Jangan menaruh `MIDTRANS_SERVER_KEY` di variabel `VITE_*`.
- Jangan mengandalkan `transaction_status` dari frontend.
- Jangan menganggap integrasi berhasil sebelum diuji dengan Sandbox key asli.

---

## 7. Upload File

- Ukuran maksimum 2 MB.
- Format: JPEG, PNG, WebP.
- Validasi **magic bytes**, bukan hanya `Content-Type`/ekstensi.
- Nama file diacak (UUID); ekstensi ditentukan server berdasarkan isi file.
- Simpan di direktori khusus upload; tidak dapat dieksekusi.
- Cegah path traversal saat menghapus gambar lama.
- Sajikan dengan `Content-Type` yang benar dan `X-Content-Type-Options: nosniff`.
- Satu produk satu gambar; gambar lama dihapus setelah gambar baru tersimpan.
- **Risiko residual:** gambar tidak di-re-encode dan EXIF tidak dihapus. Risiko
  dibatasi karena unggahan hanya oleh owner terautentikasi dan file disajikan
  sebagai gambar non-eksekusi. Re-encode dapat ditambahkan bila diperlukan.

---

## 8. Transport & Header Keamanan

Sudah diterapkan:

- HTTPS wajib di production (Caddy, TLS otomatis + redirect HTTP → HTTPS).
- HSTS `max-age=31536000; includeSubDomains` saat request HTTPS.
- `X-Content-Type-Options: nosniff`
- `X-Frame-Options: DENY`
- `Referrer-Policy: strict-origin-when-cross-origin`
- `Permissions-Policy: camera=(), microphone=(), geolocation=()`
- `Cross-Origin-Opener-Policy: same-origin`
- `Cross-Origin-Resource-Policy: same-origin`
- CSP halaman SPA:
  - `default-src 'self'`
  - `img-src 'self' data:`
  - `style-src 'self' 'unsafe-inline' https://fonts.googleapis.com`
  - `font-src 'self' data: https://fonts.gstatic.com`
  - `script-src 'self'`
  - `connect-src 'self' https://fonts.googleapis.com https://fonts.gstatic.com`
  - `frame-src 'self' https://app.sandbox.midtrans.com https://app.midtrans.com`
  - `object-src 'none'`, `base-uri 'self'`, `form-action 'self'`, `frame-ancestors 'none'`
- Header `Server` dihapus (`AddServerHeader=false` dan Caddy `-Server`).
- Forwarded headers diproses untuk IP asli di belakang proxy.

---

## 9. CORS & CSRF

- CORS allowlist hanya origin frontend (`https://<domain>` dan `http://localhost:5173` untuk development).
- Tidak memakai `AllowAnyOrigin`.
- Refresh token memakai cookie; karena itu:
  - `SameSite=Lax`
  - endpoint refresh/logout hanya `POST`
  - validasi `Origin`/`Referer` terhadap allowlist
- Aksi bisnis memakai `Authorization: Bearer`, sehingga tidak rentan CSRF.
- Access token tidak disimpan di `localStorage` (mengurangi risiko XSS).

---

## 10. Rate Limiting & DoS

Sudah diterapkan dengan fixed window per IP:

| Area | Batas default |
|---|---|
| Login/refresh | 10/menit/IP |
| Publik (site, katalog, QR meja) | 120/menit/IP |
| Pesanan customer (buat, tracking, QRIS) | 40/menit/IP |
| Webhook Midtrans | 120/menit/IP |
| Endpoint staf (global) | 300/menit/IP |

Respons `429` memakai `application/problem+json` dan header `Retry-After`.
Nilai dapat diubah lewat `RateLimiting__*` / `RATE_LIMIT_ENABLED`.

Tambahan:
- Batas body request 6 MB dan timeout header/request.
- Caddy membatasi body 6 MB.
- Request lambat/berlebih ditolak sebelum menyentuh database.
- Login gagal tidak membocorkan apakah email terdaftar.

---

## 11. Manajemen Rahasia

- Semua rahasia di environment variable; `.env` tidak masuk Git.
- `.env.example` hanya berisi nama variabel dan placeholder.
- Permission file `.env` ketat (`600`).
- Kunci berbeda untuk development, sandbox, dan production.
- Rotasi JWT key akan membatalkan semua sesi (dokumentasikan saat dilakukan).
- Tidak ada kredensial nyata di dokumen, test, atau seed.
- `SEED_OWNER_PASSWORD` dipakai sekali; setelah login wajib ganti.
- Kunci Data Protection dipersistenkan di volume `hanora_prod_keys` agar sesi
  dan cookie tidak rusak setelah container restart.
- Validasi startup production menolak base URL non-HTTPS, password database
  development, CORS localhost, mode Midtrans simulation, dan JWT key lemah.

Daftar variabel minimal:

```
APP_ENV
APP_BASE_URL
CORS_ORIGINS
DATABASE_URL
JWT_SIGNING_KEY
JWT_ISSUER
JWT_AUDIENCE
JWT_ACCESS_MINUTES
REFRESH_TOKEN_DAYS
SEED_OWNER_EMAIL
SEED_OWNER_PASSWORD
MIDTRANS_MODE
MIDTRANS_SERVER_KEY
MIDTRANS_CLIENT_KEY
MIDTRANS_MERCHANT_ID
MIDTRANS_SNAP_URL
MIDTRANS_QRIS_ENABLED
UPLOAD_PATH
DEV_SIMULATION_SECRET
```

---

## 12. Logging & Monitoring

- Serilog terstruktur, menyertakan `traceId`, `userId`, `orderNumber` (bila relevan).
- Tidak mencatat password, token, cookie, `ServerKey`, atau payload kartu.
- Header `Authorization` di-redact.
- Log aksi sensitif: login gagal, perubahan status, konfirmasi tunai, CRUD produk/user, webhook invalid, `needs_review`.
- Log disimpan terotasi; akses dibatasi.
- Health check untuk monitoring uptime (`/api/v1/health/ready`).
- Docker healthcheck API dan log rotation `10 MB × 5` per service.
- Alert minimal: error rate 5xx, webhook invalid berulang, disk penuh.
- Panduan monitoring: [PRODUCTION.md](PRODUCTION.md).

---

## 13. Database

- Container PostgreSQL tidak diekspos ke internet.
- User aplikasi bukan superuser; hanya hak yang diperlukan pada schema aplikasi.
- Password database kuat dan hanya di env.
- Backup harian; restore diuji (`scripts/backup.sh` & `scripts/restore.sh`).
- Migration direview; backup sebelum migration production.
- Data sensitif (hash password, token hash) tidak pernah dikembalikan API.
- Timezone database UTC; konversi di aplikasi.
- Migration production dijalankan eksplisit dengan `--migrate`, bukan
  auto-migrate saat startup.

---

## 14. Infrastruktur VPS

- Ubuntu 24.04 LTS, update keamanan otomatis.
- SSH key-only; root login dinonaktifkan; fail2ban.
- Firewall hanya 22, 80, 443.
- Container berjalan sebagai non-root bila memungkinkan.
- Volume uploads dan PostgreSQL persisten.
- Caddy menangani TLS dan header keamanan.
- Tidak ada port database yang dipublikasikan.
- Docker image di-pin ke versi minor; rebuild rutin untuk patch.
- `.env` tidak dimasukkan ke image.

---

## 15. Backup & Pemulihan

- `pg_dump` harian + backup volume uploads.
- Retensi 14 hari; simpan salinan di lokasi terpisah.
- Enkripsi backup bila disimpan di luar VPS.
- Uji restore minimal sebulan sekali.
- Prosedur lengkap ada di [PRODUCTION.md](PRODUCTION.md).

---

## 16. Checklist Pengujian Keamanan

- [x] Setiap endpoint tanpa token mengembalikan `401` (kecuali publik/webhook).
- [x] Kasir mendapat `403` pada seluruh endpoint owner.
- [x] Token sesi meja mendapat `403` pada endpoint staf.
- [x] Harga/total tidak dapat dimanipulasi dari body request.
- [x] `Idempotency-Key` mencegah pesanan ganda.
- [x] Webhook signature salah ditolak dan dicatat.
- [x] Webhook duplikat tidak menggandakan income.
- [x] Nominal tidak cocok memicu `needs_review`, bukan status `paid`.
- [x] Upload file non-gambar dengan ekstensi palsu ditolak.
- [x] Path traversal pada nama file ditolak.
- [x] Login brute force terkena rate limit (`429`).
- [x] Refresh token tidak dapat dibaca JavaScript (`HttpOnly`).
- [x] Logout mencabut refresh token.
- [x] Akun nonaktif tidak dapat login/refresh.
- [x] Akun dengan `must_change_password` ditolak pada endpoint staf selain auth.
- [x] Owner tidak dapat menonaktifkan dirinya sendiri.
- [x] Error produksi tidak memuat stack trace.
- [x] Swagger/Scalar tidak dapat diakses di production.
- [x] Endpoint simulasi Midtrans ditolak di production.
- [x] Header keamanan dan CSP aktif (diuji otomatis).
- [x] `dotnet list package --vulnerable` dan `npm audit` diperiksa.
- [ ] Backup direstore di VPS production (uji operasional, bukan unit test).

---

## 17. Risiko Residual & Batasan

| Risiko | Status |
|---|---|
| QR difoto orang lain | Diterima; owner dapat meregenerasi QR; staf memantau |
| Tidak ada 2FA untuk owner | Dapat ditambahkan setelah go-live |
| Tidak ada WAF khusus | Caddy + rate limit + validasi menjadi lapisan awal |
| Refund otomatis Midtrans | Tidak diimplementasikan; refund manual di dashboard Midtrans |
| Single VPS (tanpa HA) | Diterima untuk satu outlet; backup & restore menjadi mitigasi |
| Notifikasi webhook hilang total | Worker expiry + rekonsiliasi manual `pending`/`needs_review` |
| XSS pada konten yang diinput owner | Output di-encode React; deskripsi tidak dirender sebagai HTML |
| Kepatuhan PCI | Data kartu tidak menyentuh server (QRIS via Snap); tetap batasi scope |
