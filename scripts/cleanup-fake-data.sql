-- =====================================================================
-- RenTrek - Xoa DU LIEU GIA do scripts/seed-fake-data.sql tao ra.
--
-- Xoa moi dong co khoa chinh '5eed%' va moi du lieu "moc" vao chung:
--   - Don hang cua tai khoan seed, hoac dung variant / combo / khuyen mai /
--     chinh sach hoan tien seed (ke ca don tao qua API khi test) cung con
--     cua chung (dong don, snapshot, phan bo, phi, thanh toan, KM da ap).
--   - Token dang nhap, thong bao, audit log... cua tai khoan seed.
--   - system_configurations do tai khoan seed cap nhat (app se dung gia tri
--     mac dinh; chay lai scripts/seed-rental-config.sql neu can).
--
-- KHONG xu ly: du lieu tour/trip/post do tai khoan seed tao qua API -> neu
-- co, script dung voi loi khoa ngoai va ROLLBACK toan bo (khong xoa do dang).
--
-- Chay thu cong tren Supabase SQL editor.
-- =====================================================================

BEGIN;

-- Don can xoa
CREATE TEMP TABLE seed_orders ON COMMIT DROP AS
SELECT o.order_id
  FROM orders o
 WHERE o.order_id::text LIKE '5eed%'
    OR o.customer_user_id::text LIKE '5eed%'
    OR o.refund_policy_id::text LIKE '5eed%'
    OR EXISTS (SELECT 1 FROM rental_order_items i
                WHERE i.rental_order_id = o.order_id
                  AND (i.product_variant_id::text LIKE '5eed%' OR i.bundle_id::text LIKE '5eed%'))
    OR EXISTS (SELECT 1 FROM applied_promotions ap
                WHERE ap.order_id = o.order_id AND ap.promotion_tier_id::text LIKE '5eed%');

CREATE TEMP TABLE seed_items ON COMMIT DROP AS
SELECT i.rental_order_item_id FROM rental_order_items i JOIN seed_orders s ON s.order_id = i.rental_order_id;

-- Con cua don
DELETE FROM asset_assignments
 WHERE rental_order_item_id IN (SELECT rental_order_item_id FROM seed_items)
    OR equipment_asset_id::text LIKE '5eed%'
    OR asset_assignment_id::text LIKE '5eed%';

DELETE FROM applied_promotions
 WHERE order_id IN (SELECT order_id FROM seed_orders)
    OR promotion_tier_id::text LIKE '5eed%';

DELETE FROM rental_bundle_item_snapshots
 WHERE rental_order_item_id IN (SELECT rental_order_item_id FROM seed_items)
    OR product_variant_id::text LIKE '5eed%';

DELETE FROM rental_charges  WHERE rental_order_id IN (SELECT order_id FROM seed_orders);
DELETE FROM payments        WHERE order_id IN (SELECT order_id FROM seed_orders);
DELETE FROM rental_order_items WHERE rental_order_item_id IN (SELECT rental_order_item_id FROM seed_items);
DELETE FROM rental_orders   WHERE order_id IN (SELECT order_id FROM seed_orders);
DELETE FROM orders          WHERE order_id IN (SELECT order_id FROM seed_orders);

-- Khuyen mai
DELETE FROM promotion_tiers WHERE promotion_id::text LIKE '5eed%' OR gift_product_variant_id::text LIKE '5eed%';
DELETE FROM promotions      WHERE promotion_id::text LIKE '5eed%';

-- Catalog
DELETE FROM rental_price_tiers     WHERE price_tier_id::text LIKE '5eed%'
                                      OR product_variant_id::text LIKE '5eed%'
                                      OR bundle_id::text LIKE '5eed%';
DELETE FROM product_bundle_items   WHERE bundle_id::text LIKE '5eed%' OR product_variant_id::text LIKE '5eed%';
DELETE FROM product_bundles        WHERE bundle_id::text LIKE '5eed%';
DELETE FROM product_variant_images WHERE product_variant_id::text LIKE '5eed%';
DELETE FROM equipment_assets       WHERE product_variant_id::text LIKE '5eed%';
DELETE FROM product_variants       WHERE product_id::text LIKE '5eed%';
DELETE FROM products               WHERE product_id::text LIKE '5eed%';
DELETE FROM categories             WHERE category_id::text LIKE '5eed%';

-- Chinh sach hoan tien
DELETE FROM refund_policy_rules WHERE refund_policy_id::text LIKE '5eed%';
DELETE FROM refund_policies     WHERE refund_policy_id::text LIKE '5eed%';

-- Tai khoan seed
DELETE FROM system_configurations WHERE updated_by_user_id::text LIKE '5eed%';
DELETE FROM access_tokens         WHERE user_id::text LIKE '5eed%';
DELETE FROM refresh_tokens        WHERE user_id::text LIKE '5eed%';
DELETE FROM password_reset_tokens WHERE user_id::text LIKE '5eed%';
DELETE FROM user_external_logins  WHERE user_id::text LIKE '5eed%';
DELETE FROM notifications         WHERE recipient_user_id::text LIKE '5eed%';
DELETE FROM audit_logs            WHERE actor_user_id::text LIKE '5eed%';
DELETE FROM profiles              WHERE user_id::text LIKE '5eed%';
DELETE FROM accounts              WHERE user_id::text LIKE '5eed%';

COMMIT;
