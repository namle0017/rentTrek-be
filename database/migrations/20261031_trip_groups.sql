BEGIN;

-- 7.1 Cấu hình sức chứa nhóm chuẩn theo phiên bản Tour (nullable cho dữ liệu cũ)
ALTER TABLE public.tours
    ADD COLUMN IF NOT EXISTS group_max_participants integer
        CHECK (group_max_participants IS NULL OR group_max_participants > 0);

-- 7.1 Số lượng group mặc định cho lịch lặp (nullable cho dữ liệu cũ; rule cũ sẽ bị chặn sinh Trip)
ALTER TABLE public.tour_recurrence_rules
    ADD COLUMN IF NOT EXISTS default_group_count integer
        CHECK (default_group_count IS NULL OR default_group_count > 0);

-- 7.4 GuideUserId được phép NULL khi TripGroup còn Draft
ALTER TABLE public.trip_groups
    ALTER COLUMN guide_user_id DROP NOT NULL;

-- 7.3 Cờ đánh dấu nhu cầu đồ cấp chuyến cần tính lại khi đổi số group / participant
ALTER TABLE public.trips
    ADD COLUMN IF NOT EXISTS provided_items_needs_recalc boolean NOT NULL DEFAULT false;

-- 7.5 Unique index cho tên group trong cùng Trip (bỏ qua dòng xóa mềm)
CREATE UNIQUE INDEX IF NOT EXISTS ux_trip_groups_trip_group_name
    ON public.trip_groups (trip_id, lower(btrim(group_name)))
    WHERE NOT is_deleted;

-- 7.1 Chống sinh trùng Trip cho cùng occurrence của lịch lặp
CREATE UNIQUE INDEX IF NOT EXISTS ux_trips_recurrence_start_at
    ON public.trips (recurrence_rule_id, start_at)
    WHERE recurrence_rule_id IS NOT NULL AND NOT is_deleted;

-- 7.1 Cấu hình mặc định GroupOverflowSlots = 2 trong system_configurations
INSERT INTO public.system_configurations (
    configuration_id,
    updated_by_user_id,
    config_key,
    config_value,
    value_type,
    description,
    created_at,
    updated_at,
    is_deleted
)
SELECT
    gen_random_uuid(),
    a.user_id,
    'GroupOverflowSlots',
    '2',
    'Int',
    'Số chỗ vượt tối đa trên mỗi TripGroup khi Staff xếp tay',
    now(),
    now(),
    false
FROM (
    SELECT user_id
    FROM public.accounts
    ORDER BY created_at
    LIMIT 1
) AS a
WHERE NOT EXISTS (
    SELECT 1
    FROM public.system_configurations sc
    WHERE sc.config_key = 'GroupOverflowSlots'
);

COMMIT;
