# ADR 0002: Use an explicit housing-request workflow and stay provenance

Status: Accepted

Date: 2026-09-10

## Context

Registration, transfer and checkout share one `Đơn lưu trú` lifecycle, but rules were previously distributed across manager and student services. Status alone did not identify who owed the next action, and transfer/checkout decisions inferred the source stay from mutable current data.

## Decision

Use `HousingRequestWorkflow` as the shared policy for transitions, responsibility, SLA and presentation. Persist `ActionRequiredAt` whenever responsibility changes. Transfer and checkout persist `SourceStayId`; a stay created by approved registration/transfer persists `HousingRequestId`. Use row-version concurrency, serializable decision transactions and a SQL sequence for request numbers.

`HousingRequestEvent` remains append-only and records every successful state change. The manager Controller maps endpoints to actions through one workflow Interface; the student service performs only student-owned actions.

## Consequences

- Manager and student screens cannot silently diverge on status, responsibility or SLA.
- Student waiting time no longer inflates manager overdue counts.
- A delayed decision fails safely when the source stay has changed.
- Worklist callers receive SQL-projected pages rather than materializing the full graph.
- Schema and migration logic are more explicit, and future workflow changes require policy tests plus migration review.
