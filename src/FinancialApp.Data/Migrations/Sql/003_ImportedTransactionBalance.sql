ALTER TABLE imported_transactions
    ADD COLUMN IF NOT EXISTS balance NUMERIC(14,2);
