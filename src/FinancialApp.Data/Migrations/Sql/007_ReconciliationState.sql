ALTER TABLE journal_lines
    ADD COLUMN IF NOT EXISTS reconciliation_id UUID
        REFERENCES reconciliations(id);

CREATE INDEX IF NOT EXISTS ix_journal_lines_reconciliation_id
    ON journal_lines (reconciliation_id);

CREATE OR REPLACE FUNCTION protect_reconciled_journal_line()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF OLD.reconciliation_id IS NOT NULL THEN
        RAISE EXCEPTION 'Reconciled journal lines cannot be changed or deleted.';
    END IF;

    RETURN CASE WHEN TG_OP = 'DELETE' THEN OLD ELSE NEW END;
END;
$$;

DROP TRIGGER IF EXISTS trg_protect_reconciled_journal_line ON journal_lines;

CREATE TRIGGER trg_protect_reconciled_journal_line
BEFORE UPDATE OR DELETE ON journal_lines
FOR EACH ROW
EXECUTE FUNCTION protect_reconciled_journal_line();

CREATE OR REPLACE FUNCTION protect_reconciled_journal_entry()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM journal_lines
        WHERE journal_entry_id = OLD.id
          AND reconciliation_id IS NOT NULL
    ) THEN
        RAISE EXCEPTION 'Journal entries containing reconciled lines cannot be changed or deleted.';
    END IF;

    RETURN CASE WHEN TG_OP = 'DELETE' THEN OLD ELSE NEW END;
END;
$$;

DROP TRIGGER IF EXISTS trg_protect_reconciled_journal_entry ON journal_entries;

CREATE TRIGGER trg_protect_reconciled_journal_entry
BEFORE UPDATE OR DELETE ON journal_entries
FOR EACH ROW
EXECUTE FUNCTION protect_reconciled_journal_entry();
