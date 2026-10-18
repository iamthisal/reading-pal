# Lending Service — Final Selenium E2E Test Results

## Re-run on 16 October 2026 (Sprint 4 UI) — current result

**15/15 passed** against the local docker-compose stack built from `develop` (`4e63d60`), with a dedicated approved member account (`SELENIUM_USER_EMAIL` / `SELENIUM_USER_PASSWORD`).

The suite was updated for the UI overhaul (PR #96) before this run:
- **Reserving:** members now reserve through catalogue search and the **Borrow {title}** button, which turns into a disabled **Pending** button once requested. The old "Reserve Book" cards are gone.
- **Admin UI:** Add Book and Manage genres now open in-page `<dialog>` modals.
- **Navigation:** the suite opens pages directly by URL, now that a refresh keeps the signed-in user (YRP-214). The sidebar-click workaround for that bug is gone.
- **TC_RESERVE_EDGE_002 now checks the fix:** after #91, a second reservation for the same book is impossible. The button shows Pending, and My borrowings holds exactly one pending row.
- **TC_RESERVE_EDGE_001:** the book is made unavailable with the admin "mark unavailable" action. The member then sees a disabled "no copies" Borrow button.
- **Defaults:** the suite now targets `http://localhost:5173` by default, and has no built-in member credentials. Every run creates books, so it should not be pointed at production.

The lending integration suite (`tests/integration-tests/LendingService.IntegrationTests`) was updated at the same time. It now answers the member-approval check that reserving performs (#92), and adds integration tests for that check (403) and for duplicate reservations (409). **42/42 passed.**

The rest of this document is the original Sprint 3 record (30 Sep 2026).

---

**Service under test:** `services/lending-service` (plus the parts of `inventory-service`, `user-service`, and the frontend it depends on)
**Branch:** `develop`
**Date:** 2026-09-30
**Scope:** the complete Selenium suite in `tests/selenium-tests/LendingService.Selenium` — the original 8 tests (documented in `test_results.md` §6) plus 7 new tests added to close coverage gaps identified by reviewing that suite against the full set of Lending Service features.

---

## 1. Summary

| | Tests | Result | Environment |
|---|---|---|---|
| Original suite (reserve, accept, reject, return, cancel, history filters) | 8 | ✅ 8/8 passed | Deployed site (`https://polite-water-0c0e68a00.3.azurestaticapps.net`) |
| New suite (role boundary, edge cases, fine display, user's own loan views) | 7 | ✅ 7/7 passed | Local docker-compose stack |
| **Combined full run** (all 15 together, one process) | 15 | ✅ **15/15 passed** in 1m53s | Local docker-compose stack |

Artifact: `tests/selenium-tests/LendingService.Selenium/TestResults/lending-selenium-local-full.trx` (the combined 15/15 run).

**Why the new 7 run locally instead of against the deployed site:** `TC_FINE_001` needs to backdate a loan's due date directly in the database to produce an overdue fine without waiting 14 real days — the deployed Azure database isn't reachable from outside its own network, so that test (and the rest of the new batch, kept together for consistency) runs against the local docker-compose stack instead, which exposes MySQL on `localhost:3307`. The original 8 remain verified against the deployed site as documented in `test_results.md` §6; this file doesn't repeat that run.

---

## 2. Why these 7 tests were added

The original 8 tests cover the happy-path backbone (reserve → accept/reject → return, plus cancel and the two admin history filters) but left three real gaps:

1. **No test ever checked role-boundary enforcement in the UI.** The integration test suite (`test_results.md` §3) proves the API rejects the wrong role with 401/403, but nothing proved the browser itself never renders the other role's page for someone who pastes or bookmarks its URL — a distinct, UI-level property.
2. **No edge case around reservation validity.** All 8 original tests reserve a normal, freshly-available book; nothing checked what happens with 0 available copies, or a user attempting to reserve a book they already have pending.
3. **No test ever looked at a fine on screen.** `ReturnFineCalculator` is unit-tested, and the on-time return path is Selenium-tested ("No fine is due"), but the overdue path — the one that actually produces a nonzero fine — was never exercised through the real UI.

A fourth, smaller gap was closed alongside these: `MyBorrowingsController.Get` (the user's own "My borrowings" page) is a separate code path from the admin-side `ReservationsController.GetBorrowed`/`GetHistory` that the original suite already exercises — nothing had checked what a regular user sees on their *own* page once their reservation is accepted or returned.

---

## 3. The 7 new tests

### `RoleBoundarySeleniumTests.cs` (2 tests)

| Test | What it verifies |
|---|---|
| `TC_SECURITY_001_UserRole_CannotReachAdminContentByDirectUrl` | A "User"-role account navigating directly to `/admin/reservations/pending` never ends up on an admin route. |
| `TC_SECURITY_002_AdminRole_CannotReachUserOnlyContentByDirectUrl` | An Admin account navigating directly to `/my-borrowings` (User-only) never ends up on that route either. |

Both pass. Note: because of the `ProtectedRoute` timing bug already documented in `test_results.md` §8.1, the actual redirect currently lands on `/login` rather than the role-specific page (`/home` or `/admin/dashboard`) the role check would otherwise send it to — a hard page load re-triggers that bug regardless of role, before the role check ever runs. That's a known, separate defect affecting the redirect *target*; it doesn't affect what these tests verify, which is that the forbidden route is never actually reached. Both tests assert on the URL, not on which specific fallback page appears, so they remain a correct regression check either way.

### `ReserveBookEdgeCaseSeleniumTests.cs` (2 tests)

| Test | What it verifies |
|---|---|
| `TC_RESERVE_EDGE_001_BookWithNoAvailableCopies_HidesReserveButton` | Once a book's only copy is checked out (accepted), the Discover page shows a "Not available now" badge and hides the Reserve button entirely — the frontend prevents the dead-end click rather than letting the user hit the backend's `400 Book is currently unavailable for reservation.` |
| `TC_RESERVE_EDGE_002_UserReservesSameBookTwice_BothPendingReservationsAreCreated_KnownGap` | **Documents a real, current defect** (see §4 below): the same user can reserve the same book twice while the first reservation is still pending — nothing blocks it. |

Both pass — including the second one, which passing is itself the finding: it asserts the *actual* (undesired) behavior, so it will start failing the moment someone adds the missing duplicate check, which is the intended signal to flip the assertion rather than delete the test.

### `ReturnFineSeleniumTests.cs` (1 test)

| Test | What it verifies |
|---|---|
| `TC_FINE_001_OverdueReturn_ShowsCorrectFineAmount` | A loan returned 5 days overdue shows the correct fine (5 × Rs. 10/day = **Rs. 50.00**) live on the admin "Borrowed books" table before return, in the success message after clicking Return, and in "Reservation history" afterward. |

**How it reaches an overdue state without waiting 14 real days:** `ReservationsController.cs:232` hardcodes the due date to checkout + 14 days with no admin override, so the test creates a normal loan through the real UI (reserve → accept), then makes one direct SQL `UPDATE` against the local lending database to move that specific loan's `DueDate` back 5 days (`LendingSeleniumTestBase.BackdateActiveLoanDueDateAsync`) — reaching a state the app itself would only reach through the passage of time, not changing what the Return endpoint computes from it. This is the reason the whole new batch runs locally: the deployed database isn't reachable for this step.

**A bug in the test itself was found and fixed while writing this one:** the first draft called `Driver.Navigate().Refresh()` on the admin "Borrowed books" page after the backdate, to force a fresh fetch — but that's a hard reload of a protected route, which hit the `ProtectedRoute` timing bug (§8.1 in `test_results.md`) and bounced to `/login`, failing the test with a 25-second timeout. Fixed by waiting for the page's own 10-second polling refresh (`refreshWhileVisible`, `frontend/src/utils/refreshWhileVisible.ts`) instead of forcing a reload — both correct and sidesteps the bug entirely.

### `MyBorrowingsActiveAndHistorySeleniumTests.cs` (2 tests)

| Test | What it verifies |
|---|---|
| `TC_MYBORROWINGS_001_AcceptedReservation_AppearsInUsersOwnCurrentLoans` | Once admin accepts a reservation, that same user's own "My borrowings → Current loans" section shows it, with a real due date rendered (not a placeholder). |
| `TC_MYBORROWINGS_002_ReturnedLoan_AppearsInUsersOwnBorrowingHistory` | Once admin returns that loan on time, the same user's own "My borrowings → Borrowing history" section shows it as Returned. |

Both pass.

---

## 4. Real defect found: duplicate pending reservations are not blocked

`ReservationsController.ReserveBook` (`services/lending-service/Controllers/ReservationsController.cs`, lines 345–428) checks the book's `AvailableCopies` and the caller's identity, but never checks whether that same user already has a `Pending` reservation for the same book — and there's no unique database constraint backing it up either. `TC_RESERVE_EDGE_002` confirms this against the real running system: reserving the same book twice in a row, with no page reload in between, succeeds both times and produces two independent pending rows.

This is now written up as **§8.4** in `test_results.md`, alongside the other application-code defects found during this QA pass (the `ProtectedRoute` timing bug, the JWT dependency CVEs, and the `Program.cs` config-override testability characteristic). Recommended fix there: an `AnyAsync` check for an existing pending reservation by the same user for the same book before creating a new one, returning `409 Conflict` if found — not applied here, since code changes were out of scope for this QA pass.

---

## 5. Full combined run

```
dotnet test tests/selenium-tests/LendingService.Selenium --logger "trx;LogFileName=lending-selenium-local-full.trx" --results-directory tests/selenium-tests/LendingService.Selenium/TestResults
```

All 15 tests (8 original + 7 new) run together against the local docker-compose stack, in one process, with no shared state between them — every test creates its own uniquely-named book via the real Admin UI, so none of the new tests depend on or interfere with the original ones.

```
Passed  MyBorrowingsCancelSeleniumTests.TC_CANCEL_001_UserCancelsOwnPendingReservation_MovesItToCancelledSection
Passed  ReturnFineSeleniumTests.TC_FINE_001_OverdueReturn_ShowsCorrectFineAmount
Passed  ReserveBookEdgeCaseSeleniumTests.TC_RESERVE_EDGE_002_UserReservesSameBookTwice_BothPendingReservationsAreCreated_KnownGap
Passed  ReserveBookEdgeCaseSeleniumTests.TC_RESERVE_EDGE_001_BookWithNoAvailableCopies_HidesReserveButton
Passed  RoleBoundarySeleniumTests.TC_SECURITY_001_UserRole_CannotReachAdminContentByDirectUrl
Passed  RoleBoundarySeleniumTests.TC_SECURITY_002_AdminRole_CannotReachUserOnlyContentByDirectUrl
Passed  MyBorrowingsActiveAndHistorySeleniumTests.TC_MYBORROWINGS_001_AcceptedReservation_AppearsInUsersOwnCurrentLoans
Passed  MyBorrowingsActiveAndHistorySeleniumTests.TC_MYBORROWINGS_002_ReturnedLoan_AppearsInUsersOwnBorrowingHistory
Passed  AdminAcceptRejectSeleniumTests.TC_REJECT_001_AdminRejectsPendingReservation_MovesItToHistoryAsCancelled
Passed  AdminAcceptRejectSeleniumTests.TC_ACCEPT_001_AdminAcceptsPendingReservation_MovesItToBorrowedBooks
Passed  AdminReservationHistorySeleniumTests.TC_HISTORY_001_FilteringByCancelled_OnlyShowsCancelledRecords
Passed  AdminReservationHistorySeleniumTests.TC_HISTORY_002_FilteringByReturned_OnlyShowsReturnedRecords
Passed  AdminReturnSeleniumTests.TC_RETURN_001_AdminReturnsOnTimeLoan_ShowsNoFineAndMovesItToHistory
Passed  ReserveBookSeleniumTests.TC_RESERVE_001_UserReservesAvailableBook_ShowsSuccessAndAppearsInMyBorrowings
Passed  ReserveBookSeleniumTests.TC_RESERVE_002_ReservedBook_AppearsInAdminPendingQueue

Total tests: 15   Passed: 15   Failed: 0   Total time: 1.9 minutes
```

## 6. What changed to support this

- `tests/selenium-tests/LendingService.Selenium/LendingSeleniumTestBase.cs` — added three helper methods used only by the new tests: `GetAdminApiTokenAsync` (real login via the Auth API, not the browser), `GetBookIdByIsbnAsync` (resolves a book's numeric inventory id, which the UI never surfaces directly), and `BackdateActiveLoanDueDateAsync` (the direct DB write behind `TC_FINE_001`). Also added `UserServiceUrl`/`InventoryServiceUrl`/`LendingDbConnectionString`, all env-var overridable and defaulted to the local stack — explicitly documented as local-only, since there's no exposed DB port or equivalent access on the deployed site.
- `tests/selenium-tests/LendingService.Selenium/LendingService.Selenium.Tests.csproj` — added the `MySqlConnector` package reference for the backdate helper.
- `test_results.md` — §1 and §6 updated to the combined 15/15 total; new §8.4 documenting the duplicate-reservation defect; §8.1 cross-referenced with the `ProtectedRoute`-bug recurrence found while writing `TC_FINE_001`; §9 artifacts index updated.
