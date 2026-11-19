# ADR 0007: Settle VNPAY Sandbox through the billing ledger

Status: Accepted

## Context

A student can open a payment link for an issued invoice. A browser redirect is not a reliable confirmation, and a verified provider callback can arrive late or more than once. ADR 0001 requires all recognized receipts to go into the same append-only ledger.

## Decision

- A role-protected Razor form submits only the invoice ID. The backend resolves the signed-in student, takes the current whole-VND balance, and persists a checkout before building the signed Sandbox URL. A pending checkout can resume for 15 minutes.
- Only the provider IPN can settle a checkout. The gateway adapter verifies the HMAC, merchant, reference and scaled amount. Recognition requires both provider success codes. The checkout, receipt and student notification are committed together in a serializable SQL transaction. An invoice cannot receive a duplicate provider receipt.
- Manual collection waits while a checkout is active. A verified charge that no longer matches the balance is marked for reconciliation and shown to staff; it does not change the ledger. When the browser returns, the student sees the saved state and can refresh it without changing it.
- The only endpoint for outgoing checkout is VNPAY Sandbox, disabled by default. Live collection and refunds are separate work.

## Consequences

- Real Sandbox credentials and a public HTTPS IPN on a test environment are required for VNPAY SIT. Local verification uses generated credentials, browser interception and disposable SQL Server databases.
- Operators check a flagged checkout against the Sandbox portal. After verifying that the gateway collected no funds or already refunded the charge, staff can record the result and its evidence on the invoice. The decision stores the staff identity and time. A balance can then be paid again. Only the provider IPN posts VNPAY receipts to the ledger.
