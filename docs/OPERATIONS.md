# Payment recovery and release checks

## Card checkout

Top-ups and guest closeouts retain their payment key when saving an approved charge fails. Retry the unchanged transaction to save the existing approval without collecting another payment. Changing the amount or reopening a tab does not bypass an unresolved attempt.

An interrupted connection or unconfirmed cancellation is **status unknown**, not a declined card. The POS saves the checkout ID as soon as Square creates it. Cancellation is followed by a final status check: completed payments are recorded as approved; confirmed cancellations can be retried with a new key; other outcomes remain in Square Recovery.

Unfinished attempts survive restart. A tab with an unresolved payment cannot be charged again until the prior attempt is resolved. Pitstop card checkout is similarly blocked while an earlier Pitstop payment is unresolved. Cash operation is independent. This intentionally requires manager review when the system cannot prove whether a card was charged.

Local sale/credit records, stock deductions, and their Square attempt links now commit together in a single database transaction. Failure to link the confirmed payment rolls the local write back. Retrying the same saved transaction repairs or reuses the existing link without another charge or stock deduction.

## Resolve an unfinished payment

1. Open **Admin → Square Recovery**. Record the amount, time, transaction key and checkout ID.
2. Use **Check Square status** for an entry with a saved checkout ID. This reads the existing checkout, verifies the requested amount and AUD currency, and never creates another charge. If no ID is available, check the Square dashboard using the configured environment and location. If the reader is still processing, wait or cancel it and verify its final status.
3. If Square confirms approval, check whether a POS sale/top-up already exists. Link the existing record; do not enter a second payment. Use **Save approved tab top-up** (manager PIN required) for an approved credit without a local record. An approved Pitstop attempt with a saved recovery payload can use Recover Sale. Both saves are idempotent.
4. If approval exists without a recoverable POS record, reconcile the sale/top-up or refund in Square as appropriate, then enter a reconciliation note describing what was done. The status-unknown entry itself is not evidence of approval.
5. If Square confirms cancellation or no payment, mark the attempt manually reconciled with that finding before starting a new card payment.

Upgrading also surfaces historical timeout/transport failures previously labelled Failed. Already reconciled or ignored attempts remain closed. Review these against Square; their appearance does not mean a new charge occurred.

## Inventory workflows

The stock home screen shows active product totals, buying/out-of-stock counts, the last stock-count date, explicit on-hand and status columns, and a clear empty-search state. Untracked/order-in products display a dash for on-hand quantity instead of suggesting they are out of stock.

- **Receive** adds delivery units to the latest database quantity, preserving sales made while the stock screen was open. Purchase cost and the stock movement save together. If a delivery saved but the list failed to refresh, refresh the list rather than receiving it again.
- **Adjust** and quick adjustments update only stock, retaining product setup and prices. Decreases greater than current stock are rejected; use **Set To** for an intentional physical correction, including zero. The movement records the entered reason.
- **Count** only marks products actually visited in the guided count. If a counted product's stock changed before saving, the entire batch is rejected without partial changes or count-date stamps. Refresh and recount while trading is paused.
- Saving a product opened before a quantity change is rejected instead of overwriting an intervening sale. Refresh and review the item, then reapply the intended edits.

## Backups and restore rehearsal

Backups contain a consistent `app.db` snapshot, `config/`, `Images/`, and backup information. Snapshot integrity is checked before an archive is reported as successful. Concurrent backups use separate names. Failure to read critical configuration fails the backup instead of silently omitting it.

Rehearse restoration on a separate copy, never over the running till:

1. Extract an archive into a new folder and run SQLite `PRAGMA integrity_check` against the extracted `app.db`; expect `ok`.
2. Compare tab balances, stock quantities and recent transactions to the backup time.
3. Close the rehearsal app before placing files in its data folders. Restore the database to `Data/app.db`, configuration to `Config/`, and images to `Images/`.
4. Product image references may contain absolute paths: preserve the original storage location or remap references when moving to another machine.
5. Settings protected by Windows DPAPI belong to the original Windows user. On another PC/account, re-enter and verify Square, email and backup credentials. Keep the original backup unchanged.

The backup tests restore temporary archives and verify database integrity, balances, configuration and image contents. They do not replace a rehearsal on the venue's deployment environment.

## Toshiba screen checks

The till polish retains the 1024×768 workspace, catalog grids, navigation positions and existing touch targets. Selected tabs have a check mark beside their balance status; secondary tab actions use an outlined surface. Pitstop shows cash/card totals with equal emphasis and a labelled Take payment action. Payment overlays remain visible during local saving as well as terminal waiting; cancellation is shown only during the terminal phase.

On the Toshiba, check long guest/product names, large totals, card-fee explanations and recovery messages at the installed Windows scaling. Confirm every footer action remains visible, the selected-tab check is readable with Race Day on/off, and touch entry still opens the amount/note pads. Check cash, card, decline/cancellation and approved-payment save failure; the busy overlay must close after failure so the full recovery message can be read. Automated XAML compilation does not verify physical-screen appearance.

## Before installing a release on the till

Pull requests and pushes to main run the application and standalone shopping-list tests on Windows. The MSIX release workflow must pass the same tests before packaging. CI stores TRX test results.

Before a signed deployment, use Square sandbox to check an approved payment, a confirmed cancellation, a lost connection, retry after local save failure, and restart during checkout. Check that an approved save retry makes one charge and one POS record. Verify a backup restore and an MSIX update/relaunch with the venue's signing certificate. No live payment or signed deployment is part of the automated unit tests.

## Next improvement stages

Keep subsequent changes focused and independently reviewable:

- Move sale orchestration from large view models into application services, retaining the shared payment state transitions and regression tests.
- Replace destructive undo with linked reversal records while retaining operator, reason and original transaction references; define Square refund behavior separately.
- Review the common staff journeys on the actual till for tap count, readability, keyboard behavior and error recovery before redesigning screens.
- Exercise interrupted MSIX installation and restore on a spare till; add a guided restore workflow after its failure handling is established.
