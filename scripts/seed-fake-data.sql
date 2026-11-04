-- =====================================================================
-- RenTrek - Seed DU LIEU GIA de test (tai khoan, catalog thue, combo,
-- khuyen mai, chinh sach hoan tien, don thue o moi trang thai).
--
-- Chay thu cong tren Supabase SQL editor (chay ca file mot lan).
--
-- Quy uoc:
--   - Moi khoa chinh deu co dang 5eedTTTT-0000-4000-8000-NNNNNNNNNNNN
--     (TTTT = ma bang, NNNN = so thu tu) -> de nhan biet va de xoa bang
--     scripts/cleanup-fake-data.sql.
--   - Email: *@seed.rentrek.test, SKU: SD-*, ma don: RT-SEED-*,
--     ma khuyen mai: SEED*.
--   - Mat khau moi tai khoan: Test@123 (BCrypt EnhancedHashPassword,
--     work factor 12 - giong PasswordHasher cua app).
--   - Thoi gian tinh tuong doi theo ngay chay script (UTC). Muon "lam moi"
--     lich thue: chay cleanup-fake-data.sql roi chay lai file nay.
--   - Idempotent: INSERT ... ON CONFLICT DO NOTHING -> chay lai khong loi,
--     khong nhan doi.
--
-- Tai khoan:
--   seed.admin@seed.rentrek.test      Admin
--   seed.staff1/2@seed.rentrek.test   Staff
--   seed.guide@seed.rentrek.test      Guide
--   seed.c1 .. seed.c6@seed.rentrek.test  Customer
--   seed.locked@seed.rentrek.test     Customer, status Inactive (test dang nhap bi chan)
--
-- Tinh huong test dang chu y:
--   - SD-TENT-FAM4 chi con 1 tai san cho thue duoc va da bi don RT-SEED-0004
--     giu tu ngay +10 den +15 -> trong luc hold con hieu luc (~2h), dat trung
--     khoang do se RESOURCE_UNAVAILABLE.
--   - SD-CHAIR-FOLD chi co 2 tai san -> "Combo gia dinh 4 nguoi" (2 ghe)
--     het hang ngay +20 do RT-SEED-0010 dang giu ca 2 ghe.
--   - SD-TARP-34 khong co bac gia; SD-CHAIR-FOLD chi co gia 1-3 ngay
--     -> thue > 3 ngay se PRICE_TIER_NOT_FOUND.
--   - SD-HAMMOCK (san pham Inactive), SD-PACK-40-RED (variant Inactive),
--     SD-TENT-OLD (da xoa mem) -> khong duoc hien / khong dat duoc.
--   - Khuyen mai: SEED10, SEEDLONG, SEEDQTY, SEEDTIER (nhieu bac),
--     SEEDONCE (max_uses = 1, da dung het), SEEDOLD (het han),
--     SEEDOFF (Inactive).
--   - RT-SEED-0004: PendingPayment, hold con ~2 gio -> test thanh toan.
--   - RT-SEED-0010: PendingPayment nhung hold da het -> test HOLD_EXPIRED.
-- =====================================================================

BEGIN;

-- ---------------------------------------------------------------------
-- Ham tien ich (chi ton tai trong phien hien tai)
-- ---------------------------------------------------------------------
CREATE OR REPLACE FUNCTION pg_temp.sid(t int, n bigint) RETURNS uuid
LANGUAGE sql IMMUTABLE AS $$
    SELECT format('5eed%s-0000-4000-8000-%s', lpad(to_hex(t), 4, '0'), lpad(n::text, 12, '0'))::uuid
$$;

-- Moc thoi gian: 00:00 UTC hom nay + so ngay + so gio (01:00 UTC = 08:00 gio VN)
CREATE OR REPLACE FUNCTION pg_temp.d(days int, hours int DEFAULT 0) RETURNS timestamptz
LANGUAGE sql STABLE AS $$
    SELECT date_trunc('day', now(), 'UTC') + make_interval(days => days, hours => hours)
$$;

-- Tai san: variant so v, ban so i
CREATE OR REPLACE FUNCTION pg_temp.asset(v int, i int) RETURNS uuid
LANGUAGE sql IMMUTABLE AS $$
    SELECT pg_temp.sid(7, v * 100 + i)
$$;

-- ---------------------------------------------------------------------
-- 1. Roles + tai khoan + profile
-- ---------------------------------------------------------------------
INSERT INTO roles (role_name, description) VALUES
    ('Admin',    'Quản trị viên toàn quyền hệ thống'),
    ('Staff',    'Nhân viên vận hành kho / đơn thuê'),
    ('Customer', 'Khách hàng sử dụng hệ thống'),
    ('Guide',    'Hướng dẫn viên tour')
ON CONFLICT (role_name) DO NOTHING;

INSERT INTO accounts (user_id, role_id, email, password_hash, full_name, phone, status, email_confirmed_at, created_at, updated_at)
SELECT pg_temp.sid(1, a.n), r.role_id, a.email,
       '$2a$12$F37eBjsDJ53nFQqA72i.g.DZwTtLFharxmMqvYmm1pbfihIn1XX9G',
       a.full_name, a.phone, a.status, pg_temp.d(-60), pg_temp.d(-60), pg_temp.d(-60)
FROM (VALUES
    ( 1, 'Admin',    'seed.admin@seed.rentrek.test',  'Seed Admin',          '0900000001', 'Active'),
    ( 2, 'Staff',    'seed.staff1@seed.rentrek.test', 'Trần Minh Kho',       '0900000002', 'Active'),
    ( 3, 'Staff',    'seed.staff2@seed.rentrek.test', 'Lê Thu Vận',          '0900000003', 'Active'),
    ( 4, 'Guide',    'seed.guide@seed.rentrek.test',  'Phạm Văn Dẫn',        '0900000004', 'Active'),
    (11, 'Customer', 'seed.c1@seed.rentrek.test',     'Nguyễn Văn An',       '0911000001', 'Active'),
    (12, 'Customer', 'seed.c2@seed.rentrek.test',     'Trần Thị Bình',       '0911000002', 'Active'),
    (13, 'Customer', 'seed.c3@seed.rentrek.test',     'Lê Hoàng Cường',      '0911000003', 'Active'),
    (14, 'Customer', 'seed.c4@seed.rentrek.test',     'Phạm Ngọc Dung',      '0911000004', 'Active'),
    (15, 'Customer', 'seed.c5@seed.rentrek.test',     'Hoàng Minh Em',       '0911000005', 'Active'),
    (16, 'Customer', 'seed.c6@seed.rentrek.test',     'Võ Thị Phương',       '0911000006', 'Active'),
    (17, 'Customer', 'seed.locked@seed.rentrek.test', 'Tài Khoản Bị Khoá',   '0911000007', 'Inactive')
) AS a(n, role_name, email, full_name, phone, status)
JOIN roles r ON r.role_name = a.role_name AND r.is_deleted = false
ON CONFLICT DO NOTHING;

INSERT INTO profiles (profile_id, user_id, address, date_of_birth, bio, user_code)
SELECT pg_temp.sid(2, p.n), pg_temp.sid(1, p.n), p.address, p.dob::date, p.bio, p.user_code
FROM (VALUES
    ( 1, 'Quận 1, TP. Hồ Chí Minh',     '1990-01-01', 'Tài khoản admin dữ liệu giả', 'SEED-ADMIN'),
    ( 2, 'Quận 7, TP. Hồ Chí Minh',     '1995-03-12', NULL,                          'SEED-STAFF1'),
    ( 3, 'Thủ Đức, TP. Hồ Chí Minh',    '1997-07-21', NULL,                          'SEED-STAFF2'),
    ( 4, 'Sa Pa, Lào Cai',              '1992-11-05', 'Dẫn đoàn Fansipan 8 năm',     'SEED-GUIDE'),
    (11, 'Cầu Giấy, Hà Nội',            '1998-02-14', 'Thích cắm trại cuối tuần',    'SEED-C1'),
    (12, 'Hải Châu, Đà Nẵng',           '2000-06-30', NULL,                          'SEED-C2'),
    (13, 'Ninh Kiều, Cần Thơ',          '1996-09-09', NULL,                          'SEED-C3'),
    (14, 'Đà Lạt, Lâm Đồng',            '1999-12-01', NULL,                          'SEED-C4'),
    (15, 'Bình Thạnh, TP. Hồ Chí Minh', '2001-04-18', NULL,                          'SEED-C5'),
    (16, 'Nha Trang, Khánh Hoà',        '1994-08-25', NULL,                          'SEED-C6'),
    (17, NULL,                          NULL,         NULL,                          'SEED-LOCKED')
) AS p(n, address, dob, bio, user_code)
ON CONFLICT DO NOTHING;

-- ---------------------------------------------------------------------
-- 2. Cau hinh thue (chi them neu chua co; uu tien Admin that neu da co)
-- ---------------------------------------------------------------------
INSERT INTO system_configurations (updated_by_user_id, config_key, config_value, value_type, description)
SELECT COALESCE(
           (SELECT a.user_id FROM accounts a JOIN roles r ON r.role_id = a.role_id
             WHERE r.role_name = 'Admin' AND a.is_deleted = false AND a.status = 'Active'
               AND a.user_id::text NOT LIKE '5eed%'
             ORDER BY a.created_at LIMIT 1),
           pg_temp.sid(1, 1)),
       c.config_key, c.config_value, 'Int', c.description
FROM (VALUES
    ('rental.holdMinutes',         '15', 'Số phút giữ chỗ (hold) khi tạo đơn thuê trước khi thanh toán'),
    ('rental.cleaningBufferHours', '2',  'Số giờ vệ sinh/đệm giữa hai lượt thuê liên tiếp của cùng một tài sản')
) AS c(config_key, config_value, description)
ON CONFLICT (config_key) DO NOTHING;

-- ---------------------------------------------------------------------
-- 3. Danh muc
-- ---------------------------------------------------------------------
INSERT INTO categories (category_id, name, description, status, category_type) VALUES
    (pg_temp.sid(3, 1), '[Seed] Lều & tăng',         'Lều cắm trại, tăng che mưa',            'Active',   'PRODUCT'),
    (pg_temp.sid(3, 2), '[Seed] Túi ngủ & đệm',      'Túi ngủ, đệm hơi giữ ấm',               'Active',   'PRODUCT'),
    (pg_temp.sid(3, 3), '[Seed] Balô',               'Balô trekking các dung tích',            'Active',   'PRODUCT'),
    (pg_temp.sid(3, 4), '[Seed] Đèn & nấu ăn',       'Đèn đội đầu, bếp dã ngoại',              'Active',   'PRODUCT'),
    (pg_temp.sid(3, 5), '[Seed] Phụ kiện trekking',  'Gậy, ghế, võng',                         'Active',   'PRODUCT'),
    (pg_temp.sid(3, 6), '[Seed] Combo cắm trại',     'Combo thiết bị thuê theo gói',           'Active',   'PRODUCT'),
    (pg_temp.sid(3, 7), '[Seed] Đồ câu cá',          'Danh mục ngừng kinh doanh (test filter)', 'Inactive', 'PRODUCT')
ON CONFLICT DO NOTHING;

-- ---------------------------------------------------------------------
-- 4. San pham + variant + anh
-- ---------------------------------------------------------------------
INSERT INTO products (product_id, category_id, name, description, status, created_by_user_id, is_deleted, created_at) VALUES
    (pg_temp.sid(4,  1), pg_temp.sid(3, 1), 'Lều Cloud Up 2 người',     'Lều 2 lớp siêu nhẹ 1.8kg, chống mưa 3000mm, dựng trong 5 phút.', 'Active',   pg_temp.sid(1, 1), false, pg_temp.d(-50)),
    (pg_temp.sid(4,  2), pg_temp.sid(3, 1), 'Lều gia đình 4 người',     'Lều rộng có tiền sảnh, phù hợp nhóm 4 người hoặc gia đình.',      'Active',   pg_temp.sid(1, 1), false, pg_temp.d(-50)),
    (pg_temp.sid(4,  3), pg_temp.sid(3, 1), 'Tăng che mưa 3x4m',        'Tăng dù chống thấm, kèm dây và cọc. (Chưa có bảng giá)',          'Active',   pg_temp.sid(1, 1), false, pg_temp.d(-49)),
    (pg_temp.sid(4,  4), pg_temp.sid(3, 2), 'Túi ngủ -5°C',             'Túi ngủ lông vũ tổng hợp, nhiệt độ thoải mái 0°C, giới hạn -5°C.', 'Active',   pg_temp.sid(1, 1), false, pg_temp.d(-48)),
    (pg_temp.sid(4,  5), pg_temp.sid(3, 2), 'Túi ngủ mùa hè',           'Túi ngủ mỏng nhẹ cho thời tiết trên 15°C.',                        'Active',   pg_temp.sid(1, 1), false, pg_temp.d(-48)),
    (pg_temp.sid(4,  6), pg_temp.sid(3, 2), 'Đệm hơi tự bơm',           'Đệm cách nhiệt R-value 3.5, tự bơm hơi.',                          'Active',   pg_temp.sid(1, 1), false, pg_temp.d(-47)),
    (pg_temp.sid(4,  7), pg_temp.sid(3, 3), 'Balô trekking 40L',        'Balô trợ lực có đai hông, áo mưa đi kèm.',                         'Active',   pg_temp.sid(1, 1), false, pg_temp.d(-46)),
    (pg_temp.sid(4,  8), pg_temp.sid(3, 3), 'Balô trekking 60L',        'Balô cho hành trình dài ngày, khung nhôm.',                        'Active',   pg_temp.sid(1, 1), false, pg_temp.d(-46)),
    (pg_temp.sid(4,  9), pg_temp.sid(3, 4), 'Đèn đội đầu 400 lumen',    'Đèn đội đầu sạc USB-C, 3 chế độ sáng.',                            'Active',   pg_temp.sid(1, 1), false, pg_temp.d(-45)),
    (pg_temp.sid(4, 10), pg_temp.sid(3, 4), 'Bếp gas mini',             'Bếp gas du lịch kèm 1 bình gas 230g.',                             'Active',   pg_temp.sid(1, 1), false, pg_temp.d(-45)),
    (pg_temp.sid(4, 11), pg_temp.sid(3, 5), 'Gậy trekking (cặp)',       'Cặp gậy carbon gấp 3 khúc, đầu cao su.',                           'Active',   pg_temp.sid(1, 1), false, pg_temp.d(-44)),
    (pg_temp.sid(4, 12), pg_temp.sid(3, 5), 'Ghế xếp dã ngoại',         'Ghế xếp khung nhôm, tải trọng 120kg. (Chỉ có giá 1-3 ngày)',       'Active',   pg_temp.sid(1, 1), false, pg_temp.d(-44)),
    (pg_temp.sid(4, 13), pg_temp.sid(3, 5), 'Võng dù',                  'Sản phẩm tạm ngừng cho thuê (test Inactive).',                     'Inactive', pg_temp.sid(1, 1), false, pg_temp.d(-43)),
    (pg_temp.sid(4, 14), pg_temp.sid(3, 1), 'Lều cũ thanh lý',          'Sản phẩm đã xoá mềm (test is_deleted).',                           'Active',   pg_temp.sid(1, 1), true,  pg_temp.d(-90))
ON CONFLICT DO NOTHING;

INSERT INTO product_variants (product_variant_id, product_id, sku, variant_name, status, size, color) VALUES
    (pg_temp.sid(5,  1), pg_temp.sid(4,  1), 'SD-TENT-CU2-GRN', 'Cloud Up 2 / Xanh lá',  'Active',   '2 người',  'Xanh lá'),
    (pg_temp.sid(5,  2), pg_temp.sid(4,  1), 'SD-TENT-CU2-GRY', 'Cloud Up 2 / Xám',      'Active',   '2 người',  'Xám'),
    (pg_temp.sid(5,  3), pg_temp.sid(4,  2), 'SD-TENT-FAM4',    'Gia đình 4 người',      'Active',   '4 người',  'Cam đất'),
    (pg_temp.sid(5,  4), pg_temp.sid(4,  3), 'SD-TARP-34',      'Tăng 3x4m',             'Active',   '3x4m',     'Xanh rêu'),
    (pg_temp.sid(5,  5), pg_temp.sid(4,  4), 'SD-SLEEP-M5-M',   'Túi ngủ -5°C / M',      'Active',   'M',        'Đỏ'),
    (pg_temp.sid(5,  6), pg_temp.sid(4,  4), 'SD-SLEEP-M5-L',   'Túi ngủ -5°C / L',      'Active',   'L',        'Đỏ'),
    (pg_temp.sid(5,  7), pg_temp.sid(4,  5), 'SD-SLEEP-SUM',    'Túi ngủ mùa hè',        'Active',   'Tiêu chuẩn', 'Xanh dương'),
    (pg_temp.sid(5,  8), pg_temp.sid(4,  6), 'SD-MAT-AIR',      'Đệm hơi 1 người',       'Active',   '185x55cm', 'Cam'),
    (pg_temp.sid(5,  9), pg_temp.sid(4,  7), 'SD-PACK-40',      'Balô 40L / Xanh',       'Active',   '40L',      'Xanh lá'),
    (pg_temp.sid(5, 10), pg_temp.sid(4,  8), 'SD-PACK-60-M',    'Balô 60L / size M',     'Active',   'M',        'Đen'),
    (pg_temp.sid(5, 11), pg_temp.sid(4,  8), 'SD-PACK-60-L',    'Balô 60L / size L',     'Active',   'L',        'Đen'),
    (pg_temp.sid(5, 12), pg_temp.sid(4,  9), 'SD-LAMP-HEAD',    'Đèn đội đầu',           'Active',   NULL,       'Đen'),
    (pg_temp.sid(5, 13), pg_temp.sid(4, 10), 'SD-STOVE-MINI',   'Bếp gas mini + bình',   'Active',   NULL,       NULL),
    (pg_temp.sid(5, 14), pg_temp.sid(4, 11), 'SD-POLE-PAIR',    'Gậy carbon (cặp)',      'Active',   '110-135cm', 'Xám'),
    (pg_temp.sid(5, 15), pg_temp.sid(4, 12), 'SD-CHAIR-FOLD',   'Ghế xếp',               'Active',   NULL,       'Xanh rêu'),
    (pg_temp.sid(5, 16), pg_temp.sid(4, 13), 'SD-HAMMOCK',      'Võng dù 1 người',       'Active',   NULL,       'Cam'),
    (pg_temp.sid(5, 17), pg_temp.sid(4, 14), 'SD-TENT-OLD',     'Lều cũ',                'Active',   '2 người',  'Xanh'),
    (pg_temp.sid(5, 18), pg_temp.sid(4,  7), 'SD-PACK-40-RED',  'Balô 40L / Đỏ (ngừng)', 'Inactive', '40L',      'Đỏ')
ON CONFLICT DO NOTHING;

-- Anh chinh cho moi variant + anh phu cho Cloud Up xanh
INSERT INTO product_variant_images (image_id, product_variant_id, image_url, display_order, is_primary)
SELECT pg_temp.sid(6, g.n), pv.product_variant_id,
       'https://placehold.co/800x600/png?text=' || pv.sku, 0, true
FROM generate_series(1, 18) AS g(n)
JOIN product_variants pv ON pv.product_variant_id = pg_temp.sid(5, g.n)
ON CONFLICT DO NOTHING;

INSERT INTO product_variant_images (image_id, product_variant_id, image_url, display_order, is_primary) VALUES
    (pg_temp.sid(6, 101), pg_temp.sid(5, 1), 'https://placehold.co/800x600/png?text=SD-TENT-CU2-GRN+inside', 1, false),
    (pg_temp.sid(6, 102), pg_temp.sid(5, 1), 'https://placehold.co/800x600/png?text=SD-TENT-CU2-GRN+packed', 2, false)
ON CONFLICT DO NOTHING;

-- ---------------------------------------------------------------------
-- 5. Bac gia thue theo variant (VND / ngay)
-- ---------------------------------------------------------------------
INSERT INTO rental_price_tiers (price_tier_id, product_variant_id, min_days, max_days, daily_price)
SELECT pg_temp.sid(10, t.n), pg_temp.sid(5, t.v), t.min_days, t.max_days, t.price
FROM (VALUES
    ( 1,  1, 1, 2,    120000), ( 2,  1, 3, 5,    100000), ( 3,  1, 6, NULL,  85000),
    ( 4,  2, 1, 2,    120000), ( 5,  2, 3, 5,    100000), ( 6,  2, 6, NULL,  85000),
    ( 7,  3, 1, 2,    200000), ( 8,  3, 3, 5,    170000), ( 9,  3, 6, NULL, 150000),
    -- v4 SD-TARP-34: co y khong co bac gia
    (10,  5, 1, 2,     50000), (11,  5, 3, NULL,  40000),
    (12,  6, 1, 2,     50000), (13,  6, 3, NULL,  40000),
    (14,  7, 1, NULL,  30000),
    (15,  8, 1, 2,     40000), (16,  8, 3, NULL,  30000),
    (17,  9, 1, 2,     60000), (18,  9, 3, NULL,  50000),
    (19, 10, 1, 2,     85000), (20, 10, 3, NULL,  70000),
    (21, 11, 1, 2,     85000), (22, 11, 3, NULL,  70000),
    (23, 12, 1, NULL,  20000),
    (24, 13, 1, 2,     45000), (25, 13, 3, NULL,  35000),
    (26, 14, 1, NULL,  25000),
    (27, 15, 1, 3,     30000),               -- SD-CHAIR-FOLD: > 3 ngay khong co gia
    (28, 16, 1, NULL,  25000),
    (29, 17, 1, NULL,  50000),
    (30, 18, 1, NULL,  60000)
) AS t(n, v, min_days, max_days, price)
ON CONFLICT DO NOTHING;

-- ---------------------------------------------------------------------
-- 6. Tai san (tung mon do vat ly)
-- ---------------------------------------------------------------------
INSERT INTO equipment_assets (equipment_asset_id, product_variant_id, asset_code, asset_status, condition_note)
SELECT pg_temp.asset(a.v, i), pg_temp.sid(5, a.v),
       a.sku || '-' || lpad(i::text, 3, '0'), 'Available', NULL
FROM (VALUES
    ( 1, 'SD-TENT-CU2-GRN', 4), ( 2, 'SD-TENT-CU2-GRY', 2), ( 3, 'SD-TENT-FAM4',  2),
    ( 4, 'SD-TARP-34',      2), ( 5, 'SD-SLEEP-M5-M',   5), ( 6, 'SD-SLEEP-M5-L', 4),
    ( 7, 'SD-SLEEP-SUM',    3), ( 8, 'SD-MAT-AIR',      4), ( 9, 'SD-PACK-40',    3),
    (10, 'SD-PACK-60-M',    2), (11, 'SD-PACK-60-L',    2), (12, 'SD-LAMP-HEAD',  6),
    (13, 'SD-STOVE-MINI',   3), (14, 'SD-POLE-PAIR',    5), (15, 'SD-CHAIR-FOLD', 2),
    (16, 'SD-HAMMOCK',      1), (17, 'SD-TENT-OLD',     1), (18, 'SD-PACK-40-RED', 1)
) AS a(v, sku, qty)
CROSS JOIN LATERAL generate_series(1, a.qty) AS i
ON CONFLICT DO NOTHING;

-- Trang thai dac biet (chi Available / Good duoc tinh la cho thue duoc)
UPDATE equipment_assets e
   SET asset_status = s.status, condition_note = s.note
  FROM (VALUES
    (pg_temp.asset( 1, 1), 'Good',        'Đã qua sử dụng, còn tốt'),
    (pg_temp.asset( 9, 1), 'Good',        'Trầy nhẹ ở đáy balô'),
    (pg_temp.asset( 3, 2), 'Maintenance', 'Gãy 1 khung, đang sửa'),
    (pg_temp.asset(11, 2), 'Damaged',     'Rách quai đeo vai sau đơn RT-SEED-0007'),
    (pg_temp.asset(12, 6), 'Retired',     'Hỏng pin, thanh lý'),
    (pg_temp.asset( 5, 5), 'Lost',        'Khách báo mất'),
    (pg_temp.asset(17, 1), 'Retired',     NULL)
  ) AS s(id, status, note)
 WHERE e.equipment_asset_id = s.id
   AND e.asset_status = 'Available';

-- ---------------------------------------------------------------------
-- 7. Combo (bundle) + thanh phan + bac gia combo
-- ---------------------------------------------------------------------
INSERT INTO product_bundles (bundle_id, category_id, name, description, status) VALUES
    (pg_temp.sid(8, 1), pg_temp.sid(3, 6), 'Combo cắm trại 2 người',  'Lều Cloud Up xanh + 2 túi ngủ -5°C (M) + 2 đệm hơi + 1 đèn đội đầu', 'Active'),
    (pg_temp.sid(8, 2), pg_temp.sid(3, 6), 'Combo trekking cá nhân',  'Balô 40L + gậy trekking + đèn đội đầu + túi ngủ mùa hè',             'Active'),
    (pg_temp.sid(8, 3), pg_temp.sid(3, 6), 'Combo gia đình 4 người',  'Lều 4 người + 4 túi ngủ -5°C (L) + bếp gas + 2 ghế xếp',             'Active'),
    (pg_temp.sid(8, 4), pg_temp.sid(3, 6), 'Combo cũ (ngừng bán)',    'Combo Inactive để test filter',                                       'Inactive')
ON CONFLICT DO NOTHING;

INSERT INTO product_bundle_items (bundle_item_id, bundle_id, product_variant_id, quantity)
SELECT pg_temp.sid(9, b.n), pg_temp.sid(8, b.bundle), pg_temp.sid(5, b.v), b.qty
FROM (VALUES
    ( 1, 1,  1, 1), ( 2, 1,  5, 2), ( 3, 1,  8, 2), ( 4, 1, 12, 1),
    ( 5, 2,  9, 1), ( 6, 2, 14, 1), ( 7, 2, 12, 1), ( 8, 2,  7, 1),
    ( 9, 3,  3, 1), (10, 3,  6, 4), (11, 3, 13, 1), (12, 3, 15, 2),
    (13, 4,  2, 1), (14, 4,  7, 2)
) AS b(n, bundle, v, qty)
ON CONFLICT DO NOTHING;

INSERT INTO rental_price_tiers (price_tier_id, bundle_id, min_days, max_days, daily_price)
SELECT pg_temp.sid(10, t.n), pg_temp.sid(8, t.bundle), t.min_days, t.max_days, t.price
FROM (VALUES
    (101, 1, 1, 2,    250000), (102, 1, 3, NULL, 210000),
    (103, 2, 1, NULL, 120000),
    (104, 3, 1, 3,    400000), (105, 3, 4, NULL, 350000),
    (106, 4, 1, NULL, 150000)
) AS t(n, bundle, min_days, max_days, price)
ON CONFLICT DO NOTHING;

-- ---------------------------------------------------------------------
-- 8. Chinh sach hoan tien don thue
-- ---------------------------------------------------------------------
INSERT INTO refund_policies (refund_policy_id, created_by_user_id, policy_type, name, description, status,
                             policy_family_id, version, effective_from)
VALUES (pg_temp.sid(11, 1), pg_temp.sid(1, 1), 'Rental', '[Seed] Chính sách hoàn tiền thuê đồ',
        'Huỷ trước ≥ 72h: hoàn 100%; 24h - 72h: hoàn 50%; < 24h: không hoàn', 'Active',
        pg_temp.sid(11, 100), 1, pg_temp.d(-60))
ON CONFLICT DO NOTHING;

INSERT INTO refund_policy_rules (refund_policy_rule_id, refund_policy_id, min_hours_before, refund_percentage) VALUES
    (pg_temp.sid(12, 1), pg_temp.sid(11, 1), 72, 100),
    (pg_temp.sid(12, 2), pg_temp.sid(11, 1), 24,  50),
    (pg_temp.sid(12, 3), pg_temp.sid(11, 1),  0,   0)
ON CONFLICT DO NOTHING;

-- ---------------------------------------------------------------------
-- 9. Khuyen mai
-- ---------------------------------------------------------------------
INSERT INTO promotions (promotion_id, created_by_user_id, name, description, start_at, end_at, status,
                        applies_to, application_scope, promotion_code, max_uses, max_uses_per_user, is_stackable, priority)
VALUES
    (pg_temp.sid(13, 1), pg_temp.sid(1, 1), 'Giảm 10% đơn thuê từ 500k',  'Áp dụng đơn thuê có tạm tính ≥ 500.000đ',      pg_temp.d(-30), pg_temp.d(60),  'Active',   'Rental', 'Order', 'SEED10',   NULL, 3,    false, 10),
    (pg_temp.sid(13, 2), pg_temp.sid(1, 1), 'Thuê dài ngày giảm 50k',     'Thuê từ 5 ngày trở lên giảm 50.000đ',          pg_temp.d(-60), pg_temp.d(90),  'Active',   'Rental', 'Order', 'SEEDLONG', NULL, NULL, false, 5),
    (pg_temp.sid(13, 3), pg_temp.sid(1, 1), 'Thuê từ 3 món giảm 5%',      'Tổng số lượng thiết bị ≥ 3',                    pg_temp.d(-10), pg_temp.d(30),  'Active',   'Rental', 'Order', 'SEEDQTY',  NULL, NULL, false, 3),
    (pg_temp.sid(13, 4), pg_temp.sid(1, 1), 'Khuyến mãi hè (đã hết hạn)', 'Test PROMOTION_INVALID do hết hạn',             pg_temp.d(-90), pg_temp.d(-10), 'Active',   'Rental', 'Order', 'SEEDOLD',  NULL, NULL, false, 1),
    (pg_temp.sid(13, 5), pg_temp.sid(1, 1), 'Giảm 30k một lần duy nhất',  'max_uses = 1 và đã được dùng (RT-SEED-0003)', pg_temp.d(-30), pg_temp.d(30),  'Active',   'Rental', 'Order', 'SEEDONCE', 1,    NULL, false, 1),
    (pg_temp.sid(13, 6), pg_temp.sid(1, 1), 'Giảm theo bậc tổng tiền',    '≥300k: 5% · ≥1tr: 10% · ≥2tr: 15%',            pg_temp.d(-30), pg_temp.d(60),  'Active',   'Rental', 'Order', 'SEEDTIER', NULL, NULL, false, 8),
    (pg_temp.sid(13, 7), pg_temp.sid(1, 1), 'Khuyến mãi đang tắt',        'Test status Inactive',                          pg_temp.d(-30), pg_temp.d(30),  'Inactive', 'Rental', 'Order', 'SEEDOFF',  NULL, NULL, false, 1)
ON CONFLICT DO NOTHING;

INSERT INTO promotion_tiers (promotion_tier_id, promotion_id, min_threshold, discount_type, discount_value, qualification_metric)
SELECT pg_temp.sid(14, t.n), pg_temp.sid(13, t.promo), t.min_threshold, t.discount_type, t.discount_value, t.metric
FROM (VALUES
    (1, 1,  500000, 'Percent', 10,    'Amount'),
    (2, 2,       5, 'Fixed',   50000, 'Days'),
    (3, 3,       3, 'Percent', 5,     'Quantity'),
    (4, 4,       0, 'Percent', 20,    'Amount'),
    (5, 5,       0, 'Fixed',   30000, 'Amount'),
    (6, 6,  300000, 'Percent', 5,     'Amount'),
    (7, 6, 1000000, 'Percent', 10,    'Amount'),
    (8, 6, 2000000, 'Percent', 15,    'Amount'),
    (9, 7,       0, 'Percent', 50,    'Amount')
) AS t(n, promo, min_threshold, discount_type, discount_value, metric)
ON CONFLICT DO NOTHING;

-- ---------------------------------------------------------------------
-- 10. Don thue
--   line_amount = quantity x billable_days x daily_price
--   total = subtotal - discount
-- ---------------------------------------------------------------------
INSERT INTO orders (order_id, customer_user_id, order_type, total_amount, status, created_at, updated_at, order_code, note,
                    subtotal_amount, discount_amount, payment_status, hold_expires_at, cancelled_at, cancel_reason, refund_policy_id)
SELECT pg_temp.sid(15, o.n), pg_temp.sid(1, o.customer), 'Rental', o.subtotal - o.discount, o.status,
       o.created_at, COALESCE(o.cancelled_at, o.created_at), 'RT-SEED-' || lpad(o.n::text, 4, '0'), o.note,
       o.subtotal, o.discount, o.payment_status, o.hold_expires_at, o.cancelled_at, o.cancel_reason, pg_temp.sid(11, 1)
FROM (VALUES
    ( 1, 11, 'Completed',      'Paid',              540000::numeric,  54000::numeric, pg_temp.d(-22, 3),                      NULL::timestamptz,                       NULL::timestamptz, NULL,                          'Nhận đồ: tại cửa hàng'),
    ( 2, 12, 'InFulfillment',  'Paid',              630000,           0,              pg_temp.d(-3, 4),                       NULL,                                    NULL,              NULL,                          'Nhận đồ: tại cửa hàng'),
    ( 3, 13, 'Confirmed',      'Paid',              300000,           30000,          pg_temp.d(-1, 2),                       NULL,                                    NULL,              NULL,                          NULL),
    ( 4, 14, 'PendingPayment', 'Unpaid',            1025000,          102500,         now(),                                  now() + interval '2 hours',              NULL,              NULL,                          'Đơn đang chờ thanh toán (hold ~2h)'),
    ( 5, 15, 'Cancelled',      'Refunded',          240000,           0,              pg_temp.d(-4, 2),                       NULL,                                    pg_temp.d(-2, 3),  'Khách đổi lịch trình',        NULL),
    ( 6, 16, 'Expired',        'Unpaid',            240000,           0,              pg_temp.d(-3, 5),                       pg_temp.d(-3, 5) + interval '15 minutes', NULL,             NULL,                          NULL),
    ( 7, 11, 'Completed',      'Paid',              1085000,          50000,          pg_temp.d(-42, 3),                      NULL,                                    NULL,              NULL,                          'Thuê đi Tà Năng - Phan Dũng'),
    ( 8, 12, 'Cancelled',      'PartiallyRefunded', 120000,           0,              pg_temp.d(-2, 1),                       NULL,                                    pg_temp.d(-1, 1),  'Trời mưa bão, huỷ chuyến',    NULL),
    ( 9, 13, 'Completed',      'Paid',              480000,           24000,          pg_temp.d(-12, 2),                      NULL,                                    NULL,              NULL,                          NULL),
    (10, 16, 'PendingPayment', 'Unpaid',            60000,            0,              now() - interval '20 minutes',          now() - interval '5 minutes',            NULL,              NULL,                          'Hold đã hết hạn nhưng chưa bị job chuyển Expired')
) AS o(n, customer, status, payment_status, subtotal, discount, created_at, hold_expires_at, cancelled_at, cancel_reason, note)
ON CONFLICT DO NOTHING;

INSERT INTO rental_orders (order_id, handled_by_user_id, rental_start_at, rental_end_at, deposit_amount)
SELECT pg_temp.sid(15, r.n), CASE WHEN r.staff IS NULL THEN NULL ELSE pg_temp.sid(1, r.staff) END, r.start_at, r.end_at, 0
FROM (VALUES
    ( 1, 2,             pg_temp.d(-20, 1), pg_temp.d(-17, 1)),   -- 3 ngay
    ( 2, 2,             pg_temp.d( -1, 1), pg_temp.d(  2, 1)),   -- 3 ngay
    ( 3, 3,             pg_temp.d(  5, 1), pg_temp.d(  7, 1)),   -- 2 ngay
    ( 4, NULL::int,     pg_temp.d( 10, 1), pg_temp.d( 15, 1)),   -- 5 ngay
    ( 5, NULL,          pg_temp.d(  3, 1), pg_temp.d(  6, 1)),   -- 3 ngay
    ( 6, NULL,          pg_temp.d(  1, 1), pg_temp.d(  3, 1)),   -- 2 ngay
    ( 7, 2,             pg_temp.d(-40, 1), pg_temp.d(-33, 1)),   -- 7 ngay
    ( 8, NULL,          pg_temp.d(  1, 1), pg_temp.d(  3, 1)),   -- 2 ngay
    ( 9, 3,             pg_temp.d(-10, 1), pg_temp.d( -8, 1)),   -- 2 ngay
    (10, NULL,          pg_temp.d( 20, 1), pg_temp.d( 21, 1))    -- 1 ngay
) AS r(n, staff, start_at, end_at)
ON CONFLICT DO NOTHING;

-- Dong don: variant (v) hoac combo (bundle)
INSERT INTO rental_order_items (rental_order_item_id, rental_order_id, product_variant_id, bundle_id, quantity, daily_price, billable_days, line_amount, created_at)
SELECT pg_temp.sid(16, i.n), pg_temp.sid(15, i.ord),
       CASE WHEN i.v IS NULL THEN NULL ELSE pg_temp.sid(5, i.v) END,
       CASE WHEN i.bundle IS NULL THEN NULL ELSE pg_temp.sid(8, i.bundle) END,
       i.qty, i.price, i.days, i.qty * i.days * i.price, o.created_at
FROM (VALUES
    ( 1,  1,    1, NULL::int, 1, 100000, 3),   -- 300.000
    ( 2,  1,    5, NULL,      2,  40000, 3),   -- 240.000
    ( 3,  2, NULL, 1,         1, 210000, 3),   -- 630.000 (combo 2 nguoi)
    ( 4,  3,   10, NULL,      1,  85000, 2),   -- 170.000
    ( 5,  3,   14, NULL,      1,  25000, 2),   --  50.000
    ( 6,  3,   12, NULL,      2,  20000, 2),   --  80.000
    ( 7,  4,    3, NULL,      1, 170000, 5),   -- 850.000
    ( 8,  4,   13, NULL,      1,  35000, 5),   -- 175.000
    ( 9,  5,    9, NULL,      1,  50000, 3),   -- 150.000
    (10,  5,    7, NULL,      1,  30000, 3),   --  90.000
    (11,  6,    2, NULL,      1, 120000, 2),   -- 240.000
    (12,  7,   11, NULL,      1,  70000, 7),   -- 490.000
    (13,  7,    2, NULL,      1,  85000, 7),   -- 595.000
    (14,  8,   12, NULL,      3,  20000, 2),   -- 120.000
    (15,  9, NULL, 2,         2, 120000, 2),   -- 480.000 (2 combo trekking)
    (16, 10,   15, NULL,      2,  30000, 1)    --  60.000
) AS i(n, ord, v, bundle, qty, price, days)
JOIN orders o ON o.order_id = pg_temp.sid(15, i.ord)
ON CONFLICT DO NOTHING;

-- Snapshot thanh phan combo tai thoi diem dat
INSERT INTO rental_bundle_item_snapshots (rental_bundle_item_snapshot_id, rental_order_item_id, product_variant_id, quantity_per_bundle)
SELECT pg_temp.sid(17, s.n), pg_temp.sid(16, s.item), pg_temp.sid(5, s.v), s.qty
FROM (VALUES
    (1, 3,  1, 1), (2, 3,  5, 2), (3, 3,  8, 2), (4, 3, 12, 1),
    (5, 15, 9, 1), (6, 15, 14, 1), (7, 15, 12, 1), (8, 15, 7, 1)
) AS s(n, item, v, qty)
ON CONFLICT DO NOTHING;

-- Khuyen mai da ap dung
INSERT INTO applied_promotions (applied_promotion_id, promotion_tier_id, order_id, discount_amount, status, applied_at, created_at)
SELECT pg_temp.sid(18, a.n), pg_temp.sid(14, a.tier), pg_temp.sid(15, a.ord), a.discount, a.status, o.created_at, o.created_at
FROM (VALUES
    (1, 1, 1,  54000, 'Applied'),    -- SEED10
    (2, 5, 3,  30000, 'Applied'),    -- SEEDONCE (het luot)
    (3, 1, 4, 102500, 'Reserved'),   -- SEED10, don chua thanh toan
    (4, 2, 7,  50000, 'Applied'),    -- SEEDLONG
    (5, 6, 9,  24000, 'Applied')     -- SEEDTIER bac >= 300k
) AS a(n, tier, ord, discount, status)
JOIN orders o ON o.order_id = pg_temp.sid(15, a.ord)
ON CONFLICT DO NOTHING;

-- Phan bo tai san cho dong don (reserved_from/until = ky thue)
INSERT INTO asset_assignments (asset_assignment_id, rental_order_item_id, equipment_asset_id, assigned_at, returned_at,
                               return_condition, status, reserved_from, reserved_until, created_at)
SELECT pg_temp.sid(19, a.n), pg_temp.sid(16, a.item), pg_temp.asset(a.v, a.i),
       CASE WHEN a.status IN ('Issued', 'Returned') THEN r.rental_start_at ELSE o.created_at END,
       CASE WHEN a.status = 'Returned' THEN r.rental_end_at - interval '1 hour' END,
       a.return_condition, a.status, r.rental_start_at, r.rental_end_at, o.created_at
FROM (VALUES
    -- RT-SEED-0001 (Completed)
    ( 1,  1,  1, 1, 'Returned',  'Normal'),
    ( 2,  2,  5, 1, 'Returned',  'Normal'),
    ( 3,  2,  5, 2, 'Returned',  'Normal'),
    -- RT-SEED-0002 (InFulfillment, combo 2 nguoi)
    ( 4,  3,  1, 2, 'Issued',    NULL),
    ( 5,  3,  5, 3, 'Issued',    NULL),
    ( 6,  3,  5, 4, 'Issued',    NULL),
    ( 7,  3,  8, 1, 'Issued',    NULL),
    ( 8,  3,  8, 2, 'Issued',    NULL),
    ( 9,  3, 12, 1, 'Issued',    NULL),
    -- RT-SEED-0003 (Confirmed)
    (10,  4, 10, 1, 'Reserved',  NULL),
    (11,  5, 14, 1, 'Reserved',  NULL),
    (12,  6, 12, 2, 'Reserved',  NULL),
    (13,  6, 12, 3, 'Reserved',  NULL),
    -- RT-SEED-0004 (PendingPayment) - giu tai san FAM4 duy nhat con cho thue duoc
    (14,  7,  3, 1, 'Reserved',  NULL),
    (15,  8, 13, 1, 'Reserved',  NULL),
    -- RT-SEED-0005 (Cancelled)
    (16,  9,  9, 1, 'Cancelled', NULL),
    (17, 10,  7, 1, 'Cancelled', NULL),
    -- RT-SEED-0006 (Expired)
    (18, 11,  2, 1, 'Cancelled', NULL),
    -- RT-SEED-0007 (Completed, co hu hong)
    (19, 12, 11, 2, 'Returned',  'Damaged'),
    (20, 13,  2, 2, 'Returned',  'Normal'),
    -- RT-SEED-0008 (Cancelled)
    (21, 14, 12, 4, 'Cancelled', NULL),
    (22, 14, 12, 5, 'Cancelled', NULL),
    (23, 14, 12, 2, 'Cancelled', NULL),
    -- RT-SEED-0009 (Completed, 2 combo trekking)
    (24, 15,  9, 2, 'Returned',  'Normal'),
    (25, 15,  9, 3, 'Returned',  'Normal'),
    (26, 15, 14, 2, 'Returned',  'Normal'),
    (27, 15, 14, 3, 'Returned',  'Normal'),
    (28, 15, 12, 1, 'Returned',  'Normal'),
    (29, 15, 12, 2, 'Returned',  'MissingAccessory'),
    (30, 15,  7, 2, 'Returned',  'Normal'),
    (31, 15,  7, 3, 'Returned',  'Normal'),
    -- RT-SEED-0010 (PendingPayment, hold het han)
    (32, 16, 15, 1, 'Reserved',  NULL),
    (33, 16, 15, 2, 'Reserved',  NULL)
) AS a(n, item, v, i, status, return_condition)
JOIN rental_order_items it ON it.rental_order_item_id = pg_temp.sid(16, a.item)
JOIN rental_orders r ON r.order_id = it.rental_order_id
JOIN orders o ON o.order_id = r.order_id
ON CONFLICT DO NOTHING;

-- Phi phat sinh
INSERT INTO rental_charges (rental_charge_id, rental_order_id, equipment_asset_id, charge_type, amount, reason, status, created_by_user_id, created_at)
VALUES
    (pg_temp.sid(21, 1), pg_temp.sid(15, 7), pg_temp.asset(11, 2), 'Damage', 150000, 'Rách quai đeo vai balô 60L', 'Settled', pg_temp.sid(1, 2), pg_temp.d(-33, 3)),
    (pg_temp.sid(21, 2), pg_temp.sid(15, 9), pg_temp.asset(12, 2), 'Other',   20000, 'Thiếu dây sạc đèn đội đầu', 'Assessed', pg_temp.sid(1, 3), pg_temp.d(-8, 3)),
    (pg_temp.sid(21, 3), pg_temp.sid(15, 2), NULL,                 'Other',   50000, 'Nháp: kiểm tra túi đựng lều khi trả (khách không thấy)', 'Draft', pg_temp.sid(1, 2), pg_temp.d(-1, 2))
ON CONFLICT DO NOTHING;

-- Thanh toan / hoan tien
INSERT INTO payments (payment_id, order_id, original_payment_id, payment_type, amount, payment_method, provider_transaction_id,
                      status, created_at, processed_at, refund_policy_rule_id, purpose)
SELECT pg_temp.sid(20, p.n), pg_temp.sid(15, p.ord),
       CASE WHEN p.original IS NULL THEN NULL ELSE pg_temp.sid(20, p.original) END,
       p.payment_type, p.amount, 'BankTransfer', 'SEED-TXN-' || lpad(p.n::text, 4, '0'),
       'Succeeded', p.at, p.at + interval '5 minutes',
       CASE WHEN p.rule IS NULL THEN NULL ELSE pg_temp.sid(12, p.rule) END,
       p.purpose
FROM (VALUES
    ( 1,  1, NULL::int, 'Collection',  486000, pg_temp.d(-22, 3) + interval '10 minutes', NULL::int, 'RentalFee'),
    ( 2,  2, NULL,      'Collection',  630000, pg_temp.d(-3, 4)  + interval '10 minutes', NULL,      'RentalFee'),
    ( 3,  3, NULL,      'Collection',  270000, pg_temp.d(-1, 2)  + interval '10 minutes', NULL,      'RentalFee'),
    ( 4,  5, NULL,      'Collection',  240000, pg_temp.d(-4, 2)  + interval '10 minutes', NULL,      'RentalFee'),
    ( 5,  5, 4,         'Refund',      240000, pg_temp.d(-2, 4),                          1,         'RentalFee'),   -- huy >= 72h: 100%
    ( 6,  7, NULL,      'Collection', 1035000, pg_temp.d(-42, 3) + interval '10 minutes', NULL,      'RentalFee'),
    ( 7,  7, NULL,      'Collection',  150000, pg_temp.d(-33, 5),                         NULL,      'AdditionalCharge'),
    ( 8,  8, NULL,      'Collection',  120000, pg_temp.d(-2, 1)  + interval '10 minutes', NULL,      'RentalFee'),
    ( 9,  8, 8,         'Refund',       60000, pg_temp.d(-1, 2),                          2,         'RentalFee'),   -- huy 48h truoc: 50%
    (10,  9, NULL,      'Collection',  456000, pg_temp.d(-12, 2) + interval '10 minutes', NULL,      'RentalFee')
) AS p(n, ord, original, payment_type, amount, at, rule, purpose)
ORDER BY p.n
ON CONFLICT DO NOTHING;

COMMIT;

-- ---------------------------------------------------------------------
-- Tong ket
-- ---------------------------------------------------------------------
SELECT 'accounts' AS tbl, count(*) FROM accounts WHERE user_id::text LIKE '5eed%'
UNION ALL SELECT 'product_variants',   count(*) FROM product_variants   WHERE product_variant_id::text LIKE '5eed%'
UNION ALL SELECT 'equipment_assets',   count(*) FROM equipment_assets   WHERE equipment_asset_id::text LIKE '5eed%'
UNION ALL SELECT 'product_bundles',    count(*) FROM product_bundles    WHERE bundle_id::text LIKE '5eed%'
UNION ALL SELECT 'rental_price_tiers', count(*) FROM rental_price_tiers WHERE price_tier_id::text LIKE '5eed%'
UNION ALL SELECT 'promotions',         count(*) FROM promotions         WHERE promotion_id::text LIKE '5eed%'
UNION ALL SELECT 'orders',             count(*) FROM orders             WHERE order_id::text LIKE '5eed%'
UNION ALL SELECT 'asset_assignments',  count(*) FROM asset_assignments  WHERE asset_assignment_id::text LIKE '5eed%'
UNION ALL SELECT 'payments',           count(*) FROM payments           WHERE payment_id::text LIKE '5eed%';
