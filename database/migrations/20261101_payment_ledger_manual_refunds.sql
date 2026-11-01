-- Apply once after the B base schema; transactional failure preserves existing data.
BEGIN;
SELECT pg_advisory_xact_lock(hashtext('rentrek:payment-ledger-migration'));
ALTER TABLE payments ADD COLUMN IF NOT EXISTS transfer_code varchar(40);
ALTER TABLE payments ADD COLUMN IF NOT EXISTS received_amount numeric(18,0) NOT NULL DEFAULT 0;
DO $$ BEGIN
 IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema=current_schema() AND table_name='payments' AND column_name='received_amount' AND (data_type <> 'numeric' OR numeric_scale <> 0)) THEN
 RAISE EXCEPTION 'Incompatible payments.received_amount: review donor schema before migrating';
 END IF;
END $$;
UPDATE payments SET transfer_code = 'RTP' || upper(left(replace(payment_id::text, '-', ''), 16)) WHERE payment_type='Collection' AND transfer_code IS NULL;
CREATE UNIQUE INDEX IF NOT EXISTS ix_payments_transfer_code ON payments(transfer_code);
-- Historical aggregate Paid/ReceivedAmount is preserved. No bank transactions or Done refunds are inferred.
CREATE TABLE payment_webhook_receipts (
    payment_webhook_receipt_id uuid NOT NULL,
    provider character varying(30) NOT NULL,
    provider_event_id character varying(100) NOT NULL,
    payload_hash character varying(64) NOT NULL,
    protected_payload text NOT NULL,
    status character varying(30) NOT NULL,
    failure_reason text,
    has_conflicting_payload boolean NOT NULL,
    attempts integer NOT NULL,
    received_at timestamp with time zone NOT NULL,
    next_attempt_at timestamp with time zone,
    CONSTRAINT "PK_payment_webhook_receipts" PRIMARY KEY (payment_webhook_receipt_id)
);

CREATE TABLE bank_transactions (
    bank_transaction_id uuid NOT NULL,
    provider character varying(30) NOT NULL,
    provider_transaction_id character varying(100) NOT NULL,
    receipt_id uuid NOT NULL,
    payment_id uuid,
    order_id uuid,
    amount numeric(18,0) NOT NULL,
    allocated_amount numeric(18,0) NOT NULL,
    status text NOT NULL,
    reconciliation_reason text,
    reference_code text,
    received_at timestamp with time zone NOT NULL,
    applied_at timestamp with time zone,
    CONSTRAINT "PK_bank_transactions" PRIMARY KEY (bank_transaction_id),
    CONSTRAINT ck_bank_transaction_amount CHECK (amount > 0 AND allocated_amount >= 0 AND allocated_amount <= amount),
    CONSTRAINT "FK_bank_transactions_orders_order_id" FOREIGN KEY (order_id) REFERENCES orders (order_id) ON DELETE RESTRICT,
    CONSTRAINT "FK_bank_transactions_payment_webhook_receipts_receipt_id" FOREIGN KEY (receipt_id) REFERENCES payment_webhook_receipts (payment_webhook_receipt_id) ON DELETE RESTRICT,
    CONSTRAINT "FK_bank_transactions_payments_payment_id" FOREIGN KEY (payment_id) REFERENCES payments (payment_id) ON DELETE RESTRICT
);

CREATE TABLE payment_idempotency_keys (
    payment_idempotency_key_id uuid NOT NULL,
    customer_user_id uuid NOT NULL,
    key character varying(100) NOT NULL,
    request_hash character varying(64) NOT NULL,
    payment_id uuid NOT NULL,
    created_at timestamp with time zone NOT NULL,
    CONSTRAINT "PK_payment_idempotency_keys" PRIMARY KEY (payment_idempotency_key_id),
    CONSTRAINT "FK_payment_idempotency_keys_payments_payment_id" FOREIGN KEY (payment_id) REFERENCES payments (payment_id) ON DELETE RESTRICT
);

CREATE TABLE refund_requests (
    refund_request_id uuid NOT NULL,
    order_id uuid NOT NULL,
    customer_user_id uuid NOT NULL,
    idempotency_key character varying(100) NOT NULL,
    request_hash character varying(64) NOT NULL,
    protected_bank_details text NOT NULL,
    masked_account text NOT NULL,
    reason_code text NOT NULL,
    note character varying(2000),
    amount numeric(18,0) NOT NULL,
    status character varying(30) NOT NULL,
    refund_policy_id uuid NOT NULL,
    refund_policy_rule_id uuid NOT NULL,
    policy_snapshot text NOT NULL,
    submitted_at timestamp with time zone NOT NULL,
    assigned_staff_id uuid,
    reviewed_by uuid,
    reviewed_at timestamp with time zone,
    reject_reason character varying(2000),
    completed_by uuid,
    completed_at timestamp with time zone,
    transfer_reference character varying(200),
    transferred_at timestamp with time zone,
    evidence_reference character varying(1000),
    completion_key text,
    completion_hash text,
    refund_payment_id uuid,
    CONSTRAINT "PK_refund_requests" PRIMARY KEY (refund_request_id),
    CONSTRAINT ck_refund_done_evidence CHECK (status <> 'Done' OR (completed_at IS NOT NULL AND completed_by IS NOT NULL AND transfer_reference IS NOT NULL AND transferred_at IS NOT NULL AND refund_payment_id IS NOT NULL)),
    CONSTRAINT ck_refund_request_amount CHECK (amount > 0),
    CONSTRAINT ck_refund_request_status CHECK (status IN ('PendingReview','Approved','Rejected','Done')),
    CONSTRAINT "FK_refund_requests_accounts_assigned_staff_id" FOREIGN KEY (assigned_staff_id) REFERENCES accounts (user_id) ON DELETE RESTRICT,
    CONSTRAINT "FK_refund_requests_accounts_customer_user_id" FOREIGN KEY (customer_user_id) REFERENCES accounts (user_id) ON DELETE RESTRICT,
    CONSTRAINT "FK_refund_requests_orders_order_id" FOREIGN KEY (order_id) REFERENCES orders (order_id) ON DELETE RESTRICT,
    CONSTRAINT "FK_refund_requests_payments_refund_payment_id" FOREIGN KEY (refund_payment_id) REFERENCES payments (payment_id) ON DELETE RESTRICT,
    CONSTRAINT "FK_refund_requests_refund_policies_refund_policy_id" FOREIGN KEY (refund_policy_id) REFERENCES refund_policies (refund_policy_id) ON DELETE RESTRICT,
    CONSTRAINT "FK_refund_requests_refund_policy_rules_refund_policy_rule_id" FOREIGN KEY (refund_policy_rule_id) REFERENCES refund_policy_rules (refund_policy_rule_id) ON DELETE RESTRICT
);

CREATE TABLE refund_allocations (
    refund_allocation_id uuid NOT NULL,
    refund_request_id uuid NOT NULL,
    bank_transaction_id uuid NOT NULL,
    amount numeric(18,0) NOT NULL,
    CONSTRAINT "PK_refund_allocations" PRIMARY KEY (refund_allocation_id),
    CONSTRAINT ck_refund_allocation_amount CHECK (amount > 0),
    CONSTRAINT "FK_refund_allocations_bank_transactions_bank_transaction_id" FOREIGN KEY (bank_transaction_id) REFERENCES bank_transactions (bank_transaction_id) ON DELETE RESTRICT,
    CONSTRAINT "FK_refund_allocations_refund_requests_refund_request_id" FOREIGN KEY (refund_request_id) REFERENCES refund_requests (refund_request_id) ON DELETE RESTRICT
);

CREATE INDEX "IX_bank_transactions_order_id" ON bank_transactions (order_id);

CREATE INDEX "IX_bank_transactions_payment_id" ON bank_transactions (payment_id);

CREATE UNIQUE INDEX "IX_bank_transactions_provider_provider_transaction_id" ON bank_transactions (provider, provider_transaction_id);

CREATE INDEX "IX_bank_transactions_receipt_id" ON bank_transactions (receipt_id);

CREATE INDEX "IX_bank_transactions_status" ON bank_transactions (status);

CREATE UNIQUE INDEX "IX_payment_idempotency_keys_customer_user_id_key" ON payment_idempotency_keys (customer_user_id, key);

CREATE INDEX "IX_payment_idempotency_keys_payment_id" ON payment_idempotency_keys (payment_id);

CREATE UNIQUE INDEX "IX_payment_webhook_receipts_provider_provider_event_id" ON payment_webhook_receipts (provider, provider_event_id);

CREATE INDEX "IX_payment_webhook_receipts_status_next_attempt_at" ON payment_webhook_receipts (status, next_attempt_at);

CREATE INDEX "IX_refund_allocations_bank_transaction_id" ON refund_allocations (bank_transaction_id);

CREATE UNIQUE INDEX "IX_refund_allocations_refund_request_id_bank_transaction_id" ON refund_allocations (refund_request_id, bank_transaction_id);

CREATE INDEX "IX_refund_requests_assigned_staff_id" ON refund_requests (assigned_staff_id);

CREATE UNIQUE INDEX "IX_refund_requests_customer_user_id_idempotency_key" ON refund_requests (customer_user_id, idempotency_key);

CREATE INDEX "IX_refund_requests_order_id" ON refund_requests (order_id);

CREATE UNIQUE INDEX "IX_refund_requests_refund_payment_id" ON refund_requests (refund_payment_id);

CREATE INDEX "IX_refund_requests_refund_policy_id" ON refund_requests (refund_policy_id);

CREATE INDEX "IX_refund_requests_refund_policy_rule_id" ON refund_requests (refund_policy_rule_id);

CREATE INDEX "IX_refund_requests_status_submitted_at" ON refund_requests (status, submitted_at);
COMMIT;
