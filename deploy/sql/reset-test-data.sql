\set ON_ERROR_STOP on

BEGIN;

DELETE FROM reconciliations;
DELETE FROM categorization_rules;
DELETE FROM journal_lines;
DELETE FROM journal_entries;
DELETE FROM imported_transactions;
DELETE FROM import_batches;
DELETE FROM accounts;
DELETE FROM organizations;

INSERT INTO organizations (
    id,
    name,
    base_currency,
    fiscal_year_start_month,
    accounting_method
)
VALUES (
    '10000000-0000-0000-0000-000000000001',
    'Test Company',
    'USD',
    1,
    'cash'
);

INSERT INTO accounts (
    id,
    organization_id,
    name,
    account_type,
    account_subtype,
    currency
)
VALUES
(
    '20000000-0000-0000-0000-000000000001',
    '10000000-0000-0000-0000-000000000001',
    'Test Checking',
    'Asset',
    'Checking',
    'USD'
),
(
    '20000000-0000-0000-0000-000000000002',
    '10000000-0000-0000-0000-000000000001',
    'Test Expenses',
    'Expense',
    'General Expense',
    'USD'
);

DO $$
BEGIN
    IF (SELECT count(*) FROM organizations) <> 1 THEN
        RAISE EXCEPTION 'Expected exactly one organization after reset.';
    END IF;

    IF (SELECT count(*) FROM accounts) <> 2 THEN
        RAISE EXCEPTION 'Expected exactly two accounts after reset.';
    END IF;
END
$$;

COMMIT;

SELECT id, name, base_currency
FROM organizations;

SELECT id, name, account_type, account_subtype, is_active
FROM accounts
ORDER BY name;
