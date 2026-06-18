CREATE UNIQUE INDEX IF NOT EXISTS ux_imported_transactions_organization_fingerprint
    ON imported_transactions (organization_id, fingerprint);
