-- Deterministic state for screenshots. Every value here is fixed on purpose:
-- the point of these images is that they only change when the UI does.
--
-- Safe to re-run. Only ever touches the two screenshot@ accounts.

BEGIN;

DELETE FROM users WHERE email IN ('screenshot@example.com', 'unverified@example.com');

-- Password is "screenshot-password". The hash is committed rather than
-- generated so a rerun produces byte-identical state.
-- An administrator, so the admin chip appears in the landing screenshot.
INSERT INTO users (id, email, password_hash, display_name, is_active, email_verified, is_admin)
VALUES (
    '11111111-1111-1111-1111-111111111111',
    'screenshot@example.com',
    '$2a$12$U0nqhd15oKnBjoFkv3FTQeNMyAPX.oeadXOtbxmTyQ1Hm3QR/BlHq',
    'Sam Rivera',
    true,
    true,
    true
);

INSERT INTO users (id, email, password_hash, display_name, is_active, email_verified)
VALUES (
    '22222222-2222-2222-2222-222222222222',
    'unverified@example.com',
    '$2a$12$U0nqhd15oKnBjoFkv3FTQeNMyAPX.oeadXOtbxmTyQ1Hm3QR/BlHq',
    NULL,
    true,
    false
);

-- Live links, so reset-password and verify-email can be photographed in the
-- state a user actually reaches them in. The application stores a SHA-256 of
-- the token, so these are the hashes of the plain values the capture script
-- puts in the URL.
--
-- One token per theme, because links are single-use and the verify-email page
-- redeems on load: a single shared token would leave the second pass — and
-- every re-run — photographing "this link no longer works".
DELETE FROM user_tokens
WHERE user_id IN (
    '11111111-1111-1111-1111-111111111111',
    '22222222-2222-2222-2222-222222222222'
);

INSERT INTO user_tokens (user_id, purpose, token_hash, expires_at)
VALUES
    -- sha256('screenshot-verify-light')
    ('22222222-2222-2222-2222-222222222222', 'email_verification',
     '81828c5b5ff8c0370df684b32d9be42e269612f909330b73fec1af0c064671bd',
     now() + interval '24 hours'),
    -- sha256('screenshot-verify-dark')
    ('22222222-2222-2222-2222-222222222222', 'email_verification',
     'e5e0655dc9e523b3a52d52275d214b7ef4f44174af94311daecd44605691a5f0',
     now() + interval '24 hours'),
    -- sha256('screenshot-reset-light')
    ('11111111-1111-1111-1111-111111111111', 'password_reset',
     '7a2fb0bcc2b289c6417caefaad69f5c645627fe7a66ad7773cce01dceda5d3d7',
     now() + interval '1 hour'),
    -- sha256('screenshot-reset-dark')
    ('11111111-1111-1111-1111-111111111111', 'password_reset',
     '43ce93315c4f7056d76d92701d2ecaecb5bb39637d2da80f6e9a9462eeb91e28',
     now() + interval '1 hour');

COMMIT;
