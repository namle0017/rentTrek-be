-- =====================================================================
-- RenTrek - Seed cau hinh thue do (system_configurations)
-- Cac key:
--   rental.holdMinutes          = 15  (phut giu cho khi tao don, API contract 5.2)
--   rental.cleaningBufferHours  = 2   (gio ve sinh giua hai luot thue, D-11)
--
-- Idempotent: INSERT ... ON CONFLICT (config_key) DO NOTHING
-- (config_key co rang buoc UNIQUE) -> chay lai khong ghi de gia tri da
-- duoc admin chinh sua.
--
-- updated_by_user_id la NOT NULL -> dung tai khoan Admin dau tien chua bi
-- xoa mem. Neu khong co Admin nao, script dung voi loi ro rang: hay tao
-- tai khoan Admin truoc roi chay lai.
-- value_type: 'Int' (chua co convention san trong schema/seed; dung 'Int').
-- =====================================================================

BEGIN;

DO $$
DECLARE
    v_admin_id uuid;
BEGIN
    SELECT a.user_id
      INTO v_admin_id
      FROM accounts a
      JOIN roles r ON r.role_id = a.role_id
     WHERE r.role_name = 'Admin'
       AND a.is_deleted = false
       AND r.is_deleted = false
     ORDER BY a.created_at
     LIMIT 1;

    IF v_admin_id IS NULL THEN
        RAISE EXCEPTION 'Khong tim thay tai khoan Admin (roles.role_name = ''Admin'', is_deleted = false). Hay tao Admin truoc khi seed system_configurations.';
    END IF;

    INSERT INTO system_configurations
        (updated_by_user_id, config_key, config_value, value_type, description)
    VALUES
        (v_admin_id, 'rental.holdMinutes',         '15', 'Int', 'So phut giu cho (hold) khi tao don thue truoc khi thanh toan'),
        (v_admin_id, 'rental.cleaningBufferHours', '2',  'Int', 'So gio ve sinh/dem giua hai luot thue lien tiep cua cung mot tai san')
    ON CONFLICT (config_key) DO NOTHING;
END;
$$;

COMMIT;
