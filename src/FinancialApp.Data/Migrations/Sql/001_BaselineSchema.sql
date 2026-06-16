CREATE TABLE IF NOT EXISTS organizations (
    id UUID PRIMARY KEY,
    name TEXT NOT NULL,
    base_currency CHAR(3) NOT NULL DEFAULT 'USD',
    fiscal_year_start_month INT NOT NULL DEFAULT 1,
    accounting_method TEXT NOT NULL DEFAULT 'cash',
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CHECK (fiscal_year_start_month BETWEEN 1 AND 12)
);

CREATE TABLE IF NOT EXISTS accounts (
    id UUID PRIMARY KEY,
    organization_id UUID NOT NULL REFERENCES organizations(id),
    name TEXT NOT NULL,
    account_type TEXT NOT NULL,
    account_subtype TEXT,
    currency CHAR(3) NOT NULL DEFAULT 'USD',
    parent_account_id UUID REFERENCES accounts(id),
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (organization_id, name),
    CHECK (account_type IN ('Asset', 'Liability', 'Equity', 'Income', 'Expense'))
);

CREATE TABLE IF NOT EXISTS import_batches (
    id UUID PRIMARY KEY,
    organization_id UUID NOT NULL REFERENCES organizations(id),
    source TEXT NOT NULL,
    file_name TEXT,
    file_hash TEXT,
    imported_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    raw_metadata JSONB
);

CREATE TABLE IF NOT EXISTS imported_transactions (
    id UUID PRIMARY KEY,
    organization_id UUID NOT NULL REFERENCES organizations(id),
    import_batch_id UUID REFERENCES import_batches(id),
    source TEXT NOT NULL,
    source_transaction_id TEXT,
    posted_date DATE NOT NULL,
    description TEXT NOT NULL,
    raw_description TEXT,
    amount NUMERIC(14,2) NOT NULL,
    currency CHAR(3) NOT NULL DEFAULT 'USD',
    status TEXT NOT NULL DEFAULT 'pending',
    fingerprint TEXT NOT NULL,
    raw_payload JSONB,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (organization_id, source, fingerprint),
    CHECK (status IN ('pending', 'categorized', 'posted', 'ignored', 'duplicate', 'error'))
);

CREATE TABLE IF NOT EXISTS journal_entries (
    id UUID PRIMARY KEY,
    organization_id UUID NOT NULL REFERENCES organizations(id),
    entry_date DATE NOT NULL,
    memo TEXT,
    source_imported_transaction_id UUID REFERENCES imported_transactions(id),
    is_void BOOLEAN NOT NULL DEFAULT false,
    voided_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS journal_lines (
    id UUID PRIMARY KEY,
    journal_entry_id UUID NOT NULL REFERENCES journal_entries(id) ON DELETE CASCADE,
    account_id UUID NOT NULL REFERENCES accounts(id),
    debit NUMERIC(14,2) NOT NULL DEFAULT 0,
    credit NUMERIC(14,2) NOT NULL DEFAULT 0,
    memo TEXT,
    CHECK (debit >= 0),
    CHECK (credit >= 0),
    CHECK (NOT (debit > 0 AND credit > 0)),
    CHECK (debit > 0 OR credit > 0)
);

CREATE TABLE IF NOT EXISTS categorization_rules (
    id UUID PRIMARY KEY,
    organization_id UUID NOT NULL REFERENCES organizations(id),
    name TEXT NOT NULL,
    match_field TEXT NOT NULL DEFAULT 'description',
    match_operator TEXT NOT NULL,
    match_value TEXT NOT NULL,
    target_account_id UUID NOT NULL REFERENCES accounts(id),
    priority INT NOT NULL DEFAULT 100,
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CHECK (match_operator IN ('contains', 'equals', 'starts_with', 'ends_with', 'regex', 'amount_equals', 'amount_between'))
);

CREATE TABLE IF NOT EXISTS reconciliations (
    id UUID PRIMARY KEY,
    organization_id UUID NOT NULL REFERENCES organizations(id),
    account_id UUID NOT NULL REFERENCES accounts(id),
    statement_end_date DATE NOT NULL,
    statement_end_balance NUMERIC(14,2) NOT NULL,
    completed_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_accounts_organization_id
    ON accounts (organization_id);

CREATE INDEX IF NOT EXISTS ix_import_batches_organization_id
    ON import_batches (organization_id);

CREATE INDEX IF NOT EXISTS ix_imported_transactions_organization_id
    ON imported_transactions (organization_id);

CREATE INDEX IF NOT EXISTS ix_imported_transactions_posted_date
    ON imported_transactions (posted_date);

CREATE INDEX IF NOT EXISTS ix_imported_transactions_source_fingerprint
    ON imported_transactions (source, fingerprint);

CREATE INDEX IF NOT EXISTS ix_journal_entries_organization_id
    ON journal_entries (organization_id);

CREATE INDEX IF NOT EXISTS ix_journal_entries_entry_date
    ON journal_entries (entry_date);

CREATE INDEX IF NOT EXISTS ix_journal_lines_account_id
    ON journal_lines (account_id);

CREATE INDEX IF NOT EXISTS ix_journal_lines_journal_entry_id
    ON journal_lines (journal_entry_id);

CREATE INDEX IF NOT EXISTS ix_categorization_rules_organization_id
    ON categorization_rules (organization_id);

CREATE INDEX IF NOT EXISTS ix_reconciliations_organization_id
    ON reconciliations (organization_id);

CREATE INDEX IF NOT EXISTS ix_reconciliations_account_id
    ON reconciliations (account_id);
