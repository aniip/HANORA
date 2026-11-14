# PANDUAN MIDTRANS QRIS — HANORA CAFE

**Tahap:** 8
**Tanggal:** 8 November 2026

Dokumen ini menjelaskan cara mengaktifkan pembayaran QRIS melalui Midtrans pada
tiga mode: `simulation`, `sandbox`, dan `production`.

---

## 1. Ringkasan Alur

1. Customer memilih **QRIS** di halaman checkout.
2. Backend membuat pesanan dengan status `pending_payment` dan payment QRIS
   `pending`.
3. Frontend memanggil `POST /api/v1/orders/track/{trackingToken}/payments/qris`.
4. Backend membuat transaksi Midtrans Snap dan mengembalikan `snapToken` serta
   `redirectUrl`.
5. Customer menyelesaikan pembayaran di halaman Midtrans.
6. Midtrans mengirim webhook ke `POST /api/v1/webhooks/midtrans`.
7. Backend memvalidasi signature, mencatat notifikasi, dan mengubah payment
   menjadi `paid`, order menjadi `processing`, serta membuat income.

Status order dan status pembayaran tetap terpisah. Pembayaran QRIS yang belum
lunas tidak memindahkan order ke proses dapur.

---

## 2. Mode Pembayaran

| Mode | Kegunaan | Perilaku |
|---|---|---|
| `simulation` | Development/demo tanpa akun Midtrans | Tidak memanggil Midtrans. Halaman simulasi internal menandai pembayaran lunas. |
| `sandbox` | Uji integrasi dengan akun Midtrans sandbox | Memanggil Snap sandbox dan menerima webhook sandbox. |
| `production` | Pembayaran nyata | Memanggil Snap production. Mode simulation ditolak saat startup. |

Nilai mode diatur lewat `MIDTRANS_MODE` atau `Midtrans__Mode`.

> Mode `simulation` **tidak boleh** dipakai di production. Backend akan gagal
> start jika `ASPNETCORE_ENVIRONMENT=Production` dan mode masih `simulation`.

---

## 3. Mode Simulation (default)

Tidak perlu akun Midtrans.

```env
MIDTRANS_MODE=simulation
MIDTRANS_QRIS_ENABLED=true
```

Langkah uji:

1. Buat kategori, produk, dan meja dari dashboard owner.
2. Scan QR meja, pilih menu, lalu checkout dengan metode **QRIS**.
3. Aplikasi membuka `/pesanan/{trackingToken}/simulasi`.
4. Halaman menampilkan banner **Mode simulasi** dan tombol
   **Simulasikan pembayaran berhasil**.
5. Setelah ditekan, pesanan menjadi `processing`, pembayaran menjadi `paid`, dan
   income tercatat.
6. Kasir dapat melanjutkan status `processing → ready → completed`.

Endpoint simulasi hanya aktif jika `Midtrans:Mode=simulation`. Pada mode lain
endpoint mengembalikan `409 simulation_disabled`.

---

## 4. Mode Sandbox

### 4.1 Dapatkan kredensial

1. Daftar/masuk ke <https://dashboard.sandbox.midtrans.com>.
2. Buka **Settings → Access Keys**.
3. Catat **Server Key** dan **Client Key** sandbox.

### 4.2 Isi environment

Di `.env` (jangan commit file ini):

```env
MIDTRANS_MODE=sandbox
MIDTRANS_SERVER_KEY=SB-Mid-server-xxxxxxxxxxxxxxxx
MIDTRANS_CLIENT_KEY=SB-Mid-client-xxxxxxxxxxxxxxxx
MIDTRANS_MERCHANT_ID=Gxxxxxxxx
MIDTRANS_QRIS_ENABLED=true
MIDTRANS_SNAP_URL=
```

- `MIDTRANS_SNAP_URL` boleh dikosongkan. Backend memakai
  `https://app.sandbox.midtrans.com/snap/v1/transactions` untuk sandbox.
- Server key hanya dipakai backend. Jangan pernah memasukkannya ke kode frontend
  atau variabel `VITE_*`.

### 4.3 Daftarkan webhook

Di dashboard sandbox: **Settings → Configuration → Payment Notification URL**.

```
https://domain-anda/api/v1/webhooks/midtrans
```

Untuk pengujian lokal, gunakan tunnel HTTPS (misalnya Cloudflare Tunnel atau
ngrok) yang meneruskan ke `http://localhost:5080`.

Aktifkan juga notifikasi untuk **Finish Redirect URL** bila diperlukan:

```
https://domain-anda/pesanan/<trackingToken>
```

Backend sudah mengirim `callbacks.finish` per transaksi, sehingga customer
kembali ke halaman tracking pesanan.

### 4.4 Aktifkan QRIS

Pastikan metode **QRIS** aktif pada akun Midtrans Anda. Snap request dari
backend memakai `enabled_payments: ["other_qris"]`.

### 4.5 Uji pembayaran

Gunakan simulator pembayaran Midtrans sandbox. Setelah pembayaran berhasil,
webhook akan mengubah status. Jika webhook belum masuk, periksa:

- URL webhook dapat diakses publik dan HTTPS.
- Server key sama dengan yang dipakai saat membuat transaksi.
- Log API (`docker compose logs -f api`).

---

## 5. Mode Production

Checklist sebelum go-live:

- [ ] Akun Midtrans production aktif dan QRIS disetujui.
- [ ] `ASPNETCORE_ENVIRONMENT=Production`.
- [ ] `MIDTRANS_MODE=production`.
- [ ] `MIDTRANS_SERVER_KEY` production (bukan `SB-`).
- [ ] `MIDTRANS_CLIENT_KEY` production.
- [ ] `MIDTRANS_MERCHANT_ID` production.
- [ ] `APP_BASE_URL=https://domain-anda` (tanpa trailing slash).
- [ ] Webhook production didaftarkan di dashboard Midtrans.
- [ ] HTTPS aktif di domain publik.
- [ ] `MIDTRANS_QRIS_ENABLED=true`.
- [ ] Mode simulation tidak digunakan.

Contoh:

```env
ASPNETCORE_ENVIRONMENT=Production
MIDTRANS_MODE=production
MIDTRANS_SERVER_KEY=Mid-server-xxxxxxxxxxxxxxxx
MIDTRANS_CLIENT_KEY=Mid-client-xxxxxxxxxxxxxxxx
MIDTRANS_MERCHANT_ID=Gxxxxxxxx
MIDTRANS_QRIS_ENABLED=true
APP_BASE_URL=https://domain-anda
```

Backend menolak start jika:

- Mode tidak dikenal.
- Mode `sandbox`/`production` tanpa server/client key.
- Mode `production` memakai server key sandbox (`SB-`).
- Mode `simulation` dipakai saat environment Production.

---

## 6. Variabel Environment

| Variabel | Default | Keterangan |
|---|---|---|
| `MIDTRANS_MODE` | `simulation` | `simulation`, `sandbox`, atau `production` |
| `MIDTRANS_SERVER_KEY` | kosong | Wajib untuk sandbox/production; rahasia |
| `MIDTRANS_CLIENT_KEY` | kosong | Client key Midtrans |
| `MIDTRANS_MERCHANT_ID` | kosong | Merchant ID Midtrans |
| `MIDTRANS_SNAP_URL` | otomatis | Override endpoint Snap |
| `MIDTRANS_QRIS_ENABLED` | `true` | Matikan sementara QRIS tanpa mengubah kode |
| `APP_BASE_URL` | `http://localhost:8080` | Dipakai untuk finish redirect |

Di Docker Compose, nilai dipetakan sebagai `Midtrans__Mode`,
`Midtrans__ServerKey`, dan seterusnya.

---

## 7. Validasi Webhook

Signature Midtrans dihitung dengan:

```
SHA512(order_id + status_code + gross_amount + ServerKey)
```

Backend:

1. Memverifikasi signature dengan perbandingan constant-time.
2. Menyimpan setiap notifikasi ke `payment_notifications`, termasuk payload
   mentah, status signature, dan error pemrosesan.
3. Menolak signature tidak valid dengan `401` dan tidak mengubah pembayaran.
4. Mencegah duplikasi memakai `notification_hash` unik.
5. Membandingkan `gross_amount` dengan nominal payment. Jika berbeda, payment
   ditandai `needs_review = true` dan tidak dianggap lunas.
6. Menjalankan perubahan payment/order/income dalam satu transaksi database.

Pemetaan status:

| Status Midtrans | Efek |
|---|---|
| `settlement` | Payment `paid`, order `processing`, income dibuat |
| `capture` + `accept` | Payment `paid` |
| `capture` + `challenge` | Payment tetap `pending`, `needs_review` |
| `pending` | Tidak ada perubahan final |
| `deny` / `failure` | Payment `failed` |
| `cancel` | Payment `cancelled` |
| `expire` | Payment `expired`, order tetap `pending_payment` agar bisa dicoba lagi |
| `refund` / `partial_refund` | Payment `refunded` |
| `chargeback` | Payment `refunded`, `needs_review` |

Webhook yang sama boleh dikirim berulang; respons kedua adalah
`200 { "message": "duplicate_notification" }` dan income tidak digandakan.

---

## 8. Keamanan

- Server key hanya ada di backend/environment server.
- Jangan commit `.env`, server key, atau client key.
- Jangan memakai `https://api.midtrans.com` atau kredensial production untuk
  pengujian.
- Mode simulasi diblokir saat production.
- Endpoint simulasi hanya aktif pada mode `simulation`.
- Semua nominal dihitung ulang di server; nominal dari client diabaikan.
- Webhook tidak memakai JWT, tetapi wajib signature valid.
- Notifikasi dengan nominal tidak cocok tidak pernah dianggap lunas.

---

## 9. Troubleshooting

| Masalah | Penyebab umum | Solusi |
|---|---|---|
| `502 provider_error` saat bayar | Server key salah, jaringan, atau Snap menolak | Periksa log API dan kredensial |
| `409 simulation_disabled` | Mode bukan simulation | Gunakan sandbox/production atau ubah mode |
| Webhook `401 invalid_signature` | Server key berbeda | Samakan server key backend dan dashboard |
| Webhook `amount_mismatch` | Nominal notifikasi berbeda | Periksa transaksi Midtrans; payment ditandai review |
| Pembayaran lunas tetapi order belum berubah | Webhook belum masuk | Cek URL webhook dan log; jangan menandai manual tanpa audit |
| QRIS tidak muncul | `MIDTRANS_QRIS_ENABLED=false` atau metode QRIS belum aktif | Aktifkan env dan metode di dashboard |
| Mode production gagal start | Kunci sandbox / mode simulation | Gunakan kredensial production dan mode `production` |

---

## 10. Referensi

- Snap API: <https://docs.midtrans.com/reference/snap-api>
- Webhook/HTTP Notification: <https://docs.midtrans.com/reference/http-notification>
- QRIS: <https://docs.midtrans.com/reference/qris>
- Signature: <https://docs.midtrans.com/reference/signature-key>
