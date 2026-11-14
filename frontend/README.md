# HANORA CAFE — Frontend

React 19 + Vite 8 + TypeScript 7 + Tailwind CSS 3.4.

## Perintah

```bash
npm install
npm run dev      # http://localhost:5173
npm run build    # tsc -b && vite build
npm run lint     # oxlint
npm run preview
```

## Environment

Salin `.env.example` menjadi `.env` bila perlu:

```
VITE_API_BASE_URL=/api/v1
VITE_API_PROXY_TARGET=http://localhost:5080
```

Vite mem-proxy `/api` ke backend pada development.

## Struktur

```
src/
├─ app/            router
├─ components/
│  ├─ auth/        RequireAuth
│  ├─ layout/      PublicLayout, OwnerLayout, AuthBootstrap
│  ├─ owner/       ProductFormModal, CategoryFormModal
│  ├─ public/      ProductCard, ProductImage, ProductDetailModal
│  └─ ui/          Button style, Modal, EmptyState, SectionTitle, StatusPill
├─ lib/            axios, auth, catalog, format, useApi, error handler
├─ pages/          publik, auth, customer, kasir, dan owner
└─ stores/         Zustand auth, table session, cart
```

## Autentikasi

- Access token disimpan di memori (Zustand), tidak di `localStorage`.
- Refresh token berada di cookie httpOnly yang dikelola backend.
- Saat halaman dimuat, aplikasi memanggil `/auth/refresh` hanya jika penanda sesi ada.
- Route `/owner/*` membutuhkan role Owner; `/kasir/*` membutuhkan Owner atau Kasir.
- Jika `mustChangePassword = true`, pengguna diarahkan ke `/ganti-password`.

## Status

Tahap 7: website publik, katalog, meja & QR, sesi meja, keranjang, checkout tunai, tracking pesanan, dashboard kasir dengan konfirmasi tunai/transisi status, dan riwayat transaksi sudah aktif. QRIS, analitik owner, dan laporan masih menunggu tahap berikutnya.
