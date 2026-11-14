BEGIN;

-- Luồng thuê đồ có cọc (docs/03.1-API-CONTRACT-RENTAL-CUSTOMER.md, mục Cọc & hoàn tiền).
-- Cọc = deposit_percent × subtotal (trước giảm giá); khách trả một lần tiền thuê + cọc.
-- Mọi % phạt tính trên tiền cọc. Policy có version, đơn snapshot policy lúc đặt.

-- ---------------------------------------------------------------------
-- 1. Chính sách thuê đồ
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS public.rental_policies (
    rental_policy_id               uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    policy_family_id               uuid          NOT NULL,
    version                        integer       NOT NULL DEFAULT 1 CHECK (version >= 1),
    name                           varchar(255)  NOT NULL,
    status                         varchar(30)   NOT NULL, -- Draft | Active | Archived
    deposit_percent                numeric(5,2)  NOT NULL CHECK (deposit_percent BETWEEN 0 AND 100),
    free_cancel_limit              integer       NOT NULL DEFAULT 2 CHECK (free_cancel_limit >= 0),
    free_cancel_window_days        integer       NOT NULL DEFAULT 30 CHECK (free_cancel_window_days > 0),
    over_limit_cancel_fee_percent  numeric(5,2)  NOT NULL DEFAULT 5 CHECK (over_limit_cancel_fee_percent BETWEEN 0 AND 100),
    return_grace_hours             integer       NOT NULL DEFAULT 2 CHECK (return_grace_hours >= 0),
    extension_max_days_late        integer       NOT NULL DEFAULT 2 CHECK (extension_max_days_late >= 0),
    suspected_loss_after_days      integer       NOT NULL DEFAULT 7 CHECK (suspected_loss_after_days > 0),
    receipt_auto_confirm_hours     integer       NOT NULL DEFAULT 24 CHECK (receipt_auto_confirm_hours > 0),
    min_photos_per_asset           integer       NOT NULL DEFAULT 1 CHECK (min_photos_per_asset >= 0),
    effective_from                 timestamptz   NOT NULL,
    effective_until                timestamptz,
    created_by_user_id             uuid REFERENCES public.accounts (user_id),
    created_at                     timestamptz   NOT NULL DEFAULT now(),
    updated_at                     timestamptz   NOT NULL DEFAULT now(),
    is_deleted                     boolean       NOT NULL DEFAULT false,
    CONSTRAINT ux_rental_policies_family_version UNIQUE (policy_family_id, version)
);

-- Chỉ một policy Active tại một thời điểm
CREATE UNIQUE INDEX IF NOT EXISTS ux_rental_policies_single_active
    ON public.rental_policies ((true))
    WHERE status = 'Active' AND NOT is_deleted;

CREATE TABLE IF NOT EXISTS public.rental_policy_tiers (
    rental_policy_tier_id  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    rental_policy_id       uuid         NOT NULL REFERENCES public.rental_policies (rental_policy_id),
    scope                  varchar(30)  NOT NULL CHECK (scope IN ('PickupLate', 'ReturnLate')),
    min_days_late          integer      NOT NULL CHECK (min_days_late > 0),
    percent_of_deposit     numeric(5,2) NOT NULL CHECK (percent_of_deposit BETWEEN 0 AND 100),
    is_terminal            boolean      NOT NULL DEFAULT false,
    created_at             timestamptz  NOT NULL DEFAULT now(),
    CONSTRAINT ux_rental_policy_tiers_scope_days UNIQUE (rental_policy_id, scope, min_days_late)
);

-- ---------------------------------------------------------------------
-- 2. Giá trị thiết bị (đền bù mất / hư không dùng lại được)
-- ---------------------------------------------------------------------
ALTER TABLE public.product_variants
    ADD COLUMN IF NOT EXISTS replacement_value numeric(18,0) CHECK (replacement_value IS NULL OR replacement_value >= 0);
ALTER TABLE public.product_bundles
    ADD COLUMN IF NOT EXISTS replacement_value numeric(18,0) CHECK (replacement_value IS NULL OR replacement_value >= 0);
ALTER TABLE public.rental_order_items
    ADD COLUMN IF NOT EXISTS unit_replacement_value numeric(18,0) NOT NULL DEFAULT 0 CHECK (unit_replacement_value >= 0);

-- ---------------------------------------------------------------------
-- 3. Đơn thuê: snapshot policy + mốc giao nhận + loại huỷ
-- ---------------------------------------------------------------------
ALTER TABLE public.rental_orders
    ADD COLUMN IF NOT EXISTS rental_policy_id uuid REFERENCES public.rental_policies (rental_policy_id),
    ADD COLUMN IF NOT EXISTS handed_over_at timestamptz,
    ADD COLUMN IF NOT EXISTS customer_received_confirmed_at timestamptz,
    ADD COLUMN IF NOT EXISTS receipt_confirm_source varchar(20), -- Customer | Auto
    ADD COLUMN IF NOT EXISTS cancellation_type varchar(30);      -- Unpaid | FreeCancel | LateCancel | NoShow | Admin

CREATE INDEX IF NOT EXISTS ix_rental_orders_rental_policy_id ON public.rental_orders (rental_policy_id);
CREATE INDEX IF NOT EXISTS ix_rental_orders_cancellation_type ON public.rental_orders (cancellation_type)
    WHERE cancellation_type IS NOT NULL;

-- Phí do hệ thống (job) tạo không có người tạo; mỗi loại phí trễ chỉ nâng bậc
ALTER TABLE public.rental_charges
    ALTER COLUMN created_by_user_id DROP NOT NULL,
    ADD COLUMN IF NOT EXISTS tier_min_days integer;

CREATE UNIQUE INDEX IF NOT EXISTS ux_rental_charges_one_tier_charge
    ON public.rental_charges (rental_order_id, charge_type)
    WHERE charge_type IN ('LatePickup', 'LateReturnPenalty', 'CancellationFee') AND status <> 'Voided';

-- Gia hạn phải thanh toán xong mới áp dụng
ALTER TABLE public.rental_order_adjustments
    ADD COLUMN IF NOT EXISTS status varchar(20) NOT NULL DEFAULT 'Applied', -- Pending | Applied | Cancelled
    ADD COLUMN IF NOT EXISTS payment_id uuid REFERENCES public.payments (payment_id),
    ADD COLUMN IF NOT EXISTS applied_at timestamptz;

CREATE INDEX IF NOT EXISTS ix_rental_order_adjustments_payment_id ON public.rental_order_adjustments (payment_id);

-- ---------------------------------------------------------------------
-- 4. Ảnh bằng chứng giao / nhận / hư hỏng
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS public.rental_evidence_photos (
    rental_evidence_photo_id  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    rental_order_id           uuid         NOT NULL REFERENCES public.rental_orders (order_id),
    asset_assignment_id       uuid REFERENCES public.asset_assignments (asset_assignment_id),
    stage                     varchar(30)  NOT NULL CHECK (stage IN ('Handover', 'Return', 'Damage', 'CustomerIssue')),
    image_url                 text         NOT NULL,
    note                      varchar(500),
    uploaded_by_user_id       uuid         NOT NULL REFERENCES public.accounts (user_id),
    created_at                timestamptz  NOT NULL DEFAULT now(),
    is_deleted                boolean      NOT NULL DEFAULT false
);

CREATE INDEX IF NOT EXISTS ix_rental_evidence_photos_order_stage
    ON public.rental_evidence_photos (rental_order_id, stage) WHERE NOT is_deleted;
CREATE INDEX IF NOT EXISTS ix_rental_evidence_photos_assignment
    ON public.rental_evidence_photos (asset_assignment_id);

-- ---------------------------------------------------------------------
-- 5. Report cho admin (no-show, kiểm tra trả đồ, nghi mất, thu thêm)
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS public.rental_reports (
    rental_report_id     uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    rental_order_id      uuid          NOT NULL REFERENCES public.rental_orders (order_id),
    report_type          varchar(30)   NOT NULL CHECK (report_type IN ('NoShow', 'LateCancellation', 'ReturnInspection', 'SuspectedLoss', 'ExtraPayment')),
    status               varchar(30)   NOT NULL CHECK (status IN ('Submitted', 'Approved', 'Rejected', 'Resolved')),
    amount               numeric(18,0) NOT NULL DEFAULT 0 CHECK (amount >= 0),
    note                 varchar(2000),
    review_note          varchar(1000),
    created_by_user_id   uuid REFERENCES public.accounts (user_id), -- null = hệ thống
    reviewed_by_user_id  uuid REFERENCES public.accounts (user_id),
    reviewed_at          timestamptz,
    row_version          integer       NOT NULL DEFAULT 1 CHECK (row_version >= 1),
    created_at           timestamptz   NOT NULL DEFAULT now(),
    updated_at           timestamptz   NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_rental_reports_status_created ON public.rental_reports (status, created_at DESC);
CREATE INDEX IF NOT EXISTS ix_rental_reports_order ON public.rental_reports (rental_order_id);
-- Một đơn chỉ có một report đang mở cho mỗi loại
CREATE UNIQUE INDEX IF NOT EXISTS ux_rental_reports_open_type
    ON public.rental_reports (rental_order_id, report_type)
    WHERE status = 'Submitted';

-- ---------------------------------------------------------------------
-- 6. Quyết toán cọc (1-1 với đơn thuê)
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS public.rental_settlements (
    rental_order_id    uuid PRIMARY KEY REFERENCES public.rental_orders (order_id),
    rental_fee_paid    numeric(18,0) NOT NULL CHECK (rental_fee_paid >= 0),
    deposit_held       numeric(18,0) NOT NULL CHECK (deposit_held >= 0),
    total_charges      numeric(18,0) NOT NULL CHECK (total_charges >= 0),
    rental_fee_refund  numeric(18,0) NOT NULL DEFAULT 0 CHECK (rental_fee_refund >= 0),
    deposit_refund     numeric(18,0) NOT NULL DEFAULT 0 CHECK (deposit_refund >= 0),
    amount_owed        numeric(18,0) NOT NULL DEFAULT 0 CHECK (amount_owed >= 0),
    refund_case_id     uuid REFERENCES public.refund_cases (refund_case_id),
    extra_payment_id   uuid REFERENCES public.payments (payment_id),
    settled_by_user_id uuid REFERENCES public.accounts (user_id),
    settled_at         timestamptz   NOT NULL DEFAULT now()
);

-- ---------------------------------------------------------------------
-- 7. Hồ sơ hoàn tiền: nguồn + chờ khách nhập STK
-- ---------------------------------------------------------------------
ALTER TABLE public.refund_cases
    ADD COLUMN IF NOT EXISTS source varchar(40) NOT NULL DEFAULT 'TripCancellation',
    ADD COLUMN IF NOT EXISTS bank_info_requested_at timestamptz,
    ADD COLUMN IF NOT EXISTS bank_info_submitted_at timestamptz,
    ALTER COLUMN bank_code DROP NOT NULL,
    ALTER COLUMN bank_account_last4 DROP NOT NULL,
    ALTER COLUMN consent_version DROP NOT NULL,
    ALTER COLUMN consent_accepted_at DROP NOT NULL;

ALTER TABLE public.refund_cases DROP CONSTRAINT IF EXISTS ck_refund_cases_payout_details;
ALTER TABLE public.refund_cases ADD CONSTRAINT ck_refund_cases_payout_details CHECK (
    (payout_details_ciphertext IS NOT NULL AND payout_details_nonce IS NOT NULL AND payout_details_tag IS NOT NULL
        AND bank_code IS NOT NULL AND bank_account_last4 IS NOT NULL
        AND consent_version IS NOT NULL AND consent_accepted_at IS NOT NULL)
    OR (payout_details_ciphertext IS NULL AND payout_details_nonce IS NULL AND payout_details_tag IS NULL
        AND (sensitive_data_purged_at IS NOT NULL OR status IN ('AwaitingBankInfo', 'Cancelled')))
);

CREATE INDEX IF NOT EXISTS ix_refund_cases_source_status ON public.refund_cases (source, status) WHERE NOT is_deleted;

-- ---------------------------------------------------------------------
-- 8. Seed policy mặc định
-- ---------------------------------------------------------------------
DO $$
DECLARE
    v_policy_id uuid := '7a1d6c1e-2f0b-4c8e-9d3a-5b6f00000001';
BEGIN
    IF NOT EXISTS (SELECT 1 FROM public.rental_policies WHERE NOT is_deleted) THEN
        INSERT INTO public.rental_policies (
            rental_policy_id, policy_family_id, version, name, status, deposit_percent,
            free_cancel_limit, free_cancel_window_days, over_limit_cancel_fee_percent,
            return_grace_hours, extension_max_days_late, suspected_loss_after_days,
            receipt_auto_confirm_hours, min_photos_per_asset, effective_from)
        VALUES (
            v_policy_id, v_policy_id, 1, 'Chính sách thuê đồ mặc định', 'Active', 20,
            2, 30, 5,
            2, 2, 7,
            24, 1, now());

        INSERT INTO public.rental_policy_tiers (rental_policy_id, scope, min_days_late, percent_of_deposit, is_terminal)
        VALUES
            (v_policy_id, 'PickupLate', 1, 5, false),
            (v_policy_id, 'PickupLate', 2, 20, false),
            (v_policy_id, 'PickupLate', 3, 100, true),
            (v_policy_id, 'ReturnLate', 3, 5, false),
            (v_policy_id, 'ReturnLate', 4, 20, false),
            (v_policy_id, 'ReturnLate', 5, 100, true);
    END IF;
END $$;

-- ---------------------------------------------------------------------
-- 9. FE không đọc trực tiếp bảng tài chính / bằng chứng
-- ---------------------------------------------------------------------
ALTER TABLE public.rental_policies ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.rental_policy_tiers ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.rental_evidence_photos ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.rental_reports ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.rental_settlements ENABLE ROW LEVEL SECURITY;

DO $$
DECLARE
    role_name text;
    table_name text;
BEGIN
    FOREACH role_name IN ARRAY ARRAY['anon', 'authenticated'] LOOP
        IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = role_name) THEN
            FOREACH table_name IN ARRAY ARRAY['rental_policies', 'rental_policy_tiers', 'rental_evidence_photos', 'rental_reports', 'rental_settlements'] LOOP
                EXECUTE format('REVOKE ALL ON public.%I FROM %I', table_name, role_name);
            END LOOP;
        END IF;
    END LOOP;
END $$;

COMMIT;
