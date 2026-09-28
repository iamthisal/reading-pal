# Lending Service — Developer Guide

This guide describes the implemented Lending features, HTTP contracts, data ownership, Kafka events, frontend behaviour, and developer verification. CI/CD, infrastructure provisioning, and deployment configuration are outside its scope.

## Reservations, acceptance, and cancellation

Lending owns Reservations, BorrowRecords, Fines, and ReservationEvents. It uses existing User APIs for admin name lookup and Inventory HTTP APIs for availability, titles, checkout, and return. Inventory is not a Kafka consumer; Lending events are intended for the future Notification Service.

### API and state transitions

| Endpoint | Access | Behavior |
| --- | --- | --- |
| POST /api/reservations | Authenticated numeric user identity; admin identity rejected | Checks Inventory availability synchronously, then creates Pending. Does not deduct a copy. |
| GET /api/reservations/pending | Admin | Pending and Accepting records, oldest reservation date then ID first; user names and book titles included. |
| POST /api/reservations/{id}/accept | Admin | Pending → Accepting → Borrowed; calls Inventory checkout, creates one borrow record, sets checkout from Inventory and due date 14 days later, queues acceptance event. |
| POST /api/reservations/{id}/reject | Admin | Pending → Cancelled; queues cancellation event, no borrow record or copy change. |
| GET /api/my-borrowings | User | Own pending/accepting reservations, active loans, returned history, cancelled history, and fine totals. |
| POST /api/my-borrowings/reservations/{id}/cancel | User | Only own Pending reservation can be cancelled. Already Cancelled is a successful repeat; unknown/other user's ID returns 404. Accepting and later states cannot be cancelled. |

Reservation Status is a concurrency token. Cancellation cannot overwrite an admin acceptance that won the race. State and event are saved together; duplicate cancellation does not add another event. Acceptance persists its intent before calling Inventory. An unconfirmed checkout stays Accepting and can be retried by an admin; rejection/user cancellation is disabled in that state. See checkout details below.

### Frontend

Admin Pending Reservations shows Accept, Reject, or Retry Accept. Successful actions remove the pending row. Borrowed Books shows active loans; Reservation History shows returned/cancelled records, not newly accepted loans. These admin lists refresh every 10 seconds while visible and when the tab regains focus, as well as through Refresh.

Users see their own pending list in My borrowings & fines. Cancel requires confirmation and is offered only for Pending. Cancelled records remain in user history. Reservation and cancellation success messages dismiss after five seconds. Lookup failures on admin lists return 503; missing lookup records use fallback labels. A user history catalogue lookup failure preserves the records with an unavailable-title label.

### Kafka and persistence

Lending's outbox worker runs immediately and then at five-second intervals. It marks PublishedAtUtc only after Kafka acknowledges publication and retries failures. Successful HTTP actions mean events are durably queued, not necessarily delivered. Delivery is at least once; consumers must deduplicate by eventId.

| Event type/topic default | Configuration override |
| --- | --- |
| reservation-accepted | Kafka__Topics__ReservationAccepted |
| reservation-cancelled | Kafka__Topics__ReservationCancelled |
| book-returned | Kafka__Topics__BookReturned |

Accepted/cancelled payloads contain eventId, eventType, schemaVersion, timestampUtc, reservationId, userId, bookId, reservationDate, and status. Kafka message keys use reservation ID. Return payload details are in returns below. Provision all three topics on the actual broker. No Notification consumer is implemented.

Apply the current migrations rather than manually adding tables. AddReservationEventOutbox and AddBorrowRecords establish the initial Lending tables; AddReturnsAndFines adds fines, return intent, and the unique (ReservationId, EventType) outbox index. Inventory checkout/return migrations are also needed. Startup attempts migrations; inspect failures rather than relying on a healthy HTTP listener alone.

### Verification

Local checks previously completed: 52 tests in tests/LendingService.Tests, frontend production build, and targeted Inventory return/checkout tests. These local test files have been excluded from the requested feature commits, so a fresh clone may not contain them. Check the tests actually committed and configured in CI. Existing package vulnerability and lint warnings remain.

Automated tests do not verify every live integration. Reserve, cancel, accept, and return fresh records to verify transitions and inspect Kafka topics; notifications are not expected until the consumer exists.


## Inventory checkout integration

Accepting a reservation now calls Inventory's admin-only POST /api/Books/{bookId}/checkouts/{reservationId}. Inventory atomically subtracts one AvailableCopies (only when positive) and records the reservation ID in BookCheckouts. TotalCopies is unchanged. Retrying an allocated reservation returns the original checkout timestamp without another deduction. Reusing the ID for a different book is rejected. Inventory's existing edit operations use an availability concurrency check to avoid overwriting a concurrent deduction.

Lending first saves Accepting, then calls Inventory with the admin token. After confirmation it saves Borrowed, the borrowing record, and the acceptance outbox event. An interrupted or unconfirmed checkout stays Accepting and appears in the queue as Retry Accept. Reject is disabled because Inventory may already have deducted a copy. Retry Accept after restoring Inventory connectivity or availability. This is recoverable by an admin retry, not an automatic reconciliation worker. Reservations with no available copies are not converted to Borrowed.

The frontend catalogue and admin inventory page refresh every 10 seconds while visible and on window focus. Admin pending, borrowed, and history lists also refresh every 10 seconds while visible and on focus/visibility return. The count changes by polling, not by a Kafka browser subscription. Kafka publishes accepted, cancelled, and returned events for the future Notification consumer. Inventory is not a consumer; it updates stock through HTTP endpoints. Do not add a consumer that deducts/restores the same copies again.

### Developer verification

Create a fresh reservation, accept it, and confirm AvailableCopies decreases by one while TotalCopies stays the same. Repeated acceptance must not deduct again. Reserving and rejecting do not change counts. Old Borrowed records created before this integration are not deducted retroactively.

Local validation previously passed 50 Inventory tests after return support and 52 Lending tests after the history/cancellation additions, plus the frontend build. Existing lint and package warnings remain. The local tests were excluded from requested feature commits; a fresh checkout may have different test projects. These checks are not live Docker/MySQL/Kafka or deployed browser verification.

For copy restoration see returns and fines below. For user/admin list behavior see borrowing history below. Loans created before checkout tracking have no ledger entry and cannot safely restore a copy without reconciliation.


## Book returns and fines

Admins use Return on the Borrowed books page. `POST /api/returns/{borrowRecordId}` records a durable return intent, calls Inventory to restore one available copy, then saves the returned dates/status, any unpaid fine, and the `book-returned` outbox event together. Repeating a completed request returns the existing result. Interrupted returns stay visible with Retry Return and preserve the date first recorded at the counter.

The fine is LKR 10 per overdue calendar day in Asia/Colombo. The due date is inclusive: returning later on the same date has no fine; the next calendar day costs Rs. 10. Timestamps are stored in UTC. Positive fines are stored in Fines with DaysOverdue, DailyRate, Amount, and Unpaid status, linked uniquely to the borrow record. No zero-value fine is created. The admin sees the calculated amount after returning the book. Fine payment/collection is outside this feature.

Inventory's admin-only `POST /api/Books/{bookId}/checkouts/{reservationId}/return` marks the existing checkout returned and increments AvailableCopies in one transaction. Repeated calls do not increment again. TotalCopies is unchanged. A loan created before Inventory checkout tracking was introduced needs its data reconciled before it can be returned through this endpoint; unknown checkouts are rejected rather than increasing stock blindly.

### Persistence and events

AddCheckoutReturns adds Inventory's checkout return timestamp. AddReturnsAndFines adds Lending's fine records and return intent. The outbox index permits one event per reservation and event type, allowing acceptance and return events for the same loan.

The BookReturned event uses the book-returned topic by default.

The existing outbox worker delivers events asynchronously and retries failures. Delivery is at least once; consumers should deduplicate by eventId. No notification consumer is added. Inventory is updated synchronously; consumers must not restore the same copy again.

The event includes eventId, eventType=book-returned, schemaVersion, timestampUtc, reservationId, borrowRecordId, bookId, userId, and returnDate. A successful return means the event is durably queued, not necessarily delivered to Kafka yet.

### Fine display and history

Admin Borrowed Books shows current overdue days and estimated fine amounts before return. Estimates use the original ReturnRequestedAtUtc while a return is pending, so retry delays do not increase the customer's fine. After completion the record leaves the active list and appears in Admin Reservation History with its saved fine and status. That history also includes cancelled reservations. Admin lists refresh every 10 seconds while visible and on tab focus/visibility return.

Users see active loans, returned records, cancelled reservations, saved unpaid totals, and separate estimated current fines in My borrowings & fines. See history behavior below. Returned records and fines remain stored; disappearing from the active list does not delete them.

### Troubleshooting and verification

“Inventory return could not be confirmed” is generic feedback for an unsuccessful Inventory call, not proof that Inventory is stopped. Inspect Inventory logs for the actual cause: missing endpoint/migration, invalid admin token, missing matching checkout, inconsistent counts, or connectivity. The return intent stays retryable and retains its original date. The reported local failure has not been conclusively diagnosed; automatic table refresh does not fix it.

The current return endpoint requires a matching Inventory BookCheckout. Older borrowed records or records from another Inventory database can fail this requirement even when the book exists. Do not manually increment copies on each retry.

Local automated checks have covered same-day/no-fine returns, Sri Lankan midnight boundaries, Rs. 10/day calculation, duplicate returns, preserved dates after Inventory failures, and rollback when counts are inconsistent. A live environment still needs verification of its migrations, data, HTTP authentication, broker, topic, and outbox delivery.


## User and admin borrowing history

### User: /my-borrowings

Accessible from Home/Profile under My borrowings & fines. GET /api/my-borrowings requires the User role and derives identity from the validated JWT; callers cannot select another user ID.

The response contains:

- pending: own Pending/Accepting reservations, oldest first, with title, reservation date, status, and canCancel.
- active: unreturned loans, earliest due date first, including days overdue and estimated fines.
- history: returned loans, latest return first, with recorded fine amount/status.
- cancelled: own cancelled reservations, newest reservation date first.
- totalUnpaid: saved fines with Unpaid status.
- estimatedActiveFines: estimates for current loans, kept separate from saved unpaid amounts.

Fines use Sri Lankan calendar dates at LKR 10/day. For an interrupted return, estimates use the preserved return-request date. Saved returned fines are not recalculated as time passes. Missing/unreachable book titles do not hide the user's records. The page reloads on entry and manual Refresh, and after cancellation; it does not currently poll in the background.

POST /api/my-borrowings/reservations/{id}/cancel checks ownership and Pending status, saves Cancelled and the existing cancellation outbox event, and does not contact Inventory. The UI asks for confirmation, disables cancellation during submission, refreshes on completion/conflict, and dismisses success feedback after five seconds.

### Admin: /admin/reservations/history

GET /api/reservations/history requires Admin and returns only Returned/Cancelled reservations, newest reservation date then ID first. It includes user/book labels, reservation/checkout/due/return dates, overdue days, saved fine amount, and fine status. Cancelled rows have no fine or borrowing dates where none exist. Missing names/books use fallback labels; lookup service failures return 503.

The page supports All, Returned, and Cancelled filters. Pending and Borrowed Books remain separate pages for ongoing work. All three admin lists refresh every 10 seconds while visible, on focus/visibility return, and manually. A successful acceptance belongs in Borrowed Books, not History, until returned.

### Data limitations

Cancelled includes admin rejection and user cancellation. Existing reservations do not record cancellation actor or cancellation date, so the UI does not claim who cancelled or display reservation date as cancellation date. Fine payment/collection is not implemented. No new history schema migration is needed beyond the existing return/fine migrations.

### Manual checks

1. Reserve as a user: verify it appears in that user's pending list and the admin queue.
2. Cancel: verify it leaves both pending lists, appears in both histories after refresh, and leaves counts unchanged.
3. Accept a new reservation: verify active borrowing appears; cancellation must now be rejected by the API even from a stale page.
4. Return a loan: verify it leaves Borrowed Books and appears in both returned histories with the saved fine.
5. Sign in as another user: verify the first user's records are not exposed.

If an endpoint returns 404 after adding the feature, restart/rebuild Lending. If an admin history lookup returns 503, inspect User/Inventory responses and logs. An Inventory return confirmation failure remains a separate integration issue; refreshing history does not resolve it.
