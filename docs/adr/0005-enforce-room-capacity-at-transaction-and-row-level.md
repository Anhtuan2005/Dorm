# ADR 0005: Enforce room capacity at transaction and row level

Status: Accepted

Date: 2026-09-12

## Context

Occupancy, active holds and maintenance blocks are stored in different rows. Validating only one collection, or validating before a later write, can overbook a room when approval, handover and maintenance updates overlap. SQL row constraints alone cannot express this cross-row invariant.

## Decision

`RoomCapacityPolicy` owns the shared reserve, hold-consumption and maintenance-block decisions. Mutating services load the relevant stays, holds and open incidents and recheck the decision inside a serializable transaction. A request may consume only its own valid one-bed hold; maintenance may use only capacity not already occupied, held or blocked by another open incident.

SQL Server check constraints protect local facts: positive room capacity, chronological stays, positive held-bed counts and non-negative blocked-bed counts. They complement rather than duplicate the cross-row business policy. Real SQL Server integration tests cover expired holds and concurrent last-bed operations without using demo seeding.

## Consequences

- Room-map projections and mutation decisions use one vocabulary and capacity formula.
- Competing approvals or confirmations cannot both take the final bed.
- Maintenance cannot invalidate an existing active hold.
- The database rejects structurally invalid rows even when data is written outside the application.
- Tests that need transaction/isolation behavior run against disposable databases named with the `DormitoryManagementSystem_Test_` prefix and clean them up after execution.
