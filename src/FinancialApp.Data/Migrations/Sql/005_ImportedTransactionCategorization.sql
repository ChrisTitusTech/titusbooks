ALTER TABLE imported_transactions
    ADD COLUMN IF NOT EXISTS category_account_id UUID REFERENCES accounts(id),
    ADD COLUMN IF NOT EXISTS matched_rule_id UUID REFERENCES categorization_rules(id);

CREATE INDEX IF NOT EXISTS ix_imported_transactions_category_account_id
    ON imported_transactions (organization_id, category_account_id);

CREATE INDEX IF NOT EXISTS ix_categorization_rules_priority
    ON categorization_rules (organization_id, is_active, priority);
