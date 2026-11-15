BEGIN;

-- Báo cáo khách gửi về đơn thuê (sự cố thiết bị) hoặc chuyến đi (HDV, lịch trình, an toàn...).
-- Admin / Staff xem ở màn "Báo cáo từ khách hàng" và cập nhật trạng thái xử lý.
CREATE TABLE IF NOT EXISTS public.customer_reports (
    customer_report_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    reporter_user_id uuid NOT NULL,
    order_id uuid NOT NULL,
    source_type varchar(20) NOT NULL,
    category varchar(40) NOT NULL,
    subject_label varchar(255),
    description varchar(2000) NOT NULL,
    preferred_action varchar(40),
    contact_phone varchar(20),
    photo_urls text[] NOT NULL DEFAULT '{}',
    status varchar(20) NOT NULL DEFAULT 'Open',
    admin_note varchar(1000),
    handled_by_user_id uuid,
    handled_at timestamptz,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT customer_reports_source_type_check CHECK (source_type IN ('Rental', 'Trip')),
    CONSTRAINT customer_reports_status_check CHECK (status IN ('Open', 'InProgress', 'Resolved', 'Rejected')),
    CONSTRAINT customer_reports_photo_count_check CHECK (cardinality(photo_urls) <= 5),
    CONSTRAINT customer_reports_reporter_user_id_fkey FOREIGN KEY (reporter_user_id) REFERENCES public.accounts (user_id),
    CONSTRAINT customer_reports_order_id_fkey FOREIGN KEY (order_id) REFERENCES public.orders (order_id),
    CONSTRAINT customer_reports_handled_by_user_id_fkey FOREIGN KEY (handled_by_user_id) REFERENCES public.accounts (user_id)
);

CREATE INDEX IF NOT EXISTS ix_customer_reports_status_created
    ON public.customer_reports (status, created_at DESC);
CREATE INDEX IF NOT EXISTS ix_customer_reports_order
    ON public.customer_reports (order_id);
CREATE INDEX IF NOT EXISTS ix_customer_reports_reporter
    ON public.customer_reports (reporter_user_id, created_at DESC);

COMMIT;
