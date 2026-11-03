BEGIN;

-- Link mời nhóm tính bằng HMAC từ order id + version; đổi link = tăng version
ALTER TABLE public.trip_booking_orders
    ADD COLUMN IF NOT EXISTS invite_version integer NOT NULL DEFAULT 0;

COMMIT;
