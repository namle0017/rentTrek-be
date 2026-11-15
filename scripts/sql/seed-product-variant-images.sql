-- 1. Xóa các dữ liệu ảnh cũ
DELETE FROM product_variant_images;

-- 2. Thêm mới 100% ảnh LOCAL từ thư mục public của dự án
INSERT INTO product_variant_images (image_id, product_variant_id, image_url, display_order, is_primary, created_at, updated_at)
SELECT 
    gen_random_uuid(),
    pv.product_variant_id,
    CASE 
        -- Lều & Tăng bạt
        WHEN pv.sku IN ('TENT-EUREKA-2L', 'SD-TENT-CU2-GRN', 'SD-TENT-CU2-GRY', 'SD-TENT-OLD') THEN '/images/discovery/tent.jpg'
        WHEN pv.sku = 'SD-TENT-FAM4' THEN '/images/discovery/tent-family.jpg'
        WHEN pv.sku = 'SD-TARP-34' THEN '/images/discovery/tarp.jpg'

        -- Túi ngủ & Đệm
        WHEN pv.sku IN ('SLEEPBAG-M5', 'SD-SLEEP-M5-M', 'SD-SLEEP-M5-L', 'SD-SLEEP-SUM', 'SD-MAT-AIR') THEN '/images/discovery/sleeping-bag.jpg'

        -- Gậy trekking
        WHEN pv.sku IN ('POLE-TREK', 'SD-POLE-PAIR') THEN '/images/discovery/trekking-poles.jpg'

        -- Balo dã ngoại
        WHEN pv.sku IN ('SD-PACK-40', 'SD-PACK-60-M', 'SD-PACK-60-L', 'SD-PACK-40-RED') THEN '/images/discovery/backpack.jpg'

        -- Đèn pin đội đầu
        WHEN pv.sku = 'SD-LAMP-HEAD' THEN '/images/discovery/headlamp.jpg'

        -- Bếp gas dã ngoại mini
        WHEN pv.sku = 'SD-STOVE-MINI' THEN '/images/discovery/stove.jpg'

        -- Ghế xếp dã ngoại
        WHEN pv.sku = 'SD-CHAIR-FOLD' THEN '/images/discovery/chair.jpg'

        -- Võng dù
        WHEN pv.sku = 'SD-HAMMOCK' THEN '/images/discovery/hammock.jpg'

        -- Bộ sơ cứu y tế
        WHEN pv.sku = 'FIRSTAID-KIT' THEN '/images/discovery/firstaid.jpg'

        ELSE '/images/discovery/tent.jpg'
    END,
    0,
    true,
    now(),
    now()
FROM product_variants pv;
