# ADR 0003: Separate approval from physical move-in

Status: Accepted

Date: 2026-09-11

## Context

Approving a registration or transfer previously changed the student's active stay immediately. In reality, a management decision and physical room handover happen at different times. Treating them as one action could make the system show a student in the destination room before keys were handed over and could end a valid source stay too early.

## Decision

Approval creates a one-bed `Giữ chỗ`, records it on the `Đơn lưu trú`, and moves the request to `AwaitingMoveIn`. A separate manager-owned **Xác nhận nhận phòng** action rechecks the hold, destination capacity and source stay inside a serializable transaction. Only then does it release the hold, end the source stay for a transfer, create the destination stay and mark the request approved.

Student cancellation or manager rejection while awaiting move-in releases the hold. Confirmation requires the request's own unreleased, unexpired, one-bed hold for the destination room. A missing or mismatched hold blocks confirmation rather than silently overbooking the room.

## Consequences

- The room map distinguishes held beds from occupied beds throughout the handover window.
- A student keeps their current room until the physical transfer is confirmed.
- The work queue exposes `Chờ nhận phòng` as a distinct state and action.
- Capacity and provenance are revalidated at both approval and handover boundaries.
- A manager can close an expired or otherwise unusable handover by rejecting it, which releases any remaining hold and lets the student submit a new request.
