-- Phase 2: tách bước duyệt hồ sơ hoàn tiền khỏi bước xuất Excel + giới hạn khiếu nại.
BEGIN;

ALTER TABLE refund_cases
    ADD COLUMN IF NOT EXISTS exported_at timestamptz NULL,
    ADD COLUMN IF NOT EXISTS rejected_at timestamptz NULL,
    ADD COLUMN IF NOT EXISTS resubmit_count int NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS appeal_reason varchar(1000) NULL;

-- Hồ sơ Approved trước migration đã được xuất (luồng cũ: xuất = duyệt).
UPDATE refund_cases SET exported_at = approved_at
WHERE status = 'Approved' AND exported_at IS NULL AND approved_at IS NOT NULL;

UPDATE refund_cases SET rejected_at = updated_at
WHERE status = 'Rejected' AND rejected_at IS NULL;

COMMIT;
