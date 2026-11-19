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

## Peak intake amendment, 2026-11-19

Management can schedule an intake window using Vietnamese local dates. During the window a new applicant submits one request for a numbered bed and receives a provisional hold in the same transaction. Transfers keep the ordinary review-first workflow. The default provisional deadline is 24 hours and management can choose 1–72 hours for a window. Closing a window stops new provisional holds. An unexpired hold keeps its own deadline.

Approval rechecks capacity and converts the request's hold to the ordinary seven-day handover hold. Cancellation and rejection release it. If a provisional hold expires first, its bed becomes available immediately and the request keeps its position and review status. A manager must recheck availability before approval. Only confirmed handover creates a stay.

VNU-HCM describes a response period after submission in its [2026–27 intake announcement](https://www.ktxhcm.edu.vn/sinh-vien-noi-tru/thong-bao-menu/ve-viec-dang-ky-o-ky-tuc-xa-nam-hoc-2026-2027). Cornell describes timed selection and release in its [room selection FAQ](https://scl.cornell.edu/sub/residential-life/housing/housing-details/apply-housing/current-undergrad-application-process/room-selection-faqs). The configurable provisional deadline is a local product decision.
