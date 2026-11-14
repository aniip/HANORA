-- ============================================================
-- HANORA CAFE — DATA DEMO
-- Jalankan hanya pada database development/demo.
-- Script ini aman dijalankan berulang (idempotent untuk data utama).
-- ============================================================

SET client_encoding = 'UTF8';

BEGIN;

-- 1. Kategori
INSERT INTO categories (name, slug, sort_order, is_active, created_at, updated_at)
VALUES
    ('Coffee', 'coffee', 1, true, now(), now()),
    ('Non-Coffee', 'non-coffee', 2, true, now(), now()),
    ('Pastry', 'pastry', 3, true, now(), now()),
    ('Main Course', 'main-course', 4, true, now(), now())
ON CONFLICT (slug) DO UPDATE
SET name = EXCLUDED.name,
    sort_order = EXCLUDED.sort_order,
    is_active = true,
    updated_at = now();

-- 2. Produk
INSERT INTO products
    (category_id, name, slug, description, price, is_available, created_at, updated_at)
SELECT c.id, v.name, v.slug, v.description, v.price, true, now(), now()
FROM (VALUES
    ('coffee', 'Caffe Latte', 'caffe-latte', 'Espresso dengan susu steamed.', 30000.00),
    ('coffee', 'Cappuccino', 'cappuccino', 'Espresso, susu, dan foam tebal.', 28000.00),
    ('non-coffee', 'Matcha Latte', 'matcha-latte', 'Matcha premium dengan susu.', 34000.00),
    ('non-coffee', 'Chocolate', 'chocolate', 'Cokelat panas dengan susu.', 30000.00),
    ('pastry', 'Croissant Butter', 'croissant-butter', 'Croissant mentega panggang.', 25000.00),
    ('pastry', 'Pain au Chocolat', 'pain-au-chocolat', 'Pastry isi cokelat.', 28000.00),
    ('main-course', 'Chicken Rice Bowl', 'chicken-rice-bowl', 'Nasi dengan ayam panggang.', 45000.00)
) AS v(category_slug, name, slug, description, price)
JOIN categories c ON c.slug = v.category_slug
ON CONFLICT (slug) DO UPDATE
SET name = EXCLUDED.name,
    description = EXCLUDED.description,
    price = EXCLUDED.price,
    is_available = true,
    updated_at = now();

-- Foto menu demo dari Unsplash (bisa diganti dengan upload lokal kapan saja).
UPDATE products p
SET image_path = v.url,
    updated_at = now()
FROM (VALUES
    ('hanora-signature-latte', 'https://images.unsplash.com/photo-1461023058943-07fcbe16d735?auto=format&fit=crop&w=800&q=80'),
    ('caffe-latte', 'https://images.unsplash.com/photo-1541167760496-1628856ab772?auto=format&fit=crop&w=800&q=80'),
    ('cappuccino', 'https://images.unsplash.com/photo-1572442388796-11668a67e53d?auto=format&fit=crop&w=800&q=80'),
    ('matcha-latte', 'https://images.unsplash.com/photo-1515823064-d6e0c04616a7?auto=format&fit=crop&w=800&q=80'),
    ('chocolate', 'https://images.unsplash.com/photo-1542990253-0d0f5be5f0ed?auto=format&fit=crop&w=800&q=80'),
    ('croissant-butter', 'https://images.unsplash.com/photo-1555507036-ab1f4038808a?auto=format&fit=crop&w=800&q=80'),
    ('pain-au-chocolat', 'https://images.unsplash.com/photo-1530610476181-d83430b64dcd?auto=format&fit=crop&w=800&q=80'),
    ('chicken-rice-bowl', 'https://images.unsplash.com/photo-1512058564366-18510be2db19?auto=format&fit=crop&w=800&q=80')
) AS v(slug, url)
WHERE p.slug = v.slug;

-- 3. Meja demo + QR
INSERT INTO dining_tables
    (number, name, capacity, is_active, created_at, updated_at)
VALUES
    ('01', 'Meja 01', 2, true, now(), now()),
    ('02', 'Meja 02', 4, true, now(), now()),
    ('03', 'Meja 03', 4, true, now(), now())
ON CONFLICT (number) DO UPDATE
SET name = EXCLUDED.name,
    capacity = EXCLUDED.capacity,
    is_active = true,
    updated_at = now();

INSERT INTO table_qr_codes (table_id, token, is_active, created_at)
SELECT t.id, v.token, true, now()
FROM (VALUES
    ('01', 'demo-meja-01'),
    ('02', 'demo-meja-02'),
    ('03', 'demo-meja-03')
) AS v(number, token)
JOIN dining_tables t ON t.number = v.number
WHERE NOT EXISTS (
    SELECT 1
    FROM table_qr_codes q
    WHERE q.table_id = t.id
      AND q.is_active = true
      AND q.revoked_at IS NULL
);

-- 4. Pesanan demo, pembayaran, income, histori
DO $$
DECLARE
    v_owner uuid;
    v_today date;
    v_seq int;
    v_day int;
    v_status order_status;
    v_method payment_method;
    v_provider payment_provider;
    v_customer text;
    v_items text;
    v_table_no text;
    v_order_id uuid;
    v_order_number text;
    v_tracking text;
    v_subtotal numeric(12,2);
    v_tax numeric(12,2);
    v_total numeric(12,2);
    v_created timestamptz;
    v_paid timestamptz;
    v_completed timestamptz;
    v_payment_id uuid;
    v_item text;
    v_item_parts text[];
    v_product_id uuid;
    v_product_name text;
    v_qty int;
    v_price numeric(12,2);
    v_line numeric(12,2);
BEGIN
    SELECT id INTO v_owner FROM users ORDER BY created_at LIMIT 1;

    IF v_owner IS NULL THEN
        RAISE EXCEPTION 'User owner tidak ditemukan. Jalankan seed owner terlebih dahulu.';
    END IF;

    v_today := (now() AT TIME ZONE 'Asia/Jakarta')::date;

    FOR v_seq IN 1..18 LOOP
        v_day := v_seq - 1;
        v_order_number := 'HNR-DEMO-' || lpad(v_seq::text, 4, '0');

        IF EXISTS (SELECT 1 FROM orders WHERE order_number = v_order_number) THEN
            CONTINUE;
        END IF;

        IF v_seq <= 12 THEN
            v_status := 'completed'::order_status;
        ELSIF v_seq <= 14 THEN
            v_status := 'ready'::order_status;
        ELSIF v_seq = 15 THEN
            v_status := 'processing'::order_status;
        ELSIF v_seq = 16 THEN
            v_status := 'pending_confirmation'::order_status;
        ELSIF v_seq = 17 THEN
            v_status := 'pending_payment'::order_status;
        ELSE
            v_status := 'completed'::order_status;
        END IF;

        IF v_status = 'pending_confirmation'::order_status THEN
            v_method := 'cash'::payment_method;
        ELSIF v_status = 'pending_payment'::order_status THEN
            v_method := 'qris'::payment_method;
        ELSIF v_seq % 2 = 1 THEN
            v_method := 'cash'::payment_method;
        ELSE
            v_method := 'qris'::payment_method;
        END IF;

        v_provider := CASE
            WHEN v_method = 'cash'::payment_method THEN 'manual'::payment_provider
            ELSE 'midtrans'::payment_provider
        END;

        v_table_no := CASE (v_seq % 3)
            WHEN 1 THEN '01'
            WHEN 2 THEN '02'
            ELSE '03'
        END;

        CASE (v_seq % 6)
            WHEN 0 THEN
                v_customer := 'Nadia';
                v_items := 'Hanora signature latte|2;Caffe Latte|1';
            WHEN 1 THEN
                v_customer := 'Rizky';
                v_items := 'Cappuccino|1;Croissant Butter|2';
            WHEN 2 THEN
                v_customer := 'Sinta';
                v_items := 'Matcha Latte|1;Pain au Chocolat|1';
            WHEN 3 THEN
                v_customer := 'Budi';
                v_items := 'Chicken Rice Bowl|1;Chocolate|1';
            WHEN 4 THEN
                v_customer := 'Maya';
                v_items := 'Caffe Latte|2;Croissant Butter|1';
            ELSE
                v_customer := 'Dewi';
                v_items := 'Hanora signature latte|1;Matcha Latte|2';
        END CASE;

        v_created := now() - make_interval(days => v_day) - interval '3 hours';

        IF v_status IN ('completed'::order_status, 'ready'::order_status, 'processing'::order_status) THEN
            v_paid := v_created + interval '15 minutes';
        ELSE
            v_paid := NULL;
        END IF;

        IF v_status = 'completed'::order_status THEN
            v_completed := v_created + interval '45 minutes';
        ELSE
            v_completed := NULL;
        END IF;

        v_subtotal := 0;

        FOREACH v_item IN ARRAY string_to_array(v_items, ';') LOOP
            v_item_parts := string_to_array(v_item, '|');
            v_product_name := v_item_parts[1];
            v_qty := v_item_parts[2]::int;

            SELECT price INTO v_price
            FROM products
            WHERE name = v_product_name;

            IF v_price IS NULL THEN
                RAISE EXCEPTION 'Produk tidak ditemukan: %', v_product_name;
            END IF;

            v_subtotal := v_subtotal + (v_price * v_qty);
        END LOOP;

        v_tax := round(v_subtotal * 0.10);
        v_total := v_subtotal + v_tax;
        v_tracking := md5(v_order_number) || md5(v_order_number || 'tracking');

        INSERT INTO orders
            (id, order_number, table_id, cashier_id, customer_name, status,
             subtotal, tax_rate, tax_amount, discount_amount, total, notes,
             tracking_token_hash, idempotency_key, created_at, updated_at,
             paid_at, completed_at)
        VALUES
            (gen_random_uuid(), v_order_number,
             (SELECT id FROM dining_tables WHERE number = v_table_no),
             CASE WHEN v_paid IS NOT NULL THEN v_owner ELSE NULL END,
             v_customer, v_status, v_subtotal, 10.00, v_tax, 0, v_total, NULL,
             v_tracking, 'demo-' || v_order_number, v_created, v_created,
             v_paid, v_completed)
        RETURNING id INTO v_order_id;

        FOREACH v_item IN ARRAY string_to_array(v_items, ';') LOOP
            v_item_parts := string_to_array(v_item, '|');
            v_product_name := v_item_parts[1];
            v_qty := v_item_parts[2]::int;

            SELECT id, price INTO v_product_id, v_price
            FROM products
            WHERE name = v_product_name;

            v_line := v_price * v_qty;

            INSERT INTO order_items
                (order_id, product_id, product_name_snapshot,
                 unit_price_snapshot, quantity, subtotal, created_at)
            VALUES
                (v_order_id, v_product_id, v_product_name,
                 v_price, v_qty, v_line, v_created);
        END LOOP;

        INSERT INTO payments
            (order_id, provider, method, provider_order_id, gross_amount,
             status, paid_at, expires_at, created_at, updated_at)
        VALUES
            (v_order_id, v_provider, v_method, v_order_number || '-P1', v_total,
             CASE WHEN v_paid IS NULL
                  THEN 'pending'::payment_status
                  ELSE 'paid'::payment_status
             END,
             v_paid,
             CASE WHEN v_method = 'qris'::payment_method
                  THEN v_created + interval '15 minutes'
                  ELSE NULL
             END,
             v_created, v_created)
        RETURNING id INTO v_payment_id;

        IF v_paid IS NOT NULL THEN
            INSERT INTO incomes
                (order_id, payment_id, source, description, amount,
                 occurred_at, created_by, created_at, updated_at)
            VALUES
                (v_order_id, v_payment_id, 'order',
                 'Pembayaran ' || v_order_number, v_total,
                 (v_paid AT TIME ZONE 'Asia/Jakarta')::date,
                 v_owner, v_paid, v_paid);
        END IF;

        INSERT INTO transaction_histories
            (order_id, from_status, to_status, actor_type, changed_by, note, created_at)
        VALUES
            (v_order_id, NULL,
             CASE WHEN v_method = 'qris'::payment_method
                  THEN 'pending_payment'::order_status
                  ELSE 'pending_confirmation'::order_status
             END,
             'customer'::actor_type, NULL, 'Pesanan demo dibuat', v_created);

        IF v_paid IS NOT NULL THEN
            INSERT INTO transaction_histories
                (order_id, from_status, to_status, actor_type, changed_by, note, created_at)
            VALUES
                (v_order_id,
                 CASE WHEN v_method = 'qris'::payment_method
                      THEN 'pending_payment'::order_status
                      ELSE 'pending_confirmation'::order_status
                 END,
                 'processing'::order_status, 'system'::actor_type, NULL,
                 'Pembayaran demo diterima', v_paid);
        END IF;

        IF v_completed IS NOT NULL THEN
            INSERT INTO transaction_histories
                (order_id, from_status, to_status, actor_type, changed_by, note, created_at)
            VALUES
                (v_order_id, 'processing'::order_status,
                 'completed'::order_status, 'cashier'::actor_type, v_owner,
                 'Pesanan demo selesai', v_completed);
        END IF;
    END LOOP;

    -- 5. Pengeluaran demo
    INSERT INTO expenses
        (category, description, amount, occurred_at, note, created_by, created_at, updated_at)
    SELECT v.category, v.description, v.amount,
           v_today - v.days, v.note, v_owner, now(), now()
    FROM (VALUES
        ('Bahan baku', 'Pembelian biji kopi', 350000.00, 2, 'Arabica 5 kg'),
        ('Operasional', 'Listrik dan air', 200000.00, 5, 'Tagihan bulan ini'),
        ('Perlengkapan', 'Cup dan lid', 125000.00, 7, 'Stok 1000 pcs'),
        ('SDM', 'Upah part-time', 500000.00, 10, '4 shift'),
        ('Lainnya', 'Perawatan mesin espresso', 150000.00, 14, 'Servis rutin')
    ) AS v(category, description, amount, days, note)
    WHERE NOT EXISTS (
        SELECT 1 FROM expenses e WHERE e.description = v.description
    );

    -- 6. Pemasukan manual demo
    INSERT INTO incomes
        (source, description, amount, occurred_at, created_by, created_at, updated_at)
    SELECT 'manual', 'Penjualan merchandise', 300000.00,
           v_today - 3, v_owner, now(), now()
    WHERE NOT EXISTS (
        SELECT 1
        FROM incomes i
        WHERE i.description = 'Penjualan merchandise'
          AND i.source = 'manual'
    );
END $$;

COMMIT;
