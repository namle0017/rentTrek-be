BEGIN;

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM public.booking_participants
        WHERE email IS NOT NULL
          AND NOT is_deleted
        GROUP BY booking_order_id, lower(btrim(email))
        HAVING count(*) > 1
    ) THEN
        RAISE EXCEPTION
            'Cannot add participant email uniqueness: duplicate normalized emails exist in a booking';
    END IF;
END $$;

ALTER TABLE public.orders
    ADD COLUMN IF NOT EXISTS idempotency_key varchar(100);

CREATE UNIQUE INDEX IF NOT EXISTS ux_orders_customer_idempotency_key
    ON public.orders (customer_user_id, idempotency_key)
    WHERE idempotency_key IS NOT NULL;

CREATE UNIQUE INDEX IF NOT EXISTS ux_booking_participants_booking_normalized_email
    ON public.booking_participants (booking_order_id, lower(btrim(email)))
    WHERE email IS NOT NULL AND NOT is_deleted;

CREATE TABLE IF NOT EXISTS public.participant_invitations
(
    participant_invitation_id uuid PRIMARY KEY,
    participant_id uuid NOT NULL,
    booking_order_id uuid NOT NULL,
    created_by_user_id uuid NOT NULL,
    token_hash varchar(64) NOT NULL,
    expires_at timestamptz NOT NULL,
    redeemed_at timestamptz NULL,
    redeemed_by_user_id uuid NULL,
    revoked_at timestamptz NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_participant_invitations_token_hash UNIQUE (token_hash),
    CONSTRAINT ck_participant_invitations_token_hash_length CHECK (length(token_hash) = 64),
    CONSTRAINT ck_participant_invitations_expiry CHECK (expires_at > created_at),
    CONSTRAINT ck_participant_invitations_redemption CHECK (
        (redeemed_at IS NULL AND redeemed_by_user_id IS NULL)
        OR (redeemed_at IS NOT NULL AND redeemed_by_user_id IS NOT NULL)
    ),
    CONSTRAINT fk_participant_invitations_participant
        FOREIGN KEY (participant_id)
        REFERENCES public.booking_participants (participant_id)
        ON DELETE CASCADE,
    CONSTRAINT fk_participant_invitations_booking_order
        FOREIGN KEY (booking_order_id)
        REFERENCES public.trip_booking_orders (order_id)
        ON DELETE CASCADE,
    CONSTRAINT fk_participant_invitations_created_by
        FOREIGN KEY (created_by_user_id)
        REFERENCES public.accounts (user_id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_participant_invitations_redeemed_by
        FOREIGN KEY (redeemed_by_user_id)
        REFERENCES public.accounts (user_id)
        ON DELETE RESTRICT
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_participant_invitations_active_participant
    ON public.participant_invitations (participant_id)
    WHERE redeemed_at IS NULL AND revoked_at IS NULL;

CREATE INDEX IF NOT EXISTS ix_participant_invitations_booking_order_id
    ON public.participant_invitations (booking_order_id);

COMMIT;
