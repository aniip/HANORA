-- ============================================================
-- HANORA CAFE — HAPUS TRANSAKSI DEMO
-- Menghapus pesanan HNR-DEMO-*, pembayaran, income, dan expenses demo.
-- Master data (produk, kategori, meja, QR) dibiarkan.
-- ============================================================

SET client_encoding = 'UTF8';

BEGIN;

DELETE FROM incomes
WHERE order_id IN (
    SELECT id FROM orders WHERE order_number LIKE 'HNR-DEMO-%'
)
OR (source = 'manual' AND description = 'Penjualan merchandise');

DELETE FROM payments
WHERE order_id IN (
    SELECT id FROM orders WHERE order_number LIKE 'HNR-DEMO-%'
);

DELETE FROM transaction_histories
WHERE order_id IN (
    SELECT id FROM orders WHERE order_number LIKE 'HNR-DEMO-%'
);

DELETE FROM orders
WHERE order_number LIKE 'HNR-DEMO-%';

DELETE FROM expenses
WHERE description IN (
    'Pembelian biji kopi',
    'Listrik dan air',
    'Cup dan lid',
    'Upah part-time',
    'Perawatan mesin espresso'
);

COMMIT;
