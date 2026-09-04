-- One row per registered authenticator. A user may hold several: a phone, a
-- laptop, a hardware key.
CREATE TABLE user_passkeys (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id         uuid NOT NULL REFERENCES users (id) ON DELETE CASCADE,
    credential_id   bytea NOT NULL,
    public_key      bytea NOT NULL,
    -- Authenticators report a counter that only goes up. A value that fails to
    -- advance is how a cloned credential gives itself away.
    sign_count      bigint NOT NULL DEFAULT 0,
    aaguid          uuid NULL,
    transports      text NULL,
    name            text NOT NULL,
    created_at      timestamptz NOT NULL DEFAULT now(),
    last_used_at    timestamptz NULL
);

-- Sign-in looks a credential up by id alone, with no user in hand, so this
-- must be unique across every account.
CREATE UNIQUE INDEX ux_user_passkeys_credential_id ON user_passkeys (credential_id);
CREATE INDEX ix_user_passkeys_user ON user_passkeys (user_id);
