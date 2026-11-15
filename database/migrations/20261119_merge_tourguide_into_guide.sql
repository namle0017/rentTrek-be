BEGIN;

UPDATE public.roles
SET is_deleted = false,
    description = 'Hướng dẫn viên dẫn đoàn tour trekking',
    updated_at = now()
WHERE role_name = 'Guide'
  AND is_deleted = true;

INSERT INTO public.roles (role_id, role_name, description, created_at, updated_at, is_deleted)
SELECT gen_random_uuid(), 'Guide', 'Hướng dẫn viên dẫn đoàn tour trekking', now(), now(), false
WHERE NOT EXISTS (
    SELECT 1
    FROM public.roles
    WHERE role_name = 'Guide'
);

UPDATE public.accounts AS account
SET role_id = guide.role_id,
    updated_at = now()
FROM public.roles AS legacy_role
CROSS JOIN LATERAL (
    SELECT role_id
    FROM public.roles
    WHERE role_name = 'Guide'
      AND is_deleted = false
    ORDER BY created_at
    LIMIT 1
) AS guide
WHERE account.role_id = legacy_role.role_id
  AND legacy_role.role_name = 'TourGuide';

DELETE FROM public.roles
WHERE role_name = 'TourGuide';

COMMIT;
