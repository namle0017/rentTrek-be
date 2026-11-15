-- Bơm thiết bị phong phú vào kho hàng loạt cho tất cả các biến thể sản phẩm
INSERT INTO equipment_assets (
    equipment_asset_id,
    product_variant_id,
    asset_code,
    asset_status,
    condition_note,
    created_at,
    updated_at,
    is_deleted
)
SELECT 
    gen_random_uuid(),
    pv.product_variant_id,
    -- Mã thiết bị định dạng: SKU-0001, SKU-0002...
    pv.sku || '-' || lpad(g.n::text, 4, '0'),
    -- Phân bổ trạng thái phong phú để test các tab trong kho
    CASE 
        WHEN g.n IN (1, 2) THEN 'Rented'       -- Đang cho thuê (để tab Đang thuê có số)
        WHEN g.n = 3 THEN 'Maintenance'         -- Đang bảo trì định kỳ
        WHEN g.n = 4 THEN 'Damaged'             -- Hư hỏng (cần sửa)
        ELSE 'Available'                        -- Còn lại là Sẵn sàng trong kho
    END AS asset_status,
    -- Ghi chú tình trạng thiết bị
    CASE 
        WHEN g.n IN (1, 2) THEN 'Đang bàn giao khách thuê, hoạt động tốt'
        WHEN g.n = 3 THEN 'Bảo dưỡng vệ sinh định kỳ'
        WHEN g.n = 4 THEN 'Bị xước nhẹ vải hoặc kẹt khóa, cần kiểm tra'
        ELSE 'Mới 100%, nguyên tem sẵn sàng cấp phát'
    END AS condition_note,
    now() - (g.n || ' days')::interval,
    now(),
    false
FROM product_variants pv
CROSS JOIN generate_series(1, 10) AS g(n)
ON CONFLICT (asset_code) DO NOTHING;
