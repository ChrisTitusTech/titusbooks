CREATE OR REPLACE FUNCTION protect_reconciled_journal_line()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    target_journal_entry_id UUID;
BEGIN
    target_journal_entry_id := CASE
        WHEN TG_OP = 'DELETE' THEN OLD.journal_entry_id
        ELSE NEW.journal_entry_id
    END;

    IF TG_OP = 'UPDATE'
       AND OLD.reconciliation_id IS NULL
       AND NEW.reconciliation_id IS NOT NULL
       AND NEW.journal_entry_id = OLD.journal_entry_id
       AND NEW.account_id = OLD.account_id
       AND NEW.debit = OLD.debit
       AND NEW.credit = OLD.credit
       AND NEW.memo IS NOT DISTINCT FROM OLD.memo THEN
        IF NOT EXISTS (
            SELECT 1
            FROM reconciliations reconciliation
            INNER JOIN journal_entries entry
                ON entry.id = NEW.journal_entry_id
            WHERE reconciliation.id = NEW.reconciliation_id
              AND reconciliation.organization_id = entry.organization_id
              AND reconciliation.account_id = NEW.account_id
              AND reconciliation.completed_at IS NOT NULL
        ) THEN
            RAISE EXCEPTION 'Reconciliation must belong to the journal line account and organization.';
        END IF;

        RETURN NEW;
    END IF;

    IF EXISTS (
        SELECT 1
        FROM journal_lines
        WHERE journal_entry_id = target_journal_entry_id
          AND reconciliation_id IS NOT NULL
    ) THEN
        RAISE EXCEPTION 'Journal entries containing reconciled lines cannot be changed.';
    END IF;

    RETURN CASE WHEN TG_OP = 'DELETE' THEN OLD ELSE NEW END;
END;
$$;

DROP TRIGGER IF EXISTS trg_protect_reconciled_journal_line ON journal_lines;

CREATE TRIGGER trg_protect_reconciled_journal_line
BEFORE INSERT OR UPDATE OR DELETE ON journal_lines
FOR EACH ROW
EXECUTE FUNCTION protect_reconciled_journal_line();
