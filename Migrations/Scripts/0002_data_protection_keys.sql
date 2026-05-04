CREATE TABLE data_protection_keys (
    id              serial PRIMARY KEY,
    friendly_name   text NULL,
    xml             text NOT NULL,
    created_at      timestamptz NOT NULL DEFAULT now()
);
