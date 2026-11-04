BEGIN;

-- Đếm lần nhập sai (đăng nhập / OTP đặt lại mật khẩu) dùng chung mọi instance BE (PRD NFR-02).
-- attempt_key dạng "login:<email>" / "reset:<email>"; cả email không tồn tại cũng được đếm để không lộ email nào có tài khoản.
CREATE TABLE IF NOT EXISTS public.auth_attempts (
    attempt_key  text        PRIMARY KEY,
    failures     integer     NOT NULL CHECK (failures > 0),
    window_start timestamptz NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_auth_attempts_window_start ON public.auth_attempts (window_start);

COMMIT;
