# Database HANORA CAFE

Folder ini menyimpan catatan dan alat bantu database. Skema aplikasi dikelola melalui **EF Core Migration** di project `backend/src/Hanora.Infrastructure`.

## PostgreSQL Lokal

Jalankan dari folder `hanora-app`:

```bash
docker compose up -d postgres
```

Cek status:

```bash
docker compose ps
```

Masuk ke psql:

```bash
docker compose exec postgres psql -U hanora -d hanora
```

## Migration (mulai Tahap 4)

```bash
cd backend

dotnet ef migrations add InitialCreate \
  --project src/Hanora.Infrastructure \
  --startup-project src/Hanora.Api

dotnet ef database update \
  --project src/Hanora.Infrastructure \
  --startup-project src/Hanora.Api
```

Aturan:

- Semua perubahan skema melalui migration, bukan edit manual di database production.
- Review SQL migration sebelum diterapkan.
- Backup sebelum migration di production.
- Tidak ada `EnsureCreated()` di production.

## Seed

Tahap 3 belum memiliki data bisnis. Mulai Tahap 4, seed hanya berisi:

- Role `Owner`, `Cashier`, `Customer`.
- Satu akun owner dari environment variable.
- Pengaturan default.

Produk, meja, pesanan, pembayaran, dan pengeluaran **tidak di-seed**.

## Data Demo

Untuk mengisi data contoh (kategori, produk, meja + QR, pesanan, pembayaran,
income, dan pengeluaran):

```bash
psql "host=... dbname=Hanora user=... sslmode=require" \
  -v ON_ERROR_STOP=1 -f database/demo-data.sql
```

Script aman dijalankan berulang; pesanan demo memakai nomor `HNR-DEMO-*` dan
tidak akan diduplikasi.

Hapus transaksi demo saja (master data tetap):

```bash
psql "host=... dbname=Hanora user=... sslmode=require" \
  -v ON_ERROR_STOP=1 -f database/demo-data-clean.sql
```

> Gunakan hanya untuk database development/demo. Jangan jalankan pada database
> produksi yang sudah berisi data nyata.

## Backup

Gunakan `scripts/backup.sh` dan `scripts/restore.sh`. Prosedur lengkap,
termasuk retensi, restore, dan deployment production, ada di
[docs/PRODUCTION.md](../docs/PRODUCTION.md).
