BEGIN;

-- Cột hiển thị theo Figma cho tour catalog
ALTER TABLE public.tours
    ADD COLUMN IF NOT EXISTS difficulty_level varchar(50) NOT NULL DEFAULT 'Trung bình',
    ADD COLUMN IF NOT EXISTS equipment_deposit numeric(18,0) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS inspection_rate integer NOT NULL DEFAULT 0;

ALTER TABLE public.tour_activities
    ADD COLUMN IF NOT EXISTS day_title varchar(255);

ALTER TABLE public.tour_provided_items
    ADD COLUMN IF NOT EXISTS item_name varchar(255) NOT NULL DEFAULT '';

-- Role mặc định; register cần role Customer
INSERT INTO public.roles (role_id, role_name, description, created_at, updated_at, is_deleted)
SELECT gen_random_uuid(), v.role_name, v.description, now(), now(), false
FROM (VALUES
    ('Admin', 'Quản trị viên toàn quyền hệ thống'),
    ('Staff', 'Nhân viên vận hành, quản lý đơn thuê và tour'),
    ('Customer', 'Khách hàng thuê thiết bị và đăng ký tour'),
    ('TourGuide', 'Hướng dẫn viên dẫn đoàn tour trekking')
) AS v(role_name, description)
WHERE NOT EXISTS (SELECT 1 FROM public.roles r WHERE r.role_name = v.role_name);

COMMIT;
