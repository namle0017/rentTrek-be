BEGIN;

-- Combo thuê gợi ý cho từng tour (plan trip-booking-refund-overhaul Phase 8).
-- Khách xem ở trang chuyến đi, thêm vào giỏ thuê với ngày thuê = ngày của chuyến.
CREATE TABLE IF NOT EXISTS public.tour_suggested_bundles (
    tour_suggested_bundle_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tour_id uuid NOT NULL,
    bundle_id uuid NOT NULL,
    sort_order integer NOT NULL DEFAULT 0 CHECK (sort_order >= 0),
    note varchar(500),
    created_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT tour_suggested_bundles_tour_id_fkey FOREIGN KEY (tour_id) REFERENCES public.tours (tour_id) ON DELETE CASCADE,
    CONSTRAINT tour_suggested_bundles_bundle_id_fkey FOREIGN KEY (bundle_id) REFERENCES public.product_bundles (bundle_id),
    CONSTRAINT tour_suggested_bundles_tour_id_bundle_id_key UNIQUE (tour_id, bundle_id)
);

CREATE INDEX IF NOT EXISTS ix_tour_suggested_bundles_bundle_id
    ON public.tour_suggested_bundles (bundle_id);

COMMIT;
