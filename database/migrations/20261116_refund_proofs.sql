BEGIN;

-- Ảnh chứng từ chuyển khoản hoàn tiền (docs/05-MANUAL-REFUND-WORKFLOW.md §7).
-- Gắn với đúng một hồ sơ hoàn tiền (refund_case_id) hoặc một khoản hoàn cũ không qua hồ sơ (payment_id).
-- File nằm ở storage riêng tư, storage_path chỉ là khóa đối tượng trong bucket / thư mục private.
CREATE TABLE IF NOT EXISTS public.refund_proofs (
    refund_proof_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    refund_case_id uuid,
    payment_id uuid,
    storage_path varchar(500) NOT NULL,
    content_type varchar(100) NOT NULL,
    file_name varchar(255) NOT NULL,
    size_bytes bigint NOT NULL CHECK (size_bytes > 0),
    uploaded_by_user_id uuid NOT NULL,
    source varchar(20) NOT NULL DEFAULT 'Manual',
    created_at timestamptz NOT NULL DEFAULT now(),
    is_deleted boolean NOT NULL DEFAULT false,
    CONSTRAINT refund_proofs_refund_case_id_fkey FOREIGN KEY (refund_case_id) REFERENCES public.refund_cases (refund_case_id),
    CONSTRAINT refund_proofs_payment_id_fkey FOREIGN KEY (payment_id) REFERENCES public.payments (payment_id),
    CONSTRAINT refund_proofs_uploaded_by_user_id_fkey FOREIGN KEY (uploaded_by_user_id) REFERENCES public.accounts (user_id),
    CONSTRAINT refund_proofs_source_check CHECK (source IN ('ExcelImport', 'Manual')),
    -- Đúng một trong hai: hồ sơ hoàn tiền hoặc khoản hoàn
    CONSTRAINT ck_refund_proofs_owner CHECK (
        (refund_case_id IS NOT NULL AND payment_id IS NULL)
        OR (refund_case_id IS NULL AND payment_id IS NOT NULL)
    )
);

CREATE INDEX IF NOT EXISTS ix_refund_proofs_refund_case
    ON public.refund_proofs (refund_case_id)
    WHERE NOT is_deleted;

CREATE INDEX IF NOT EXISTS ix_refund_proofs_payment
    ON public.refund_proofs (payment_id)
    WHERE NOT is_deleted;

COMMIT;
