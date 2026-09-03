ALTER TABLE users
    ADD COLUMN email_verified boolean NOT NULL DEFAULT false,
    -- Changing this invalidates every cookie the user holds. Rotated on
    -- password reset so a session an attacker already has stops working.
    ADD COLUMN security_stamp uuid NOT NULL DEFAULT gen_random_uuid();

-- One row per issued email link. The token itself is never stored, only a
-- SHA-256 of it, so a leak of this table yields nothing usable.
CREATE TABLE user_tokens (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id         uuid NOT NULL REFERENCES users (id) ON DELETE CASCADE,
    purpose         text NOT NULL,
    token_hash      text NOT NULL,
    expires_at      timestamptz NOT NULL,
    consumed_at     timestamptz NULL,
    created_at      timestamptz NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX ux_user_tokens_hash ON user_tokens (token_hash);
CREATE INDEX ix_user_tokens_user_purpose ON user_tokens (user_id, purpose);
