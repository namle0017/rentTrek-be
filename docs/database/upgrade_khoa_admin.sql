-- Apply once to an existing Supabase/PostgreSQL database before deploying admin APIs.
-- Safe to re-run. These fields are already defined by API contract sections 12 and 23.
BEGIN;
ALTER TABLE public.orders ADD COLUMN IF NOT EXISTS reconciliation_deadline_at timestamptz;
-- Required by the existing customer order creation flow as well as tracked admin updates.
ALTER TABLE public.orders ADD COLUMN IF NOT EXISTS idempotency_key varchar(100);
CREATE UNIQUE INDEX IF NOT EXISTS ux_orders_customer_idempotency_key
    ON public.orders (customer_user_id, idempotency_key) WHERE idempotency_key IS NOT NULL;
ALTER TABLE public.reviews ADD COLUMN IF NOT EXISTS is_hidden boolean NOT NULL DEFAULT false;
ALTER TABLE public.reviews ADD COLUMN IF NOT EXISTS hidden_reason text;
CREATE INDEX IF NOT EXISTS ix_orders_reconciliation_deadline_at ON public.orders (reconciliation_deadline_at)
    WHERE NOT is_deleted AND payment_status = 'NeedsReconciliation';
COMMIT;
