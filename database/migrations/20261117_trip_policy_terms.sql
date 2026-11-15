BEGIN;

-- Điều khoản đổi lịch / NoShow gắn với chính sách hoàn tiền loại Tour (plan 2026-11-15, Phase 1).
ALTER TABLE public.refund_policies
    ADD COLUMN IF NOT EXISTS reschedule_min_hours_before integer NULL,
    ADD COLUMN IF NOT EXISTS reschedule_fee numeric(12,2) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS max_reschedules integer NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS no_show_grace_minutes integer NOT NULL DEFAULT 30;

ALTER TABLE public.orders
    ADD COLUMN IF NOT EXISTS accepted_refund_policy_version integer NULL;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conrelid = 'public.refund_policies'::regclass AND conname = 'ck_refund_policies_trip_terms'
    ) THEN
        ALTER TABLE public.refund_policies ADD CONSTRAINT ck_refund_policies_trip_terms CHECK (
            (reschedule_min_hours_before IS NULL OR reschedule_min_hours_before BETWEEN 0 AND 8760)
            AND reschedule_fee >= 0
            AND max_reschedules BETWEEN 0 AND 10
            AND no_show_grace_minutes BETWEEN 0 AND 1440
        );
    END IF;
END $$;

COMMIT;
