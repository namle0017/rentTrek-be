-- Phase 7: đổi lịch booking sang chuyến khác cùng tour.
BEGIN;

ALTER TABLE trip_booking_orders
    ADD COLUMN IF NOT EXISTS reschedule_count int NOT NULL DEFAULT 0;

CREATE TABLE IF NOT EXISTS trip_booking_reschedules (
    id uuid PRIMARY KEY,
    order_id uuid NOT NULL REFERENCES trip_booking_orders(order_id),
    from_trip_id uuid NOT NULL REFERENCES trips(trip_id),
    to_trip_id uuid NOT NULL REFERENCES trips(trip_id),
    participant_count int NOT NULL CHECK (participant_count > 0),
    price_diff numeric(18,0) NOT NULL,
    fee numeric(18,0) NOT NULL DEFAULT 0,
    status varchar(30) NOT NULL CHECK (status IN ('PendingPayment', 'Completed', 'Expired', 'Failed')),
    payment_id uuid NULL REFERENCES payments(payment_id),
    hold_expires_at timestamptz NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    completed_at timestamptz NULL
);

CREATE INDEX IF NOT EXISTS ix_trip_booking_reschedules_order_id ON trip_booking_reschedules(order_id);
-- Giữ chỗ chuyến mới khi đang chờ trả chênh lệch
CREATE INDEX IF NOT EXISTS ix_trip_booking_reschedules_hold
    ON trip_booking_reschedules(to_trip_id, hold_expires_at) WHERE status = 'PendingPayment';
CREATE UNIQUE INDEX IF NOT EXISTS ux_trip_booking_reschedules_payment_id
    ON trip_booking_reschedules(payment_id) WHERE payment_id IS NOT NULL;

COMMIT;
