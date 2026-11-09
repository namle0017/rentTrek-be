BEGIN;

-- Đếm lần nhập sai (đăng nhập / OTP đặt lại mật khẩu) dùng chung mọi instance BE (PRD NFR-02).
-- attempt_key dạng "login:<email>" / "reset:<email>"; cả email không tồn tại cũng được đếm để không lộ email nào có tài khoản.
CREATE TABLE IF NOT EXISTS public.auth_attempts (
    attempt_key  text        PRIMARY KEY,
    failures     integer     NOT NULL CHECK (failures > 0),
    window_start timestamptz NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_auth_attempts_window_start ON public.auth_attempts (window_start);

-- Chỉ backend dùng direct PostgreSQL connection được truy cập bảng bảo mật này.
ALTER TABLE public.auth_attempts ENABLE ROW LEVEL SECURITY;

DO $$
DECLARE
    role_name text;
BEGIN
    FOREACH role_name IN ARRAY ARRAY['anon', 'authenticated'] LOOP
        IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = role_name) THEN
            EXECUTE format('REVOKE ALL ON public.auth_attempts FROM %I', role_name);
        END IF;
    END LOOP;
END $$;

COMMIT;
