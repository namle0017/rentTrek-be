BEGIN;

-- Lời mời participant dùng mã 6 số gửi qua email; sai quá số lần cho phép thì khóa lời mời
ALTER TABLE public.participant_invitations
    ADD COLUMN IF NOT EXISTS failed_attempts integer NOT NULL DEFAULT 0;

ALTER TABLE public.participant_invitations
    DROP CONSTRAINT IF EXISTS ck_participant_invitations_failed_attempts;
ALTER TABLE public.participant_invitations
    ADD CONSTRAINT ck_participant_invitations_failed_attempts CHECK (failed_attempts >= 0);

COMMIT;
