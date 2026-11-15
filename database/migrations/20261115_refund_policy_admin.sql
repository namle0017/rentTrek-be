BEGIN;

-- Màn quản lý chính sách hoàn tiền (docs/03-API-CONTRACT.md §20).
-- Chuẩn hoá policy_type / status về tập giá trị cố định, thêm CHECK và index phục vụ lọc danh sách.
-- Idempotent: chạy lại nhiều lần không đổi kết quả.

-- ---------------------------------------------------------------------
-- 1. Chuẩn hoá dữ liệu cũ ('TOUR', 'TripBooking', 'active', ...)
-- ---------------------------------------------------------------------
UPDATE public.refund_policies
SET policy_type = CASE
        WHEN upper(btrim(policy_type)) = 'RENTAL' THEN 'Rental'
        ELSE 'Tour' -- TOUR | Tour | TripBooking | giá trị lạ đều là chính sách tour
    END
WHERE policy_type NOT IN ('Tour', 'Rental');

UPDATE public.refund_policies
SET status = CASE
        WHEN lower(btrim(status)) = 'active' THEN 'Active'
        WHEN lower(btrim(status)) = 'draft' THEN 'Draft'
        ELSE 'Archived'
    END
WHERE status NOT IN ('Draft', 'Active', 'Archived');

-- ---------------------------------------------------------------------
-- 2. CHECK constraint
-- ---------------------------------------------------------------------
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_refund_policies_policy_type') THEN
        ALTER TABLE public.refund_policies
            ADD CONSTRAINT ck_refund_policies_policy_type CHECK (policy_type IN ('Tour', 'Rental'));
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_refund_policies_status') THEN
        ALTER TABLE public.refund_policies
            ADD CONSTRAINT ck_refund_policies_status CHECK (status IN ('Draft', 'Active', 'Archived'));
    END IF;
END $$;

-- ---------------------------------------------------------------------
-- 3. Index lọc danh sách / tìm chính sách hiện hành
-- ---------------------------------------------------------------------
CREATE INDEX IF NOT EXISTS ix_refund_policies_type_status
    ON public.refund_policies (policy_type, status)
    WHERE NOT is_deleted;

COMMIT;
