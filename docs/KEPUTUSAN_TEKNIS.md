# KEPUTUSAN TEKNIS (ADR) — HANORA CAFE

**Versi:** 1.0 (Tahap 2)
**Status:** Final untuk direview

Format: **Konteks → Keputusan → Konsekuensi**. Keputusan yang sudah dikunci tidak diubah tanpa persetujuan dan catatan revisi.

---

## ADR-001 — Backend ASP.NET Core

**Konteks:** Master prompt meminta C# ASP.NET Core; environment sudah memiliki .NET SDK 10.0.403.

**Keputusan:** Gunakan **.NET 10 LTS + ASP.NET Core Web API** dengan controller, bukan Minimal API, agar struktur endpoint, filter otorisasi, dan dokumentasi OpenAPI lebih mudah dirawat untuk tim.

**Konsekuensi:**
- Butuh runtime .NET 10 di VPS/container.
- EF Core, Npgsql, dan library pendukung mengikuti versi mayor 10.
- Frontend tetap terpisah dan berkomunikasi lewat REST.

---

## ADR-002 — PostgreSQL + EF Core Migration

**Konteks:** Data transaksi membutuhkan konsistensi, relasi, dan tipe uang yang tepat.

**Keputusan:** **PostgreSQL 17**, akses melalui **EF Core 10 + Npgsql**, semua perubahan skema melalui migration.

**Konsekuensi:**
- Tidak ada `EnsureCreated()` di production.
- Migration direview SQL-nya dan dijalankan eksplisit saat deploy.
- PostgreSQL dijalankan sebagai container; port tidak diekspos ke internet.

---

## ADR-003 — Frontend React + Vite + TypeScript

**Konteks:** Prototype memakai React 18 + Vite + JavaScript dalam satu file.

**Keputusan:** Tulis ulang frontend dengan **React 19 + Vite 8 + TypeScript 7 strict + Tailwind 3.4**, dipecah per fitur.

**Konsekuensi:**
- Desain visual prototype dipertahankan; struktur kode tidak.
- Tailwind 3.4 dipilih agar token dan konfigurasi prototype dapat dipindahkan langsung. Migrasi ke Tailwind 4 dapat dilakukan terpisah.
- Tidak ada `any` tanpa alasan yang didokumentasikan.

---

## ADR-004 — Guest checkout melalui QR, tanpa akun customer

**Konteks:** Customer datang ke cafe, scan QR meja, lalu memesan. Akun customer menambah friksi dan permukaan keamanan.

**Keputusan:** Customer **tidak memiliki akun/login**. Identitas customer hanya nama pada pesanan. Otorisasi customer berasal dari sesi meja.

**Konsekuensi:**
- Tidak ada tabel customer, reset password, atau verifikasi email.
- Role `Customer` hanya menjadi principal sesi meja, bukan akun login.
- Tracking pesanan memakai token acak sekali pakai yang disimpan frontend.

---

## ADR-005 — Satu QR unik per meja + sesi meja 2 jam

**Konteks:** QR harus mengidentifikasi meja dan mencegah pesanan dari meja yang tidak valid.

**Keputusan:**
- Setiap meja memiliki tepat satu QR aktif (partial unique index).
- QR berisi token acak dan mengarah ke `/t/<token>`.
- Setelah scan, backend menerbitkan JWT sesi meja (audience `table`, 2 jam).
- Owner dapat meregenerasi QR; token lama langsung dicabut.

**Konsekuensi:**
- QR yang difoto masih dapat dipakai selama aktif; ini risiko fisik yang diterima untuk cafe.
- Regenerasi QR harus dicetak ulang dan ditempel di meja.
- Endpoint staf menolak token dengan audience `table`.

---

## ADR-006 — Harga dan total sepenuhnya dihitung server

**Konteks:** Prototype mengirim `price` dan `total` dari client.

**Keputusan:** Client hanya mengirim `productId`, `quantity`, dan `notes`. Server mengambil harga dari database, menghitung subtotal, PB1, dan total.

**Konsekuensi:**
- Tidak ada celah manipulasi harga.
- Jika harga produk berubah setelah item masuk keranjang, server memakai harga terbaru saat checkout.
- Jika produk menjadi tidak tersedia, server menolak dengan `409`.

---

## ADR-007 — Snapshot nama dan harga pada item pesanan

**Konteks:** Harga produk dapat berubah; riwayat transaksi tidak boleh berubah.

**Keputusan:** `order_items` menyimpan `product_name_snapshot` dan `unit_price_snapshot`.

**Konsekuensi:**
- Laporan lama tetap akurat.
- Produk dapat diarsipkan tanpa merusak riwayat.
- `product_id` boleh menjadi `NULL` jika produk dihapus permanen (tetapi arsip lebih disarankan).

---

## ADR-008 — Status pesanan dan pembayaran dipisah

**Konteks:** Satu field status tidak cukup untuk membedakan "belum bayar", "sudah bayar tetapi belum diproses", dan "siap disajikan".

**Keputusan:** Dua state machine terpisah: `order_status` dan `payment_status`, dengan aturan transisi di service.

**Konsekuensi:**
- Setiap transisi pesanan dicatat di `transaction_histories`.
- Webhook hanya mengubah status pembayaran dan, bila lunas, memajukan pesanan ke `processing`.
- UI menampilkan dua status bila relevan (misalnya "Lunas · Diproses").

---

## ADR-009 — QRIS via Midtrans, tunai manual, tanpa transfer bank

**Konteks:** Pemilik memilih QRIS melalui Midtrans dan tunai; transfer bank tidak diperlukan.

**Keputusan:**
- QRIS memakai **Midtrans Snap** dengan `enabled_payments` dibatasi ke QRIS.
- Tunai tidak melalui Midtrans; kasir mengonfirmasi pembayaran.
- Tidak ada metode transfer bank di UI maupun database (selain nilai enum yang tidak dipakai).

**Konsekuensi:**
- Diperlukan akun Midtrans dan kredensial Sandbox/Production.
- Tanpa kredensial, mode simulasi dipakai dan ditandai jelas.
- Rekonsiliasi tunai dilakukan dari dashboard kasir/owner.

---

## ADR-010 — PB1 10% dengan snapshot tarif

**Konteks:** Harga menu belum termasuk pajak daerah.

**Keputusan:** `tax_rate = 10.00` disimpan sebagai snapshot pada setiap pesanan; `tax_amount` dibulatkan half-up ke rupiah utuh; total = subtotal + pajak − diskon.

**Konsekuensi:**
- Perubahan tarif hanya berlaku untuk pesanan baru.
- Laporan pajak dihitung dari nilai tersimpan, bukan tarif saat ini.
- Tidak ada service charge.

---

## ADR-011 — Batas waktu pembayaran QRIS dan pesanan

**Konteks:** Pesanan yang tidak dibayar harus tidak menggantung selamanya.

**Keputusan:**
- QRIS kedaluwarsa setelah **15 menit**.
- Pesanan `pending_payment` dibatalkan otomatis setelah **30 menit**.
- Customer dapat mengulang pembayaran QRIS selama pesanan masih `pending_payment`.
- Pembayaran yang telat masuk setelah pesanan dibatalkan ditandai `needs_review`.

**Konsekuensi:**
- Dua background worker: `PaymentExpiryWorker` dan `OrderDeadlineWorker`.
- Pesanan tunai tidak terkena deadline pembayaran otomatis; kasir menangani.
- Owner perlu menindaklanjuti `needs_review` (refund manual).

---

## ADR-012 — Idempotency untuk pesanan dan webhook

**Konteks:** Double tap, retry jaringan, dan webhook Midtrans yang dikirim ulang dapat membuat data ganda.

**Keputusan:**
- `POST /orders` wajib memakai `Idempotency-Key` unik.
- Webhook menyimpan `notification_hash` unik.
- Update status memakai row lock dan validasi transisi.

**Konsekuensi:**
- Request pesanan yang sama mengembalikan pesanan yang sama, bukan membuat baru.
- Webhook duplikat menghasilkan `200` tanpa perubahan.
- Perlu penanganan khusus jika hash payload identik tetapi status berbeda (hash mencakup seluruh payload relevan).

---

## ADR-013 — JWT access 15 menit + refresh cookie 7 hari

**Konteks:** Token akses perlu umur pendek; refresh token tidak boleh dapat dibaca JavaScript.

**Keputusan:**
- Access token JWT HS256, umur 15 menit, disimpan di memori frontend.
- Refresh token acak, disimpan sebagai hash SHA-256 di database, dikirim via cookie `HttpOnly; Secure; SameSite=Lax; Path=/api/v1/auth`.
- Rotasi refresh token setiap kali refresh; token lama dicabut.

**Konsekuensi:**
- Refresh halaman tidak menghilangkan sesi (refresh otomatis).
- Logout mencabut refresh token di database.
- Perlu penanganan race condition refresh di frontend (single-flight).

---

## ADR-014 — Password hashing memakai ASP.NET Core PasswordHasher

**Konteks:** Implementasi kriptografi sendiri berisiko.

**Keputusan:** Gunakan `Microsoft.AspNetCore.Identity.PasswordHasher<T>` (PBKDF2-HMAC-SHA512, salt acak, iterasi tinggi) tanpa mengambil seluruh skema ASP.NET Identity. Rehash otomatis saat login jika parameter berubah.

**Konsekuensi:**
- Tidak ada password plaintext di database atau log.
- Kebijakan password minimal 12 karakter.
- Akun owner pertama wajib mengganti password.

---

## ADR-015 — Penyimpanan aset lokal

**Konteks:** Prototype memakai hotlink Unsplash; pemilik akan menyediakan foto asli.

**Keputusan:** Foto produk dan logo disimpan di volume lokal `/var/hanora/uploads`, disajikan melalui `/media/*`. Upload divalidasi ekstensi, MIME, ukuran, dan magic bytes.

**Konsekuensi:**
- Backup harus mencakup volume uploads.
- Nama file diacak; tidak memakai nama asli.
- Jika kelak pindah ke object storage, antarmuka penyimpanan dibuat agar mudah diganti.

---

## ADR-016 — Export laporan dibuat di server

**Konteks:** Prototype mengekspor PDF/Excel dari data dummy di browser.

**Keputusan:** Endpoint owner menghasilkan PDF (QuestPDF) dan Excel (ClosedXML) dari query database pada periode yang dipilih.

**Konsekuensi:**
- Angka laporan konsisten dengan database.
- QuestPDF memakai Community License; perlu ditinjau jika pendapatan usaha melewati batas lisensi.
- File besar dibatasi rentang tanggal maksimum (misalnya 1 tahun per export).

---

## ADR-017 — Polling 5 detik, belum WebSocket

**Konteks:** Dashboard kasir perlu melihat pesanan baru; volume cafe tidak membutuhkan realtime penuh.

**Keputusan:** Gunakan polling 5 detik pada antrean kasir dan tracking customer. Hentikan polling saat tab tidak aktif atau status final.

**Konsekuensi:**
- Implementasi lebih sederhana dan tahan reconnect.
- Beban server rendah untuk satu outlet.
- Jika kelak butuh realtime, tambahkan SSE/WebSocket tanpa mengubah kontrak data.

---

## ADR-018 — Satu repo, aplikasi di subfolder

**Konteks:** Prototype berada di root repo dengan history Git yang harus dipertahankan.

**Keputusan:** Aplikasi baru berada di `hanora-app/` dalam repo yang sama; file prototype tidak dipindahkan.

**Konsekuensi:**
- Satu riwayat perubahan untuk prototype dan aplikasi.
- CI harus membatasi trigger berdasarkan path (`hanora-app/**`).
- Deploy production tidak menyertakan folder prototype.

---

## ADR-019 — Bahasa Indonesia, IDR, Asia/Jakarta

**Konteks:** Pengguna adalah staf dan customer lokal.

**Keputusan:** Seluruh UI, pesan error, dan laporan berbahasa Indonesia. Mata uang IDR tanpa desimal. Tanggal bisnis memakai `Asia/Jakarta`.

**Konsekuensi:**
- Tidak ada layer i18n pada versi ini.
- Database menyimpan waktu UTC; konversi dilakukan di API/serializer.
- Format tanggal dan angka konsisten di seluruh aplikasi.

---

## ADR-020 — Seed minimal, tanpa data demo produksi

**Konteks:** Instruksi melarang data dummy disajikan sebagai data asli.

**Keputusan:** Seed hanya roles, settings default, dan satu akun owner dari environment. Produk, meja, pesanan, pembayaran, dan pengeluaran tidak di-seed.

**Konsekuensi:**
- Aplikasi baru pertama kali tampil kosong dan menampilkan empty state.
- Owner mengisi menu dan meja melalui UI.
- Tidak ada tombol "isi data demo" di production.

---

## ADR-021 — Docker Compose + Caddy di VPS

**Konteks:** Hosting ditargetkan ke VPS.

**Keputusan:** Gunakan Docker Compose dengan service `caddy`, `api`, `postgres`, dan volume uploads. Caddy menangani TLS otomatis dan reverse proxy.

**Konsekuensi:**
- Deploy cukup dengan `docker compose up -d --build`.
- Hanya port 80/443 (dan SSH) yang terbuka.
- Backup dan restore harus diuji sebelum go-live.

---

## ADR-022 — Otorisasi di application layer, bukan RLS

**Konteks:** PostgreSQL Row Level Security menambah kompleksitas operasional untuk satu aplikasi.

**Keputusan:** Otorisasi role dan kepemilikan dilakukan di service/controller ASP.NET Core. Database hanya dijangkau oleh aplikasi.

**Konsekuensi:**
- Setiap endpoint wajib memiliki `[Authorize]` dan policy role yang eksplisit.
- Test otorisasi menjadi bagian wajib.
- Jika kelak multi-tenant, RLS dapat ditambahkan.

---

## ADR-023 — Soft delete hanya untuk produk

**Konteks:** Menghapus produk tidak boleh merusak riwayat pesanan.

**Keputusan:** Produk "dihapus" dengan mengisi `archived_at`; tidak muncul di UI dan tidak dapat dipesan. Pesanan, pembayaran, dan income tidak pernah dihapus.

**Konsekuensi:**
- Data historis tetap utuh.
- Kategori yang masih dipakai produk tidak dapat dihapus.
- Meja dengan riwayat pesanan tidak dapat dihapus; cukup dinonaktifkan.

---

## ADR-024 — Audit log untuk aksi sensitif

**Konteks:** Perubahan status, pembayaran, produk, dan pengaturan perlu dapat ditelusuri.

**Keputusan:** Catat aksi sensitif di `audit_logs` dan perubahan status pesanan di `transaction_histories`.

**Konsekuensi:**
- Metadata tidak boleh memuat password/token.
- Audit log tidak dapat diubah dari UI.
- Retensi minimal 1 tahun.

## ADR-025 — Hardening production di application layer

**Konteks:** Aplikasi akan diekspos ke internet melalui VPS tunggal. Ancaman utama
adalah credential stuffing, spam request, kebocoran header, dan salah konfigurasi
saat deploy.

**Keputusan:**
- Rate limiting fixed-window per IP untuk auth, publik, pesanan, webhook, dan
  endpoint staf; nilai dapat dikonfigurasi.
- Security headers di middleware API dan nginx (CSP untuk SPA), HSTS dari Caddy.
- Validasi konfigurasi production saat startup (HTTPS base URL, password DB
  development, CORS localhost, mode Midtrans simulation, JWT key lemah).
- Kunci Data Protection dipersistenkan di volume.
- Batas body request 6 MB, upload tetap 2 MB.
- Migrasi production eksplisit melalui argumen `--migrate` / `--seed`.

**Konsekuensi:**
- Beberapa request sah dapat terkena `429` jika dilakukan berlebihan; monitoring
  perlu memperhatikan false positive.
- Deployment harus menyertakan volume kunci dan `.env.production` yang benar.
- Reverse proxy wajib meneruskan `X-Forwarded-For`/`X-Forwarded-Proto` agar IP
  asli dan cookie `Secure` bekerja.
