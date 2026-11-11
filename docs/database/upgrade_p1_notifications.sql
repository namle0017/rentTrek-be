-- Áp dụng cho database PostgreSQL/Supabase hiện có trước khi deploy tính năng thông báo trong ứng dụng.
-- An toàn khi chạy lại nhiều lần (idempotent).
BEGIN;
ALTER TABLE public.notifications ADD COLUMN IF NOT EXISTS event_key varchar(200);
CREATE UNIQUE INDEX IF NOT EXISTS ux_notifications_recipient_event_key
    ON public.notifications (recipient_user_id, event_key) WHERE event_key IS NOT NULL;
COMMIT;
