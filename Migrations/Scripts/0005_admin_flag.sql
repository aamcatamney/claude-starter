-- Administrators. The first account created through the bootstrap invite gets
-- this; everyone else is granted it deliberately.
ALTER TABLE users ADD COLUMN is_admin boolean NOT NULL DEFAULT false;
