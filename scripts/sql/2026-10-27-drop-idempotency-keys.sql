-- =====================================================================
-- RenTrek - Migration: xóa bảng idempotency_keys
-- Ngày: 2026-10-27
-- Lý do: bảng `idempotency_keys` không còn được sử dụng. Idempotency của
--        đơn thuê (POST /rental-orders) dùng cột `orders.idempotency_key`
--        giống cách trip booking đang làm (cùng user + cùng key + cùng
--        payload -> trả lại đơn cũ; khác payload -> 409
--        IDEMPOTENCY_KEY_REUSED), nên không cần bảng riêng.
-- Ghi chú:
--   - Script có thể chạy lại nhiều lần (DROP TABLE IF EXISTS).
--   - Cấu hình `idempotency.ttlHours` (nếu đã seed) không còn được đọc;
--     có thể xóa thủ công khỏi system_configurations.
-- Chạy thủ công trên Supabase SQL editor.
-- =====================================================================

BEGIN;

DROP TABLE IF EXISTS idempotency_keys;

COMMIT;

-- =====================================================================
-- ROLLBACK (chạy thủ công nếu cần tạo lại bảng)
-- =====================================================================
-- BEGIN;
--
-- CREATE TABLE IF NOT EXISTS idempotency_keys (
--     idempotency_key_id   uuid PRIMARY KEY DEFAULT gen_random_uuid(),
--     user_id              uuid          NOT NULL REFERENCES accounts(user_id),
--     idempotency_key      varchar(100)  NOT NULL,
--     endpoint             varchar(100)  NOT NULL,   -- vd: 'POST /rental-orders'
--     request_hash         varchar(64)   NOT NULL,   -- SHA-256 hex của request body đã canonical hóa
--     status               varchar(20)   NOT NULL,   -- InProgress | Completed
--     response_status_code int,
--     response_body        jsonb,
--     resource_id          uuid,                     -- vd: id đơn hàng đã tạo
--     created_at           timestamptz   NOT NULL DEFAULT now(),
--     expires_at           timestamptz   NOT NULL,
--     CONSTRAINT ux_idempotency_keys_user_endpoint_key UNIQUE (user_id, endpoint, idempotency_key)
-- );
--
-- CREATE INDEX IF NOT EXISTS ix_idempotency_keys_expires_at ON idempotency_keys (expires_at);
--
-- ALTER TABLE public.idempotency_keys ENABLE ROW LEVEL SECURITY;
--
-- COMMIT;
