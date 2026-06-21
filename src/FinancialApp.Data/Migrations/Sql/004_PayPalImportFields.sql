ALTER TABLE imported_transactions
    ADD COLUMN IF NOT EXISTS reference_transaction_id TEXT,
    ADD COLUMN IF NOT EXISTS source_type TEXT,
    ADD COLUMN IF NOT EXISTS source_status TEXT,
    ADD COLUMN IF NOT EXISTS posted_time TIME,
    ADD COLUMN IF NOT EXISTS source_time_zone TEXT,
    ADD COLUMN IF NOT EXISTS gross_amount NUMERIC(14,2),
    ADD COLUMN IF NOT EXISTS fee_amount NUMERIC(14,2),
    ADD COLUMN IF NOT EXISTS net_amount NUMERIC(14,2),
    ADD COLUMN IF NOT EXISTS transaction_kind TEXT NOT NULL DEFAULT 'other';

CREATE INDEX IF NOT EXISTS ix_imported_transactions_reference_transaction_id
    ON imported_transactions (organization_id, reference_transaction_id);

CREATE INDEX IF NOT EXISTS ix_imported_transactions_status_kind
    ON imported_transactions (organization_id, status, transaction_kind);

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'ck_imported_transactions_fee_amount_nonnegative'
    ) THEN
        ALTER TABLE imported_transactions
            ADD CONSTRAINT ck_imported_transactions_fee_amount_nonnegative
            CHECK (fee_amount IS NULL OR fee_amount >= 0);
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'ck_imported_transactions_transaction_kind'
    ) THEN
        ALTER TABLE imported_transactions
            ADD CONSTRAINT ck_imported_transactions_transaction_kind
            CHECK (transaction_kind IN (
                'other',
                'payment',
                'fee',
                'refund',
                'transfer',
                'currency_conversion'
            ));
    END IF;
END
$$;
