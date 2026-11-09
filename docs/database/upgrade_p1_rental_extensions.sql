-- Áp dụng cho database PostgreSQL/Supabase trước khi deploy tính năng gia hạn đơn thuê (T5 / FR-R09 / UAT-19).
-- An toàn khi chạy lại nhiều lần (idempotent).
BEGIN;

CREATE TABLE IF NOT EXISTS public.rental_order_adjustments (
    rental_order_adjustment_id  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    rental_order_id             uuid          NOT NULL REFERENCES public.rental_orders(order_id),
    adjustment_type             varchar(30)   NOT NULL,
    old_end_at                  timestamptz   NOT NULL,
    new_end_at                  timestamptz   NOT NULL,
    extra_amount                numeric(12,2) NOT NULL CHECK (extra_amount >= 0),
    requested_by_user_id        uuid          NOT NULL,
    created_at                  timestamptz   NOT NULL DEFAULT now()
);

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'rental_order_adjustments_requested_by_user_id_fkey'
    ) THEN
        IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'accounts') THEN
            ALTER TABLE public.rental_order_adjustments
                ADD CONSTRAINT rental_order_adjustments_requested_by_user_id_fkey
                FOREIGN KEY (requested_by_user_id) REFERENCES public.accounts(user_id);
        ELSE
            ALTER TABLE public.rental_order_adjustments
                ADD CONSTRAINT rental_order_adjustments_requested_by_user_id_fkey
                FOREIGN KEY (requested_by_user_id) REFERENCES public.users(user_id);
        END IF;
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_rental_order_adjustments_rental_order_id
    ON public.rental_order_adjustments (rental_order_id);

CREATE INDEX IF NOT EXISTS ix_rental_order_adjustments_requested_by_user_id
    ON public.rental_order_adjustments (requested_by_user_id);

COMMIT;
