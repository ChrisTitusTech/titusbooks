CREATE UNIQUE INDEX IF NOT EXISTS ux_journal_entries_source_imported_transaction_id
    ON journal_entries (source_imported_transaction_id)
    WHERE source_imported_transaction_id IS NOT NULL;
