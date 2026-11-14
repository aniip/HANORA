# ALUR SISTEM — HANORA CAFE

**Versi:** 1.0 (Tahap 2)
**Tujuan:** Mendefinisikan urutan interaksi, state machine, dan penanganan kasus tepi sebelum implementasi.

---

## 1. Scan QR dan Sesi Meja

```mermaid
sequenceDiagram
    autonumber
    actor C as Customer
    participant FE as Frontend
    participant API as API
    participant DB as PostgreSQL

    C->>FE: Scan QR di meja
    FE->>API: GET /public/tables/{qrToken}
    API->>DB: Cari QR aktif + meja
    DB-->>API: Data meja
    alt QR tidak valid / dicabut
        API-->>FE: 404 Not Found
        FE-->>C: "QR tidak berlaku, silakan hubungi kasir"
    else QR valid
        API-->>FE: table + tableSessionToken (2 jam)
        FE->>API: GET /public/products
        API-->>FE: Daftar menu tersedia
        FE-->>C: Katalog meja 01
    end
```

Aturan:
- Sesi meja adalah JWT audience `table`, bukan access token staf.
- Jika QR diregenerasi, sesi lama tidak dapat membuat pesanan baru karena backend memeriksa ulang status QR.
- Customer tidak memasukkan nomor meja secara manual.

---

## 2. Pesanan QRIS (Midtrans)

```mermaid
sequenceDiagram
    autonumber
    actor C as Customer
    participant FE as Frontend
    participant API as API
    participant DB as PostgreSQL
    participant MT as Midtrans

    C->>FE: Isi nama, pilih QRIS, checkout
    FE->>API: POST /orders (Bearer sesi meja, Idempotency-Key)
    API->>DB: Validasi produk & hitung ulang harga
    API->>DB: BEGIN; INSERT order + items; COMMIT
    API->>MT: Buat transaksi Snap (QRIS)
    alt Snap gagal
        API->>DB: Tandai payment gagal dibuat
        API-->>FE: 502 "Pembayaran belum dapat dibuat"
        FE-->>C: Tawarkan coba lagi
    else Snap berhasil
        API->>DB: Simpan snap_token, redirect_url, expires_at
        API-->>FE: 201 order + snapToken
        FE->>MT: Buka halaman Snap / tampilkan QRIS
        C->>MT: Bayar dengan aplikasi bank/e-wallet
        MT->>API: POST /webhooks/midtrans
        API->>API: Verifikasi signature + nominal + idempotency
        API->>DB: BEGIN; lock payment; update paid; order processing; income; history; COMMIT
        API-->>MT: 200 { status: ok }
        loop Polling setiap 5 detik
            FE->>API: GET /orders/track/{trackingToken}
            API-->>FE: status processing, payment paid
        end
        FE-->>C: "Pembayaran berhasil, pesanan diproses"
    end
```

Catatan:
- Panggilan ke Midtrans **tidak** dilakukan di dalam transaksi database yang panjang.
- `gross_amount` yang dikirim ke Midtrans sama dengan `orders.total` (termasuk PB1).
- Frontend tidak pernah menerima Server Key.
- Status final selalu diambil dari backend, bukan dari callback Snap di browser.

---

## 3. Pesanan Tunai

```mermaid
sequenceDiagram
    autonumber
    actor C as Customer
    participant FE as Frontend
    participant API as API
    participant DB as PostgreSQL
    actor K as Kasir

    C->>FE: Pilih tunai, checkout
    FE->>API: POST /orders (paymentMethod=cash)
    API->>DB: BEGIN; INSERT order pending_confirmation + payment manual pending; COMMIT
    API-->>FE: 201 order + trackingToken
    FE-->>C: "Tunjukkan nomor pesanan ke kasir"
    C->>K: Datang ke kasir dan bayar
    K->>API: POST /cashier/orders/{id}/confirm-cash
    API->>DB: BEGIN; lock; payment paid; order processing; income; history; COMMIT
    API-->>K: Detail pesanan
    FE->>API: GET /orders/track/{trackingToken}
    API-->>FE: processing + paid
```

Aturan:
- Tunai tidak melalui Midtrans.
- Pesanan tunai langsung `pending_confirmation`, bukan `pending_payment`.
- Jika customer pergi tanpa membayar, kasir membatalkan pesanan dengan alasan.

---

## 4. Perubahan Status oleh Kasir

```mermaid
sequenceDiagram
    autonumber
    actor K as Kasir
    participant API as API
    participant DB as PostgreSQL

    K->>API: PATCH /cashier/orders/{id}/status { toStatus: ready }
    API->>DB: BEGIN; SELECT order FOR UPDATE
    API->>API: Validasi transisi processing -> ready
    alt Transisi tidak sah
        API-->>K: 409 invalid-status-transition
    else Valid
        API->>DB: UPDATE order + INSERT transaction_history
        API->>DB: COMMIT
        API-->>K: Detail terbaru
    end
    K->>API: PATCH ... { toStatus: completed }
    API->>DB: UPDATE ready -> completed + completed_at + history
```

Transisi kasir:
- `pending_confirmation → processing` (setelah tunai dikonfirmasi)
- `processing → ready`
- `ready → completed`
- `pending_confirmation → cancelled`

Kasir tidak dapat membatalkan pesanan yang sudah `processing`/`ready`; pembatalan setelah pembayaran ditangani owner secara manual (refund di luar sistem).

---

## 5. Webhook: Duplikat, Terlambat, dan Nominal Tidak Cocok

```mermaid
sequenceDiagram
    autonumber
    participant MT as Midtrans
    participant API as API
    participant DB as PostgreSQL

    MT->>API: settlement (P1)
    API->>DB: notification_hash sudah ada?
    alt Duplikat
        API-->>MT: 200 ok (tanpa perubahan)
    else Baru
        API->>API: Verifikasi signature
        alt Signature invalid
            API-->>MT: 401 Unauthorized
        else Signature valid
            API->>DB: Bandingkan gross_amount
            alt Nominal tidak cocok
                API->>DB: needs_review=true + audit log
                API-->>MT: 200 ok (tidak mengubah status)
            else Nominal cocok
                API->>DB: Update payment paid, order processing, income, history
                API-->>MT: 200 ok
            end
        end
    end
    MT->>API: settlement (retry)
    API->>DB: hash ada -> 200 ok
    MT->>API: pending (terlambat)
    API->>DB: payment sudah paid -> abaikan
    API-->>MT: 200 ok
```

Kasus pembayaran telat setelah pesanan dibatalkan:

```mermaid
sequenceDiagram
    autonumber
    participant MT as Midtrans
    participant API as API
    participant DB as PostgreSQL

    Note over DB: Order sudah cancelled karena melewati 30 menit
    MT->>API: settlement
    API->>DB: Payment paid, needs_review=true, audit log payment.late_received
    Note over DB: Order tetap cancelled, income tidak dibuat otomatis
    API-->>MT: 200 ok
    Note over API: Owner menindaklanjuti refund manual di dashboard Midtrans
```

Prinsip:
- `paid` bersifat final; notifikasi `pending` yang datang belakangan tidak menurunkannya.
- Uang yang masuk tidak boleh hilang dari catatan: payment tetap `paid`, tetapi `needs_review` menandai tindakan manual.
- Semua penyimpangan dicatat di `audit_logs`.

---

## 6. Kedaluwarsa Pembayaran dan Batas Pesanan

```mermaid
sequenceDiagram
    autonumber
    participant W1 as PaymentExpiryWorker
    participant W2 as OrderDeadlineWorker
    participant DB as PostgreSQL

    loop Setiap menit
        W1->>DB: SELECT payments WHERE status=pending AND expires_at < now()
        W1->>DB: UPDATE status=expired
        W2->>DB: SELECT orders WHERE status=pending_payment AND created_at < now()-30m
        W2->>DB: UPDATE status=cancelled + cancel_reason + history
        W2->>DB: UPDATE payments pending -> cancelled
    end
```

- QRIS kedaluwarsa 15 menit; customer masih dapat membuat payment attempt baru selama pesanan belum melewati 30 menit.
- Setelah 30 menit, pesanan dibatalkan otomatis.
- Worker menggunakan advisory lock bila API dijalankan lebih dari satu instance.

---

## 7. Tracking Pesanan Customer

```mermaid
sequenceDiagram
    autonumber
    actor C as Customer
    participant FE as Frontend
    participant API as API

    C->>FE: Buka halaman status
    loop Setiap 5 detik selama belum final
        FE->>API: GET /orders/track/{trackingToken}
        API-->>FE: status + timeline + payment
    end
    alt completed / cancelled
        FE->>FE: Hentikan polling
        FE-->>C: Tampilkan status final
    end
```

- Polling berhenti saat tab tidak aktif (`document.visibilityState`).
- Token tracking disimpan di `sessionStorage`; jika hilang, customer scan ulang QR.
- Endpoint tracking tidak menampilkan data internal (ID database, catatan kasir, token pembayaran).

---

## 8. Laporan Owner

```mermaid
sequenceDiagram
    autonumber
    actor O as Owner
    participant FE as Frontend
    participant API as API
    participant DB as PostgreSQL

    O->>FE: Pilih periode 1-7 November
    FE->>API: GET /owner/dashboard/summary?from=2026-11-01&to=2026-11-07
    API->>DB: SUM incomes, expenses, hitung order aktif
    API-->>FE: Ringkasan
    O->>FE: Export PDF
    FE->>API: GET /owner/reports/export?format=pdf&type=full&from=...&to=...
    API->>DB: Query transaksi + pengeluaran periode
    API->>API: Render PDF (QuestPDF)
    API-->>FE: application/pdf
    FE->>O: Unduh laporan
```

- Export dibatasi maksimal 1 tahun per permintaan untuk menjaga memori.
- Nama file: `Laporan-Hanora-2026-11-01_2026-11-07.pdf` / `.xlsx`.
- Angka laporan berasal dari database, bukan state frontend.

---

## 9. State Machine

### 9.1 Status pesanan

```mermaid
stateDiagram-v2
    [*] --> pending_payment
    pending_payment --> processing: QRIS lunas (webhook)
    pending_payment --> pending_confirmation: Pesanan tunai
    pending_payment --> cancelled: Customer batal / deadline 30 menit
    pending_confirmation --> processing: Kasir konfirmasi tunai
    pending_confirmation --> cancelled: Customer / kasir batal
    processing --> ready: Kasir menandai siap
    processing --> cancelled: Owner batal (refund manual)
    ready --> completed: Kasir menandai selesai
    completed --> [*]
    cancelled --> [*]
```

### 9.2 Status pembayaran

```mermaid
stateDiagram-v2
    [*] --> pending
    pending --> paid: settlement / capture accept
    pending --> expired: melewati expires_at / webhook expire
    pending --> failed: deny / failure
    pending --> cancelled: pesanan dibatalkan
    paid --> refunded: refund manual
    expired --> [*]
    failed --> [*]
    cancelled --> [*]
    refunded --> [*]
```

---

## 10. Kasus Tepi dan Perilaku yang Diharapkan

| Kasus | Perilaku |
|---|---|
| QR tidak valid/dicabut | `404`, customer diarahkan ke kasir |
| QR diregenerasi saat sesi aktif | Pesanan baru ditolak; customer scan ulang |
| Produk menjadi tidak tersedia setelah masuk keranjang | Checkout ditolak `409` dengan daftar produk |
| Harga berubah setelah masuk keranjang | Server memakai harga terbaru; total dihitung ulang |
| Double tap checkout | Idempotency-Key mengembalikan pesanan yang sama |
| Snap gagal dibuat | Pesanan tetap ada; customer dapat mencoba lagi |
| QRIS kedaluwarsa | Payment `expired`; customer dapat membuat attempt baru selama < 30 menit |
| Melewati 30 menit | Pesanan `cancelled`; payment pending menjadi `cancelled` |
| Webhook duplikat | `200` tanpa perubahan |
| Webhook dengan signature salah | `401`, dicatat, tidak ada perubahan |
| Nominal webhook tidak cocok | `needs_review=true`, status tidak berubah, audit log |
| Webhook `pending` setelah `paid` | Diabaikan; `paid` final |
| Pembayaran masuk setelah pesanan dibatalkan | Payment `paid` + `needs_review`, order tetap `cancelled`, refund manual |
| Customer membatalkan setelah `processing` | Ditolak `409`; hubungi kasir |
| Kasir membatalkan pesanan yang sudah dibayar | Tidak diizinkan lewat endpoint kasir; owner menangani refund manual |
| Refresh token dipakai dua kali bersamaan | Rotasi single-flight; token lama dicabut, sesi lain diminta login ulang |
| Server restart | Data aman di PostgreSQL; worker melanjutkan pemeriksaan |
| Midtrans tidak dapat dihubungi | Pesanan tetap `pending_payment`; error ditampilkan; coba lagi |
| Tab customer ditutup | Pesanan tetap diproses; status dapat dilihat dengan scan ulang QR |

---

## 11. Definisi "Selesai"

Pesanan dianggap selesai ketika:
1. Pembayaran berstatus `paid`; dan
2. Kasir menandai `completed`.

Pesanan `cancelled` tidak dihitung sebagai pendapatan. Pembayaran telat yang memerlukan refund tidak dihitung sebagai pendapatan sampai keputusan owner dicatat.

---

## 12. Alur Autentikasi Staf

```mermaid
sequenceDiagram
    autonumber
    actor S as Staf
    participant FE as Frontend
    participant API as API
    participant DB as PostgreSQL

    S->>FE: Email + password
    FE->>API: POST /auth/login
    API->>DB: Cari user + role
    API->>API: Verifikasi password hash
    alt Valid
        API->>DB: Simpan refresh token (hash) + audit
        API-->>FE: accessToken + cookie refresh httpOnly + marker sesi
        FE->>FE: Simpan access token di memori
        alt mustChangePassword = true
            FE-->>S: Arahkan ke /ganti-password
        else
            FE-->>S: Dashboard sesuai role
        end
    else Tidak valid
        API->>DB: Audit login gagal
        API-->>FE: 401 (pesan generik)
    end

    Note over FE,API: Saat halaman dimuat ulang
    FE->>API: POST /auth/refresh (cookie)
    API->>DB: Cek hash, expiry, revoked
    alt Token valid
        API->>DB: Cabut token lama + buat token baru
        API-->>FE: accessToken baru + cookie baru
    else Token sudah dicabut (reuse)
        API->>DB: Cabut semua sesi aktif user + audit
        API-->>FE: 401
    end
```

Aturan:
- Access token tidak disimpan di `localStorage`; hanya di memori.
- Refresh token httpOnly, `SameSite=Lax`, path `/api/v1/auth`.
- Cookie penanda `hanora_session` hanya untuk bootstrap, bukan otorisasi.
- Endpoint staf yang wajib ganti password akan menolak akses `403 password-change-required` sampai password diubah.
- Ganti password mencabut seluruh refresh token; pengguna harus login ulang.
- Owner tidak dapat menonaktifkan dirinya sendiri atau menghapus owner aktif terakhir.
