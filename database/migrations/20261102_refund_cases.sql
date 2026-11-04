BEGIN;

-- Hồ sơ hoàn tiền thủ công (docs/05-MANUAL-REFUND-WORKFLOW.md §5.1)
CREATE TABLE IF NOT EXISTS public.refund_cases (
    refund_case_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    order_id uuid NOT NULL REFERENCES public.orders (order_id),
    refund_payment_id uuid NOT NULL UNIQUE REFERENCES public.payments (payment_id),
    customer_user_id uuid NOT NULL REFERENCES public.accounts (user_id),
    assigned_staff_user_id uuid REFERENCES public.accounts (user_id),
    approved_by_admin_user_id uuid REFERENCES public.accounts (user_id),
    refund_policy_rule_id uuid REFERENCES public.refund_policy_rules (refund_policy_rule_id),
    amount numeric(18, 0) NOT NULL CHECK (amount > 0),
    status varchar(40) NOT NULL,
    bank_code varchar(20) NOT NULL,
    bank_account_last4 char(4) NOT NULL,
    payout_details_ciphertext bytea,
    payout_details_nonce bytea,
    payout_details_tag bytea,
    consent_version varchar(50) NOT NULL,
    consent_accepted_at timestamptz NOT NULL,
    provider_transaction_id varchar(255),
    provider_reference varchar(255),
    assigned_at timestamptz,
    approved_at timestamptz,
    sent_at timestamptz,
    bank_verified_at timestamptz,
    customer_confirmed_at timestamptz,
    disputed_at timestamptz,
    dispute_reason varchar(1000),
    completed_at timestamptz,
    completion_method varchar(40),
    approved_at timestamptz,
    sensitive_data_purged_at timestamptz,
    rejection_reason varchar(1000),
    row_version integer NOT NULL DEFAULT 1 CHECK (row_version >= 1),
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    is_deleted boolean NOT NULL DEFAULT false,
    -- Ba cột mã hóa cùng có giá trị, chỉ cùng null sau khi đã purge
    CONSTRAINT ck_refund_cases_payout_details CHECK (
        (payout_details_ciphertext IS NOT NULL AND payout_details_nonce IS NOT NULL AND payout_details_tag IS NOT NULL)
        OR (payout_details_ciphertext IS NULL AND payout_details_nonce IS NULL AND payout_details_tag IS NULL
            AND sensitive_data_purged_at IS NOT NULL)
    )
);

-- Một Order chỉ có một hồ sơ chưa ở trạng thái cuối
CREATE UNIQUE INDEX IF NOT EXISTS ux_refund_cases_open_order
    ON public.refund_cases (order_id)
    WHERE NOT is_deleted AND status NOT IN ('Completed', 'Rejected', 'Failed', 'Cancelled');

CREATE UNIQUE INDEX IF NOT EXISTS ux_refund_cases_provider_transaction
    ON public.refund_cases (provider_transaction_id)
    WHERE provider_transaction_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_refund_cases_status_created
    ON public.refund_cases (status, created_at DESC)
    WHERE NOT is_deleted;

CREATE INDEX IF NOT EXISTS ix_refund_cases_order
    ON public.refund_cases (order_id);

-- OTP duyệt hoàn tiền, không dùng chung token xác minh email (§5.3)
CREATE TABLE IF NOT EXISTS public.refund_action_challenges (
    challenge_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    refund_case_id uuid NOT NULL REFERENCES public.refund_cases (refund_case_id),
    admin_user_id uuid NOT NULL REFERENCES public.accounts (user_id),
    code_hash varchar(255) NOT NULL,
    expires_at timestamptz NOT NULL,
    attempt_count integer NOT NULL DEFAULT 0,
    used_at timestamptz,
    created_at timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_refund_action_challenges_case_admin
    ON public.refund_action_challenges (refund_case_id, admin_user_id, created_at DESC);

-- FE không được đọc bảng nhạy cảm trực tiếp (§6); role anon/authenticated chỉ có trên Supabase
ALTER TABLE public.refund_cases ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.refund_action_challenges ENABLE ROW LEVEL SECURITY;

DO $$
DECLARE
    role_name text;
BEGIN
    FOREACH role_name IN ARRAY ARRAY['anon', 'authenticated'] LOOP
        IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = role_name) THEN
            EXECUTE format('REVOKE ALL ON public.refund_cases FROM %I', role_name);
            EXECUTE format('REVOKE ALL ON public.refund_action_challenges FROM %I', role_name);
        END IF;
    END LOOP;
END $$;

COMMIT;
