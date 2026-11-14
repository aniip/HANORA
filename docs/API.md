# KONTRAK API — HANORA CAFE

**Versi:** 1.1 (Tahap 10)
**Base URL:** `https://<domain>/api/v1`
**Format:** JSON (`application/json`), kecuali upload dan export
**Spesifikasi mesin:** [openapi.yaml](openapi.yaml)

---

## 1. Konvensi Umum

| Aspek | Aturan |
|---|---|
| Versioning | Prefix `/api/v1` |
| Autentikasi staf | `Authorization: Bearer <accessToken>` |
| Autentikasi customer | `Authorization: Bearer <tableSessionToken>` (audience `table`) |
| Refresh token | Cookie `hanora_refresh`, `HttpOnly`, `Secure`, `SameSite=Lax`, path `/api/v1/auth` |
| Waktu | ISO 8601 dengan offset, contoh `2026-11-07T14:30:00+07:00` |
| Tanggal query | `YYYY-MM-DD`, ditafsirkan sebagai `Asia/Jakarta` |
| Uang | Angka JSON dalam rupiah (contoh `96800`); tidak mengirim string berformat |
| Pagination | `page` (default 1), `pageSize` (default 20, maks 100) |
| Sorting | `sort=createdAt:desc` bila tersedia |
| Idempotency | Header `Idempotency-Key` wajib pada `POST /orders` |
| Error | RFC 7807 ProblemDetails |
| Korelasi | `traceId` disertakan pada setiap error |
| Bahasa pesan | Indonesia |

### 1.1 Format error

```json
{
  "type": "https://hanora.example.com/errors/validation",
  "title": "Validasi gagal",
  "status": 400,
  "detail": "Periksa kembali data yang dikirim.",
  "instance": "/api/v1/orders",
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
  "errors": {
    "customerName": ["Nama wajib diisi."],
    "items": ["Minimal satu item harus dipesan."]
  }
}
```

### 1.2 Kode error

| `type` (slug) | HTTP | Kapan |
|---|---:|---|
| `validation` | 400 | Input tidak valid |
| `unauthorized` | 401 | Token tidak ada/kedaluwarsa |
| `forbidden` | 403 | Role tidak berhak |
| `not-found` | 404 | Resource tidak ditemukan |
| `conflict` | 409 | Konflik data umum |
| `product-unavailable` | 409 | Produk tidak tersedia/diarsipkan |
| `invalid-status-transition` | 409 | Transisi status tidak diizinkan |
| `order-deadline-exceeded` | 409 | Batas pembayaran habis |
| `payment-mismatch` | 409 | Nominal webhook tidak cocok |
| `password-change-required` | 403 | Akun wajib mengganti password terlebih dahulu |
| `rate-limited` | 429 | Melebihi batas request |
| `internal` | 500 | Kesalahan server |

### 1.3 Rate limit

| Area | Batas default |
|---|---|
| `POST /auth/login`, `POST /auth/refresh` | 10 / menit / IP |
| `/orders/*` (buat, tracking, QRIS, simulasi) | 40 / menit / IP |
| `GET /public/*` | 120 / menit / IP |
| `POST /webhooks/midtrans` | 120 / menit / IP |
| Endpoint staf (global) | 300 / menit / IP |

Respons `429` memakai `application/problem+json` dan menyertakan header
`Retry-After`. Nilai dapat diubah melalui konfigurasi `RateLimiting:*`.

Semua respons API memuat `X-Content-Type-Options: nosniff`,
`X-Frame-Options: DENY`, `Referrer-Policy`, `Permissions-Policy`, dan
`Cross-Origin-Opener-Policy`. HSTS dikirim saat HTTPS. Halaman SPA memakai CSP
yang diatur nginx.

---

## 2. Autentikasi

### 2.1 Login staf

`POST /auth/login`

```json
{
  "email": "owner@hanoracafe.id",
  "password": "••••••••"
}
```

Respons `200`:

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIs...",
  "expiresIn": 900,
  "user": {
    "id": "9f1c...",
    "name": "Owner Hanora",
    "email": "owner@hanoracafe.id",
    "role": "Owner",
    "mustChangePassword": true
  }
}
```

Refresh token dikirim sebagai cookie httpOnly. Tidak pernah masuk body respons.

### 2.2 Refresh

`POST /auth/refresh` — memakai cookie. Respons sama seperti login; refresh token lama dicabut dan diganti (rotasi).

### 2.3 Logout

`POST /auth/logout` — mencabut refresh token dan menghapus cookie.

### 2.4 Profil

`GET /auth/me` — mengembalikan profil dan role dari access token.

### 2.5 Ganti password

`POST /auth/change-password`

```json
{
  "currentPassword": "••••••••",
  "newPassword": "••••••••••••"
}
```

- Minimal 12 karakter; tidak boleh sama dengan password lama.
- Respons `204`; seluruh refresh token lain dicabut.
- Jika `mustChangePassword = true`, endpoint staf selain auth ditolak `403 password-change-required` sampai password diganti.
- Frontend mengarahkan ke `/ganti-password` setelah login pertama.

### 2.6 Sesi meja (customer)

`GET /public/tables/{qrToken}`

Respons `200`:

```json
{
  "table": { "id": "…", "number": "01", "name": "Meja 01" },
  "tableSessionToken": "eyJhbGciOiJIUzI1NiIs...",
  "expiresIn": 7200
}
```

- Token QR tidak valid/dicabut → `404`.
- Sesi meja berlaku 2 jam; tidak dapat dipakai untuk endpoint staf.
- Customer tidak memiliki akun, password, atau profil.

---

## 3. Website Publik

| Method | Endpoint | Auth | Keterangan |
|---|---|---|---|
| GET | `/public/site` | – | Profil bisnis, jam, kontak, galeri |
| GET | `/public/categories` | – | Kategori aktif |
| GET | `/public/products?category=&search=&page=` | – | Produk tersedia |
| GET | `/public/products/{slug}` | – | Detail produk |
| GET | `/public/tables/{qrToken}` | – | Validasi QR + sesi meja |

Contoh produk:

```json
{
  "id": "1a2b…",
  "name": "Hanora Signature Latte",
  "slug": "hanora-signature-latte",
  "category": { "id": "…", "name": "Coffee", "slug": "coffee" },
  "description": "Espresso khas Hanora dengan susu creamy.",
  "price": 32000,
  "imageUrl": "/media/products/8f3c….webp",
  "isAvailable": true
}
```

---

## 4. Pemesanan Customer

### 4.1 Buat pesanan

`POST /orders` — membutuhkan Bearer sesi meja dan header `Idempotency-Key` (UUID).

```json
{
  "customerName": "Nadia",
  "paymentMethod": "qris",
  "notes": "Tolong dipisah",
  "items": [
    { "productId": "1a2b…", "quantity": 2, "notes": "Less sugar" },
    { "productId": "3c4d…", "quantity": 1 }
  ]
}
```

Aturan validasi:
- `customerName` 2–80 karakter.
- `items` 1–50 baris, `quantity` 1–99.
- `productId` harus ada, tidak diarsipkan, dan `is_available = true`.
- Harga, subtotal, PB1, dan total **dihitung server**. Harga dari client diabaikan.
- `paymentMethod`: `qris` atau `cash`.

Respons QRIS `201`:

```json
{
  "orderNumber": "HNR-20261107-0001",
  "status": "pending_payment",
  "customerName": "Nadia",
  "table": { "number": "01", "name": "Meja 01" },
  "items": [
    {
      "productName": "Hanora Signature Latte",
      "unitPrice": 32000,
      "quantity": 2,
      "subtotal": 64000,
      "notes": "Less sugar"
    }
  ],
  "subtotal": 88000,
  "taxRate": 10,
  "taxAmount": 8800,
  "discountAmount": 0,
  "total": 96800,
  "trackingToken": "V1StGXR8_Z5jdHi6B-myT…",
  "payment": {
    "method": "qris",
    "provider": "midtrans",
    "status": "pending",
    "snapToken": "66e4fa55-…",
    "redirectUrl": "https://app.sandbox.midtrans.com/snap/v4/redirection/…",
    "expiresAt": "2026-11-07T14:45:00+07:00"
  },
  "createdAt": "2026-11-07T14:30:00+07:00"
}
```

Jika pembuatan transaksi Snap gagal, pesanan tetap tersimpan sebagai `pending_payment` dan API mengembalikan `502`. Customer dapat mencoba lagi tanpa membuat pesanan baru.

Respons tunai `201`:

```json
{
  "orderNumber": "HNR-20261107-0002",
  "status": "pending_confirmation",
  "payment": { "method": "cash", "provider": "manual", "status": "pending" },
  "total": 96800,
  "trackingToken": "…"
}
```

### 4.2 Tracking pesanan

`GET /orders/track/{trackingToken}` — publik, rate-limited.

```json
{
  "orderNumber": "HNR-20261107-0001",
  "status": "processing",
  "customerName": "Nadia",
  "table": { "number": "01", "name": "Meja 01" },
  "total": 96800,
  "payment": { "method": "qris", "status": "paid", "paidAt": "2026-11-07T14:36:12+07:00" },
  "timeline": [
    { "status": "pending_payment", "at": "2026-11-07T14:30:00+07:00", "note": "Pesanan dibuat" },
    { "status": "processing", "at": "2026-11-07T14:36:13+07:00", "note": "Pembayaran diterima" }
  ]
}
```

Frontend melakukan polling setiap 5 detik selama status belum final.

### 4.3 Batalkan pesanan

`POST /orders/track/{trackingToken}/cancel`

- Diizinkan hanya pada `pending_payment` atau `pending_confirmation`.
- Body: `{ "reason": "Salah pilih menu" }` (opsional).
- Pembayaran QRIS yang masih `pending` diubah menjadi `cancelled`.

### 4.4 Ulangi pembayaran QRIS

`POST /orders/track/{trackingToken}/payments/qris`

- Hanya jika order `pending_payment` dan belum ada payment `paid`.
- Membuat payment attempt baru (`provider_order_id` dengan suffix `-P2`, `-P3`, …).
- Payment lama yang `expired` tetap tersimpan untuk audit.
- Mode `sandbox`/`production` memanggil Midtrans Snap dan mengembalikan `snapToken` serta `redirectUrl`.
- Mode `simulation` mengembalikan `simulation: true` dan `redirectUrl` internal ke halaman simulasi.

Contoh respons mode simulasi:

```json
{
  "method": "qris",
  "provider": "midtrans",
  "status": "pending",
  "paidAt": null,
  "expiresAt": "2026-11-08T18:45:00+00:00",
  "snapToken": "SIM-…",
  "redirectUrl": "/pesanan/<trackingToken>/simulasi",
  "simulation": true
}
```

### 4.5 Simulasi pembayaran (mode simulation)

`POST /orders/track/{trackingToken}/simulate` — hanya tersedia jika `Midtrans:Mode=simulation`.

- Menandai payment QRIS `paid`, order `processing`, `paid_at` diisi, dan income dibuat.
- Idempoten: memanggil ulang saat sudah `paid` hanya mengembalikan tracking.
- Mengembalikan `409 simulation_disabled` pada mode sandbox/production.
- Halaman `/pesanan/{trackingToken}/simulasi` menampilkan penanda jelas bahwa ini bukan pembayaran nyata.

---

## 5. Webhook Midtrans

`POST /webhooks/midtrans` — tanpa JWT, diverifikasi signature.

Contoh payload (disederhanakan):

```json
{
  "transaction_time": "2026-11-07 14:36:12",
  "transaction_status": "settlement",
  "transaction_id": "b1e3…",
  "status_message": "midtrans payment notification",
  "status_code": "200",
  "signature_key": "a1b2…",
  "payment_type": "qris",
  "order_id": "HNR-20261107-0001-P1",
  "gross_amount": "96800.00",
  "fraud_status": "accept",
  "currency": "IDR"
}
```

Prosedur backend:
1. Hitung `SHA512(order_id + status_code + gross_amount + ServerKey)`.
2. Bandingkan dengan `signature_key` memakai perbandingan constant-time.
3. Cari payment berdasarkan `provider_order_id`.
4. Bandingkan `gross_amount` dengan `payments.gross_amount` (parse desimal, bukan string).
5. Cegah duplikat dengan `notification_hash` unik.
6. Dalam satu transaksi database: update payment, order, `incomes`, dan `transaction_histories`.
7. Respons `200 { "message": "ok" }` jika sukses, `duplicate_notification` jika sudah pernah diproses, `amount_mismatch` jika nominal berbeda (payment ditandai `needs_review`).
8. Signature tidak valid → `401`; `payment_not_found` tetap dijawab `200` agar Midtrans tidak mengulang tanpa akhir.

Pemetaan status Midtrans:

| `transaction_status` | Aksi |
|---|---|
| `settlement` | payment `paid`; order `processing`; income dibuat |
| `capture` + `fraud_status=accept` | payment `paid` |
| `capture` + `fraud_status=challenge` | payment `pending`; `needs_review=true` |
| `pending` | tidak ada perubahan final |
| `deny` | payment `failed` |
| `cancel` | payment `cancelled` |
| `expire` | payment `expired`; order tetap `pending_payment` sampai deadline |
| `refund` / `partial_refund` | payment `refunded`; audit log; tidak mengubah order otomatis |
| `failure` | payment `failed` |

---

## 6. Dashboard Kasir

| Method | Endpoint | Keterangan |
|---|---|---|
| GET | `/cashier/orders?status=&from=&to=&page=` | Antrean pesanan |
| GET | `/cashier/orders/{id}` | Detail + item + riwayat |
| PATCH | `/cashier/orders/{id}/status` | Ubah status (`processing`→`ready`→`completed`) |
| POST | `/cashier/orders/{id}/confirm-cash` | Konfirmasi tunai diterima |
| POST | `/cashier/orders/{id}/cancel` | Batalkan pesanan |
| GET | `/cashier/transactions?from=&to=&method=&status=&page=` | Riwayat transaksi |

Contoh ubah status:

```json
{
  "toStatus": "ready",
  "note": "Pesanan sudah di meja"
}
```

Transisi yang diizinkan untuk kasir: `pending_confirmation → processing`, `processing → ready`, `ready → completed`, serta pembatalan `pending_confirmation`.

`POST /cashier/orders/{id}/confirm-cash`:

```json
{ "note": "Tunai diterima Rp 96.800" }
```

Efek: `payments.status=paid`, `orders.status=processing`, `orders.paid_at` diisi, income dibuat, riwayat dicatat.

---

> **Implementasi Tahap 7:** daftar, detail, perubahan status, konfirmasi tunai, pembatalan, dan riwayat transaksi kasir sudah aktif. Konfirmasi tunai mencatat income dan memindahkan pesanan ke `processing`.
>
> **Implementasi Tahap 9:** dashboard owner, pengeluaran, pemasukan manual, dan
> export laporan sudah aktif. Lihat catatan pada bagian 7.4.

---

## 7. Dashboard Owner

### 7.1 Ringkasan & grafik

| Method | Endpoint | Keterangan |
|---|---|---|
| GET | `/owner/dashboard/summary?from=&to=` | Pendapatan, transaksi, pesanan aktif, pengeluaran, laba |
| GET | `/owner/dashboard/sales-trend?from=&to=&groupBy=day\|month` | Grafik pendapatan |
| GET | `/owner/dashboard/top-products?from=&to=&limit=` | Produk terlaris |
| GET | `/owner/dashboard/payment-methods?from=&to=` | Komposisi QRIS/tunai |

Contoh ringkasan:

```json
{
  "from": "2026-11-01",
  "to": "2026-11-07",
  "totalRevenue": 14500000,
  "totalTransactions": 132,
  "activeOrders": 7,
  "totalExpenses": 4200000,
  "netProfit": 10300000,
  "averageOrderValue": 109848,
  "qrisRevenue": 11800000,
  "cashRevenue": 2700000
}
```

Definisi:
- `totalRevenue` = jumlah `incomes` (order + manual) pada periode.
- `totalExpenses` = jumlah `expenses` pada periode.
- `netProfit` = `totalRevenue − totalExpenses`.
- `activeOrders` = pesanan dengan status `pending_payment`, `pending_confirmation`, `processing`, atau `ready`.

### 7.2 Produk & kategori

| Method | Endpoint | Keterangan |
|---|---|---|
| GET | `/owner/categories` | Daftar kategori |
| POST | `/owner/categories` | Tambah kategori |
| PUT | `/owner/categories/{id}` | Ubah kategori |
| DELETE | `/owner/categories/{id}` | Hapus (ditolak jika masih dipakai produk) |
| GET | `/owner/products?category=&search=&status=&page=` | Daftar produk |
| POST | `/owner/products` | Tambah produk |
| GET | `/owner/products/{id}` | Detail |
| PUT | `/owner/products/{id}` | Ubah produk |
| DELETE | `/owner/products/{id}` | Arsipkan produk |
| PATCH | `/owner/products/{id}/availability` | Toggle ketersediaan |
| POST | `/owner/products/{id}/image` | Upload gambar (`multipart/form-data`) |

Contoh tambah produk:

```json
{
  "name": "Hanora Signature Latte",
  "categoryId": "1a2b…",
  "description": "Espresso khas Hanora dengan susu creamy.",
  "price": 32000,
  "isAvailable": true
}
```

Upload gambar:
- Field `file`.
- Maks 2 MB; `image/jpeg`, `image/png`, `image/webp`.
- Validasi magic bytes, bukan hanya `Content-Type`.
- Nama file diacak; path dikembalikan sebagai `imageUrl`.

### 7.3 Meja & QR

| Method | Endpoint | Keterangan |
|---|---|---|
| GET | `/owner/tables` | Daftar meja + status QR |
| POST | `/owner/tables` | Tambah meja + QR aktif pertama |
| PUT | `/owner/tables/{id}` | Ubah nama/kapasitas/status |
| DELETE | `/owner/tables/{id}` | Hapus (ditolak jika ada pesanan) |
| GET | `/owner/tables/{id}/qr?format=svg\|png` | Unduh QR |
| POST | `/owner/tables/{id}/qr/regenerate` | Cabut token lama, buat token baru |

QR mengarah ke `https://<domain>/t/<token>`.

### 7.4 Keuangan

| Method | Endpoint | Keterangan |
|---|---|---|
| GET | `/owner/expenses?from=&to=&page=` | Daftar pengeluaran |
| POST | `/owner/expenses` | Catat pengeluaran |
| PUT | `/owner/expenses/{id}` | Ubah |
| DELETE | `/owner/expenses/{id}` | Hapus |
| GET | `/owner/incomes?from=&to=&page=` | Pemasukan (order + manual) |
| POST | `/owner/incomes` | Pemasukan manual |
| DELETE | `/owner/incomes/{id}` | Hapus pemasukan manual saja |
| GET | `/owner/reports/export?format=pdf\|xlsx&type=full&from=&to=` | Export laporan |

`type`: `summary`, `transactions`, `expenses`, `full`.
Respons: `application/pdf` atau `.xlsx` dengan `Content-Disposition`.

> **Implementasi Tahap 9:** seluruh endpoint dashboard, pengeluaran, pemasukan
> manual, dan export sudah aktif.
>
> - Rentang tanggal default: awal bulan berjalan sampai hari ini (WIB).
> - Rentang maksimal 366 hari; `from > to` ditolak `400`.
> - `totalRevenue` dihitung dari `incomes` (pesanan + manual), sedangkan
>   `qrisRevenue`/`cashRevenue` dihitung dari `payments` berstatus `paid`
>   berdasarkan `paid_at`.
> - `activeOrders` menghitung pesanan berstatus `pending_payment`,
>   `pending_confirmation`, `processing`, atau `ready`.
> - Export PDF memakai QuestPDF dan XLSX memakai ClosedXML; angka diambil
>   langsung dari database.
> - Pemasukan dengan `source=order` tidak dapat dihapus melalui endpoint
>   incomes (respons `409 income_from_order`).

### 7.5 Pengguna & pengaturan

| Method | Endpoint | Keterangan |
|---|---|---|
| GET | `/owner/users?page=` | Daftar user internal |
| POST | `/owner/users` | Buat user (Owner/Kasir) |
| PUT | `/owner/users/{id}` | Ubah nama/email/role |
| PATCH | `/owner/users/{id}/status` | Aktif/nonaktif |
| GET | `/owner/settings` | Profil bisnis publik (nama, tagline, alamat, kontak, jam) |
| PUT | `/owner/settings` | Ubah profil bisnis publik |
| GET | `/owner/audit-logs?from=&to=&page=` | Audit log |

Catatan: `/owner/settings` saat ini mengelola profil publik (nama, tagline, deskripsi, alamat, telepon, Instagram, jam buka, URL peta). Pengaturan pajak dan batas waktu pembayaran akan ditambahkan pada tahap pembayaran.

Aturan:
- Owner tidak dapat menonaktifkan akunnya sendiri.
- Password user baru minimal 12 karakter dan harus diganti saat login pertama (`mustChangePassword=true`).
- Perubahan `tax_rate` hanya berlaku untuk pesanan baru (snapshot).

---

## 8. Kesehatan Sistem

| Method | Endpoint | Keterangan |
|---|---|---|
| GET | `/health` | Liveness |
| GET | `/health/ready` | Kesiapan database & storage |

---

## 9. Peta Route Frontend

| Route | Halaman | Guard |
|---|---|---|
| `/` | Beranda | Publik |
| `/menu` | Menu lengkap | Publik |
| `/galeri` | Galeri | Publik |
| `/lokasi` | Lokasi & kontak | Publik |
| `/masuk` | Login staf | Publik |
| `/ganti-password` | Ganti password wajib | Staf login |
| `/t/:qrToken` | Validasi QR meja | Publik |
| `/pesan` | Katalog & keranjang | Sesi meja |
| `/checkout` | Checkout & pembayaran | Sesi meja |
| `/pesanan/:trackingToken` | Status pesanan | Token tracking |
| `/kasir` | Antrean kasir | Kasir/Owner |
| `/kasir/pesanan/:id` | Detail pesanan | Kasir/Owner |
| `/kasir/riwayat` | Riwayat transaksi | Kasir/Owner |
| `/owner` | Ringkasan owner | Owner |
| `/owner/produk` | Manajemen produk | Owner |
| `/owner/kategori` | Manajemen kategori | Owner |
| `/owner/meja` | Manajemen meja & QR | Owner |
| `/owner/laporan` | Laporan & export | Owner |
| `/owner/pengeluaran` | Pengeluaran & pemasukan | Owner |
| `/owner/pengguna` | Manajemen user | Owner |
| `/owner/pengaturan` | Pengaturan bisnis | Owner |
| `/403` | Akses ditolak | – |
| `/404` | Tidak ditemukan | – |

---

## 10. Catatan Implementasi

- Semua nominal dihitung ulang di server; client hanya mengirim `productId` dan `quantity`.
- Endpoint owner/kasir tidak boleh diakses dengan token sesi meja.
- `POST /orders` tanpa `Idempotency-Key` ditolak `400`.
- Webhook tidak boleh memakai `[Authorize]`; keamanannya berasal dari signature.
- Jangan pernah mencatat `ServerKey`, password, atau token mentah di log.
- Perubahan kontrak API harus memperbarui `openapi.yaml` dan dokumen ini.
