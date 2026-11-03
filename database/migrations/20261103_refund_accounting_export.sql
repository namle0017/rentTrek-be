BEGIN;

-- Luồng mới: Admin xuất Excel cho kế toán (docs/05-MANUAL-REFUND-WORKFLOW.md §4).
-- Hồ sơ đang ở bước Staff quay về chờ xuất; hồ sơ đã chuyển tiền chờ Admin xác nhận hoàn tất.
UPDATE public.refund_cases
SET status = 'Requested', updated_at = now()
WHERE status IN ('Assigned', 'PendingApproval');

UPDATE public.refund_cases
SET status = 'Approved', approved_at = COALESCE(approved_at, updated_at), updated_at = now()
WHERE status IN ('Sent', 'Disputed', 'ManualReview');

-- Không thể hoàn tác: lý do khiếu nại và mốc thời gian cũ mất hẳn; audit_logs vẫn giữ lịch sử trạng thái
ALTER TABLE public.refund_cases
    DROP COLUMN assigned_staff_user_id,
    DROP COLUMN assigned_at,
    DROP COLUMN sent_at,
    DROP COLUMN bank_verified_at,
    DROP COLUMN customer_confirmed_at,
    DROP COLUMN disputed_at,
    DROP COLUMN dispute_reason,
    DROP COLUMN completion_method,
    DROP COLUMN provider_reference;

-- OTP giờ gắn với lần xuất, không gắn với từng hồ sơ
DELETE FROM public.refund_action_challenges;
DROP INDEX IF EXISTS public.ix_refund_action_challenges_case_admin;
ALTER TABLE public.refund_action_challenges DROP COLUMN refund_case_id;
CREATE INDEX ix_refund_action_challenges_admin
    ON public.refund_action_challenges (admin_user_id, created_at DESC);

COMMIT;
