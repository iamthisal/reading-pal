# Reading Pal — End-to-End (Selenium) Test Plan

ASE assignment, end-to-end testing. This plan covers the whole running system: the React frontend, the
four .NET services (User, Inventory, Lending, Notification), MySQL and Kafka, driven through the browser
with Selenium WebDriver.

Every case below was checked against the current code on `develop` (routes in `frontend/src/App.tsx`,
the pages and hooks behind them, and the controllers and DTO validation in `services/*`). Expected
results quote the real messages the app shows. Behaviour the system does not have (for example paying a
fine) is listed in [§9 Not tested](#9-not-tested-and-why) instead of being invented as a test.

---

## 1. Scope

**In scope**

| Area | What is exercised end to end |
|---|---|
| Accounts | Register, sign in, sign out, session expiry, admin approval / rejection / revocation of members |
| Access control | Role-based routes, redirects, admin-only and member-only pages, direct-URL access |
| Catalogue | Discover page, shelves and curated sections, search with filters/sort/pages, book and author pages, favourites |
| Borrowing | Reserve → admin accept / reject → loan → return → fine, member cancellation, Borrow button states |
| Administration | Books (add, edit, availability, delete), genres, authors, curated lists, reservation queue, borrowed books, history |
| Notifications | Events flowing Lending/Inventory → Kafka → Notification Service → bell and Notifications page, for members and admins |
| Resilience | What the UI does when a service is down or a request conflicts with someone else's change |

**Out of scope:** unit and integration behaviour already covered by the xUnit suites, load
(JMeter branch), mutation testing, Azure deployment and infrastructure.

## 2. Test environment

| Component | Local address | Notes |
|---|---|---|
| Frontend (Vite) | `http://localhost:5173` | `frontend/.env.local` must point at the local services, otherwise the app falls back to Azure |
| User Service | `http://localhost:5050` | 5000 is taken by macOS AirPlay |
| Inventory Service | `http://localhost:5001` | |
| Lending Service | `http://localhost:5148` | |
| Notification Service | `http://localhost:5003` | |
| Kafka UI | `http://localhost:8085` | handy for checking events while a test runs |
| MySQL | `localhost:3306` (Docker) | used only for test setup, e.g. backdating a loan |

Start-up:

```bash
docker compose -f infrastructure/kafka/docker-compose.yml --profile tools up -d
docker compose up -d --build mysql user-service inventory-service lending-service notification-service
cd frontend && npm run dev
```

`ASPNETCORE_ENVIRONMENT=Development` is required: the admin login and the demo accounts only exist in
Development since PR #100.

**Browsers:** Chrome (primary, headless in CI), plus Firefox for the P1 smoke set. Viewports:
1440×900 (desktop) and 390×844 (phone) for the responsive cases.

## 3. Approach

- **Framework:** Selenium WebDriver 4.48 + xUnit 2.9 in C#, the same stack as the existing
  `tests/*Selenium` projects, so the suites share helpers and run with `dotnet test`.
- **Page objects:** one class per page (`LoginPage`, `SearchPage`, `AdminBooksPage`, …) that hides selectors.
  Prefer stable selectors in this order: element `id` (forms use `firstName`, `lastName`, `email`,
  `password`, `author-name`, `list-key`, …), ARIA role + accessible name, visible text, then CSS classes.
- **Arrange through the API, act and assert through the UI.** Creating a book or an approved member
  through the UI in every test is slow and makes failures ambiguous. Set-up calls the REST APIs with an
  admin token; the behaviour under test is then driven in the browser.
- **Time travel through the database only where the system has no other way:** overdue loans and due-soon
  reminders need a past checkout date, so set-up updates `BorrowRecords` in `ReadingPal_lending`
  (the existing `ReturnFineSeleniumTests` already does this).
- **Isolation:** every test creates its own data with a unique suffix (`E2E-<guid8>`) and removes it
  afterwards where the app allows it. Tests never depend on the demo catalogue.
- **Waiting:** explicit waits only (`WebDriverWait` on a condition), never fixed sleeps. Kafka-driven
  results (notifications) are asserted with a polling wait of up to 30 s: the Lending outbox runs every
  5 s, and the bell refreshes every 10 s.
- **Native dialogs:** destructive actions use `window.confirm`; tests accept or dismiss them through
  `driver.SwitchTo().Alert()`. Add/edit forms open in `<dialog>` elements (`dialog[open]`).
- **React inputs:** set values with real key presses (`SendKeys`) so React's change handlers run; use the
  existing `SetReactInputValue` helper only where typing is impractical.
- **Evidence:** on failure, save a screenshot and the browser console log to `TestResults/`.

### Accounts and test data

| Account | How it is made | Used for |
|---|---|---|
| Admin A | Built-in admin from `appsettings.Development.json` (`E2E_ADMIN_EMAIL` / `E2E_ADMIN_PASSWORD`) | All admin flows |
| Admin B | Second admin (local only, role set in the database) | "Other admins are told" notification cases |
| Approved member | Registered through the API, then accepted by Admin A through the API | Borrowing, favourites, profile |
| Pending member | Registered through the API, not accepted | Approval cases |
| Book with copies | Created through the API with 1–3 copies | Borrowing |
| Book with 0 copies | Created, then marked unavailable through the API | Out-of-stock cases |

Configuration comes from `E2E_*` environment variables with local defaults (see README.md, "Run the tests").
The User Service default is port 5050; the older suites' base class defaults to 5000.

### Priorities and types

- **P1:** the core journey; a failure blocks a release. 34 cases, run on every PR; the connected smoke journey in §6 covers the core of them.
- **P2:** important rules and error handling. Run nightly.
- **P3:** edge cases, cosmetics and resilience. Run before a release.
- **Type:** **H** = happy path, **U** = unhappy path (invalid input, refused action, conflict or outage).
- **Existing** marks a case already automated in `tests/*Selenium`; the new suite reuses or extends it
  instead of duplicating it.

---

## 4. Existing Selenium coverage

| Project | Already automated |
|---|---|
| `UserService.Selenium` | Login (valid, wrong password, unknown email, empty fields); registration (success, duplicate email, invalid email, empty first name / last name / email); profile (view, update, invalid email, missing fields, short password, update without password); RBAC (admin and user dashboards, navigation visibility, signed-out access); admin user lists (active, pending, admin excluded) |
| `InventoryService.Selenium` | Add book (success, missing fields, invalid copies); update book (success, missing fields, duplicate ISBN, copies above / below loans); remove book (no loans, with loans blocked, confirmation); availability shown on the home page |
| `LendingService.Selenium` | Reserve (success, appears in admin queue, 0 copies disabled, duplicate blocked); admin accept / reject; returns (on time, overdue fine); history filters; My Borrowings (current, history, cancel); role boundaries by direct URL |

The plan below includes these cases (marked **Existing**) so the coverage matrix in §8 is complete, and adds
everything that is not automated yet.

---

## 5. Test cases

### 5.1 Authentication and session (`AUTH`)

| ID | Type | Pri | Scenario | Steps / data | Expected result |
|---|---|---|---|---|---|
| AUTH-01 | H | P1 | Approved member signs in | `/login`, valid member email and password, Sign in | Lands on `/home` (Discover); topbar shows the member's menu; no approval warning |
| AUTH-02 | H | P1 | Admin signs in | Built-in admin credentials | Lands on `/admin/dashboard`; admin sidebar is shown |
| AUTH-03 | U | P1 | Wrong password | Valid email, wrong password | Stays on `/login`; error "Invalid email or password"; password field is not echoed anywhere (**Existing**) |
| AUTH-04 | U | P2 | Unknown email | Unregistered email | Same message as AUTH-03: the response does not reveal whether the email exists (**Existing**) |
| AUTH-05 | U | P2 | Empty fields | Submit with both fields empty | Browser `required` validation blocks the submit; no request is sent (**Existing**) |
| AUTH-06 | U | P3 | Malformed email | `not-an-email` | `type="email"` validation blocks the submit |
| AUTH-07 | H | P2 | Pending member signs in | Registered, not yet approved | Sign-in succeeds and lands on `/home`; banner "Account Pending Approval: Your registration must be validated before reserving physical books." |
| AUTH-08 | U | P2 | Rejected member signs in | Admin rejected the registration (the user row is deleted) | "Invalid email or password" |
| AUTH-09 | H | P1 | Log out | Signed in → user menu → Log out | Back on `/login`; `localStorage.token` is cleared; browser Back does not show protected content |
| AUTH-10 | U | P2 | Expired token | Put a token with `exp` in the past into `localStorage.token`, open `/home` | Redirected to `/login` (the token is treated as missing) |
| AUTH-11 | U | P2 | Malformed token | `localStorage.token = "garbage"`, open `/my-borrowings` | Redirected to `/login`, no crash |
| AUTH-12 | U | P3 | Token expires during a session | Sign in with a token that expires in about 60 s, stay on a page | When it expires the app signs out and returns to `/login` without a reload |
| AUTH-13 | H | P2 | Root address by role | Signed in as member, then as admin, open `/` | `/` redirects by role: member → `/home`, admin → `/admin/dashboard` |
| AUTH-14 | H | P3 | Unknown route | `/no-such-page` | Redirected to `/`, then on by role (or to `/login` if signed out) |
| AUTH-15 | H | P3 | Session survives a reload | Sign in, reload the page | Still signed in on the same page |

### 5.2 Registration (`REG`)

| ID | Type | Pri | Scenario | Steps / data | Expected result |
|---|---|---|---|---|---|
| REG-01 | H | P1 | Register a new member | First name, last name, unique email, password of 6+ characters | Success panel "Registration Successful!" with a "Sign in now" link; redirected to `/login` after about 3 s (**Existing**, extend for the redirect) |
| REG-02 | H | P2 | New member appears for admins | After REG-01, admin opens `/admin/users/pending` | The new member is listed as waiting for approval |
| REG-03 | U | P1 | Duplicate email | Email of an existing member | "Email already in use"; stays on the form (**Existing**) |
| REG-04 | U | P2 | Password shorter than 6 | `abc12` | `minLength=6` blocks the submit; the server also rejects it (400) if the check is bypassed |
| REG-05 | U | P2 | Missing first name / last name / email / password | Leave each empty in turn | `required` blocks the submit (**Existing** for three of the four; add the password case) |
| REG-06 | U | P3 | Invalid email format | `a@` | Blocked by `type="email"` (**Existing**) |
| REG-07 | U | P3 | Registering with the admin email | `admin@library.com` | Registration succeeds (that address is not stored in the user table), but signing in with the admin password still logs in as the built-in admin. **Record as a finding:** the built-in admin's address can be registered as a member |
| REG-08 | H | P3 | "Sign in" link | Click the link to the login page | Navigates to `/login` |
| REG-09 | H | P3 | Layout matches the login page | Compare at 1440 and 390 px | Same two-panel layout; first and last name side by side at desktop width, stacked at phone width |

### 5.3 Access control and routing (`RBAC`)

| ID | Type | Pri | Scenario | Steps / data | Expected result |
|---|---|---|---|---|---|
| RBAC-01 | U | P1 | Signed out, any protected page | Open `/home`, `/search`, `/books/1`, `/my-borrowings`, `/admin/books` | Each redirects to `/login` (**Existing** for the admin dashboard) |
| RBAC-02 | U | P1 | Member opens admin pages | Every `/admin/*` route | Redirected to `/home`; no admin data is shown (**Existing** for some routes; extend to all 10) |
| RBAC-03 | U | P2 | Admin opens member-only pages | `/profile`, `/my-borrowings`, `/notifications` | Redirected to `/admin/dashboard` (**Existing** for some) |
| RBAC-04 | H | P2 | Pages shared by both roles | Admin opens `/home`, `/search`, `/books/:id`, `/authors/:name`, `/favourites` | All load |
| RBAC-05 | H | P2 | Navigation by role | Inspect the sidebar for each role | Members see the library navigation (Discover, Search, Favourites, My Borrowings, Notifications, Profile); admins see the admin navigation (**Existing**) |
| RBAC-06 | U | P2 | Member calls an admin API with their own token | Run `fetch('/api/Books', {method:'POST'})` from the browser with the member's token | 403; nothing is created (checks the server, not only the hidden button) |

### 5.4 Member accounts, administration (`ACC`)

| ID | Type | Pri | Scenario | Steps / data | Expected result |
|---|---|---|---|---|---|
| ACC-01 | H | P1 | Approve a pending member | `/admin/users/pending` → member → Accept | Member leaves the pending list and appears under `/admin/users/active` |
| ACC-02 | H | P1 | Approved member can now borrow | After ACC-01, member signs in again | No approval banner; Borrow buttons are shown |
| ACC-03 | H | P2 | Reject a pending member | Pending member → Reject | Member disappears from both lists; signing in now gives "Invalid email or password" (AUTH-08) |
| ACC-04 | H | P2 | Revoke an active member | Active member → Revoke → confirm | Member moves back to the pending list |
| ACC-05 | U | P2 | Revoke dialog cancelled | Revoke → Cancel the confirm | Nothing changes |
| ACC-06 | U | P1 | Revoked member with a still-valid session tries to borrow | Member signed in **before** the revocation, clicks Borrow | The button is still shown (the old token says approved), but reserving fails with "Your account is not approved to reserve books. Please contact the library."; nothing reaches the admin queue |
| ACC-07 | H | P2 | Revoked member signs in again | After ACC-04 | Approval banner shown, no Borrow buttons |
| ACC-08 | U | P3 | Two admins act on the same pending member | Admin A accepts in tab 1; Admin B clicks Reject on a stale page in tab 2 | Tab 2 shows "Failed to reject user. Please try again."; the member stays approved |
| ACC-09 | H | P3 | Built-in admin is never listed | Active and pending lists | No admin account in either list (**Existing**) |

### 5.5 Profile (`PROF`)

| ID | Type | Pri | Scenario | Steps / data | Expected result |
|---|---|---|---|---|---|
| PROF-01 | H | P1 | View profile | `/profile` | Current first name, last name and email are filled in (**Existing**) |
| PROF-02 | H | P2 | Update names | Change first and last name → Save | "Profile updated successfully!"; values persist after a reload (**Existing**) |
| PROF-03 | H | P2 | Change password | New password of 6+ characters | Success; old password then fails at sign-in, new one works |
| PROF-04 | H | P2 | Save without a password | Leave the password empty | Other details save; the old password still works (**Existing**) |
| PROF-05 | U | P2 | Email taken by another member | Another member's email | "Email already in use"; nothing changes |
| PROF-06 | U | P2 | Invalid email, empty name, short password | One field at a time | Blocked by the browser (`type="email"`, `required`, `minLength=6`) (**Existing**) |
| PROF-07 | H | P3 | Change email, then sign in with it | New unique email | Sign-in works with the new email; the old email gives "Invalid email or password" |

### 5.6 Discover and catalogue browsing (`CAT`)

| ID | Type | Pri | Scenario | Steps / data | Expected result |
|---|---|---|---|---|---|
| CAT-01 | H | P1 | Discover page loads | Member opens `/home` | Banner with search box, book count, poster slider, recommended shelf and curated sections |
| CAT-02 | H | P2 | Banner search | Type a title in the banner search → Enter | Goes to `/search?q=<title>` with matching results |
| CAT-03 | H | P2 | Curated list drives a shelf | Admin edits the `recommended` list order (API); member reloads `/home` | The shelf shows the same books in the new order |
| CAT-04 | H | P3 | Poster slider | Click Next poster, then the first dot, then the poster's "Browse books" link | Slides change and the dots follow; the link opens `/search?sort=newest` |
| CAT-05 | H | P2 | Placeholders while loading | Pause the Inventory container (`docker pause readingpal-inventory-service`), load `/home`, then resume it | Animated skeleton placeholders are shown; the shelves replace them once the catalogue answers |
| CAT-06 | U | P2 | Inventory down | Stop Inventory, load `/home` | "Unable to load the book catalogue. Please ensure the Inventory Service is running."; the rest of the shell still works |
| CAT-07 | H | P3 | Shelf size follows the screen | Load `/home` at 2560, 1440 and 390 px | The recommended shelf shows all 6 books at 2560 px; fewer, scrollable at smaller widths; no horizontal page scroll at 390 px |
| CAT-08 | H | P2 | Availability on cards | One book with copies, one with 0 | Card status and Borrow state differ correctly (**Existing**) |

### 5.7 Search (`SRCH`)

| ID | Type | Pri | Scenario | Steps / data | Expected result |
|---|---|---|---|---|---|
| SRCH-01 | H | P1 | Search by title | `q` = part of a unique title | Only matching books; the result count is shown |
| SRCH-02 | H | P2 | Search by author, ISBN and genre text | One query of each kind | Each finds the book (the server matches title, author, genre and ISBN) |
| SRCH-03 | H | P2 | Several words | `"agatha orient"` | Only books matching **every** word |
| SRCH-04 | H | P2 | Category filter | Tick one genre, then two | Results limited to those genres; the URL gains `genre=` parameters; unticking restores the list |
| SRCH-05 | H | P2 | Available only | Toggle "available only" | Books with 0 copies disappear; `available=1` in the URL |
| SRCH-06 | H | P3 | Sort | Switch between relevance and newest, plus title or author if offered | Order changes accordingly; the choice is kept in the URL |
| SRCH-07 | H | P2 | Pages | Catalogue with more than 12 matches → next page | 12 per page; page 2 shows the next books; `page=2` in the URL |
| SRCH-08 | H | P2 | Deep link | Open `/search?q=dune&available=1&page=1` directly | Filters and the query are restored from the URL |
| SRCH-09 | U | P2 | No matches | `q = zzqqxx-E2E` | Friendly empty state, no error |
| SRCH-10 | U | P3 | Odd input | Very long query, `%`, quotes, `<script>` | No crash; nothing is executed; treated as plain text |
| SRCH-11 | U | P3 | Out-of-range page | `/search?q=<title>&page=999` | No error message; the page and navigation still work |
| SRCH-12 | U | P2 | Search fails | Stop Inventory, run a search | "Unable to search the catalogue. Please try again." |
| SRCH-13 | H | P3 | Banner input focus | Click into the banner search box | The whole box highlights; no inner green ring (regression for the fix in PR #98) |

### 5.8 Book and author pages (`BOOK`)

| ID | Type | Pri | Scenario | Steps / data | Expected result |
|---|---|---|---|---|---|
| BOOK-01 | H | P1 | Open a book | Click a card | `/books/:id` shows cover, title, author link, genre, description, availability and Borrow |
| BOOK-02 | H | P2 | Back button | Book page → Back | Returns to the previous page with its state (for example the same search results) |
| BOOK-03 | U | P2 | Book that does not exist | `/books/999999` and `/books/abc` | "Book not found" page, no crash |
| BOOK-04 | H | P2 | Author page | Click the author on a book page | `/authors/<name>` with portrait, biography and the author's books |
| BOOK-05 | U | P2 | Unknown author | `/authors/Nobody%20E2E` | "Author not found" |
| BOOK-06 | H | P3 | Author with no books | Admin creates an author with no books | Page shows "No books in the catalogue yet." |
| BOOK-07 | H | P2 | Pending member on a book page | Pending member opens an available book | No Borrow button; note "You can borrow once an admin has approved your account." |
| BOOK-08 | H | P3 | Missing or broken cover | One book without a cover URL, one with a URL that does not load | The placeholder cover (with the title) is shown; no broken image |

### 5.9 Favourites (`FAV`)

| ID | Type | Pri | Scenario | Steps / data | Expected result |
|---|---|---|---|---|---|
| FAV-01 | H | P1 | Add a favourite | Heart on a book card | Heart fills; the book appears on `/favourites` |
| FAV-02 | H | P2 | Remove a favourite | Heart again, from a card and from `/favourites` | Book leaves the list |
| FAV-03 | H | P2 | Favourites persist | Add on the book page, log out, sign in through the form | Still in favourites (stored on the server) |
| FAV-04 | H | P2 | Per-user favourites | Member A adds a book; member B signs in | B's favourites do not include it |
| FAV-05 | H | P3 | Empty state | New member opens `/favourites` | Friendly empty state |
| FAV-06 | H | P3 | Admin favourites | Admin adds a favourite | Works (the route and API allow both roles) |
| FAV-07 | U | P3 | Favourite book deleted | Favourite a book; admin deletes it | It disappears from `/favourites`, no broken card |
| FAV-08 | U | P3 | Favourites fail to load | Stop Inventory, open `/favourites` | "Unable to load your favourites. Please try again." |

### 5.10 Borrow button and reserving (`RES`)

| ID | Type | Pri | Scenario | Steps / data | Expected result |
|---|---|---|---|---|---|
| RES-01 | H | P1 | Reserve an available book | Approved member → Borrow | Toast "Book reserved successfully! It is now pending admin approval."; the button becomes a disabled "Pending" (**Existing** for the toast; add the button state) |
| RES-02 | H | P1 | Pending everywhere | After RES-01, view the same book on a shelf, in search and on its page | All three show "Pending" |
| RES-03 | H | P1 | Borrowed after acceptance | Admin accepts; the member's window regains focus (or up to 30 s pass) | Button shows a disabled "Borrowed" |
| RES-04 | H | P2 | Back to Borrow after return | Admin records the return | Button returns to an active "Borrow" |
| RES-05 | H | P2 | Back to Borrow after rejection or cancellation | Admin rejects, or the member cancels | Button returns to "Borrow" |
| RES-06 | U | P1 | No copies on the shelf | Book with 0 available copies | Greyed, disabled "Borrow"; tooltip "No copies on the shelf right now"; not-allowed cursor (**Existing** for disabled) |
| RES-07 | U | P2 | Copies run out after the page loaded | Load the book; admin marks it unavailable through the API; click Borrow | Error "Book is currently unavailable for reservation."; nothing is queued |
| RES-08 | U | P2 | Same book twice | Second request for a book already pending (second tab with a stale page) | "You already have a pending reservation for this book." (**Existing**) |
| RES-09 | U | P2 | Admin clicks Borrow | Admin on a book page | The admin token counts as approved, so Borrow is shown; clicking it gives "Admins cannot reserve books. Please log in with a regular user account." **Record as a UX finding:** the button should be hidden for admins |
| RES-10 | U | P2 | Book deleted after the page loaded | Load the book; admin deletes it through the API; click Borrow | "Book with ID <n> not found in inventory." |
| RES-11 | U | P3 | Lending down | Stop Lending, click Borrow | "Failed to reserve book. Please try again."; the button returns to an active "Borrow" (not stuck on "Borrowing…") |
| RES-12 | U | P3 | User Service down while reserving | Stop the User Service (Lending checks approval live) | "Your account could not be verified right now. Please try again."; nothing queued |
| RES-13 | H | P3 | Double click | Double-click Borrow quickly | Exactly one reservation appears in the admin queue |
| RES-14 | H | P2 | Borrow a second, different book | Member already has one pending and one borrowed book; borrows a third | Allowed: the only limit is one open request per book |

### 5.11 My Borrowings (`MYB`)

| ID | Type | Pri | Scenario | Steps / data | Expected result |
|---|---|---|---|---|---|
| MYB-01 | H | P1 | Pending list | After RES-01 | Book listed under pending reservations with its date |
| MYB-02 | H | P1 | Current loans | After RES-03 | Book under current loans with checkout and due date (14 days later) (**Existing**) |
| MYB-03 | H | P2 | Estimated fine on an overdue loan | Backdate the checkout so the loan is 3 days overdue | Loan shows 3 days overdue and an estimated fine of Rs. 30.00 |
| MYB-04 | H | P2 | History | After return | Book in history with return date and fine (**Existing**) |
| MYB-05 | H | P1 | Cancel a pending reservation | Cancel → confirm | "Your reservation for "<title>" has been cancelled."; moves to the cancelled section (**Existing**) |
| MYB-06 | U | P2 | Cancel dialog dismissed | Cancel → Cancel the confirm | Nothing changes |
| MYB-07 | U | P2 | Cancel after the admin accepted | Stale page: admin accepts in another browser; member clicks Cancel | "This reservation is already being processed or has been accepted and cannot be cancelled. Refresh your list." |
| MYB-08 | U | P3 | Lending down | Stop Lending, open `/my-borrowings` | "Unable to load your borrowing records. Please try again." |
| MYB-09 | U | P3 | Token the service rejects | A token that looks valid to the page but has a bad signature, open `/my-borrowings` | "Your session has expired. Please log in again." |
| MYB-10 | H | P3 | Unpaid fines total | After an overdue return | The unpaid fines total includes the recorded fine |

### 5.12 Reservation queue, administration (`ADMRES`)

| ID | Type | Pri | Scenario | Steps / data | Expected result |
|---|---|---|---|---|---|
| ADMRES-01 | H | P1 | Queue shows the request | After RES-01, `/admin/reservations/pending` | Row with the member's name, book title and date (**Existing**) |
| ADMRES-02 | H | P1 | Accept | Accept | Row leaves the queue; loan appears on `/admin/borrowed`; available copies drop by 1 (**Existing**) |
| ADMRES-03 | H | P1 | Reject | Reject | Message "Reservation for <title> by <name> was cancelled. Notification event queued."; row goes to history as Cancelled; copies unchanged (**Existing**) |
| ADMRES-04 | U | P2 | Accept with no copy left | Book with 1 copy and two pending reservations from different members; accept both | First succeeds. Second shows "Inventory could not allocate a copy. Acceptance is pending; retry when a copy is available."; the row stays with "Retry Accept" and Reject is disabled |
| ADMRES-05 | H | P2 | Retry Accept after a copy returns | Continue ADMRES-04: return the first loan, click Retry Accept | Succeeds; the second member now has the loan |
| ADMRES-06 | U | P2 | Two admins, same reservation | Admin A accepts; Admin B (stale page) clicks Reject | Admin B sees "Only pending reservations can be rejected. Refresh the queue."; the queue refreshes and the row is gone |
| ADMRES-07 | U | P2 | Member cancelled meanwhile | Member cancels; admin (stale page) clicks Accept | "Only pending reservations can be accepted. Refresh the queue."; nothing is borrowed |
| ADMRES-08 | U | P3 | Inventory down during accept | Stop Inventory, Accept | "Inventory checkout could not be confirmed. Retry Accept to safely complete this reservation."; after a restart, Retry Accept completes it and exactly one copy is taken |
| ADMRES-09 | U | P3 | Queue fails to load | Stop Lending | "Unable to load pending reservations. Please try refreshing." |
| ADMRES-10 | H | P3 | Refresh button | Queue open; a member reserves; click Refresh | The new request appears without reloading the page |

### 5.13 Borrowed books, returns and fines (`LOAN`)

| ID | Type | Pri | Scenario | Steps / data | Expected result |
|---|---|---|---|---|---|
| LOAN-01 | H | P1 | On-time return | `/admin/borrowed` → Return → confirm (the dialog mentions Rs. 10 per day) | "Book returned. No fine is due."; loan goes to history; copies go up by 1 (**Existing**) |
| LOAN-02 | H | P1 | Late return | Backdate so the loan is 3 days overdue → Return | "Book returned. 3 day(s) overdue. Fine: Rs. 30.00 — Unpaid." (**Existing**) |
| LOAN-03 | H | P2 | Due just before Sri Lankan midnight | Due date 23:59 yesterday, Colombo time → Return | Fine Rs. 10.00 for 1 day: days are Sri Lanka calendar days, not 24-hour blocks |
| LOAN-04 | U | P2 | Return dialog cancelled | Return → Cancel | Loan stays |
| LOAN-05 | U | P2 | Return twice | Two admins; both click Return | The second request repeats the finished return (same message); only one return and one fine are recorded |
| LOAN-06 | U | P3 | Inventory down during return | Stop Inventory → Return | "Inventory return could not be confirmed. Retry Return; the original return date is preserved."; after a restart, Retry Return keeps the **original** return time |
| LOAN-07 | H | P2 | History filters | A Returned and a Cancelled record; filter Returned / Cancelled / All | Each filter shows only matching rows (**Existing** for two filters) |
| LOAN-08 | H | P3 | History columns | A late return | Days overdue, fine amount and fine status filled in; "—" for records without a fine |

### 5.14 Books and genres, administration (`ADMBOOK`)

| ID | Type | Pri | Scenario | Steps / data | Expected result |
|---|---|---|---|---|---|
| ADMBOOK-01 | H | P1 | Add a book | Add Book dialog: title, author, unique ISBN, genre, copies, cover URL, description | Success message; the book is searchable straight away (**Existing**) |
| ADMBOOK-02 | U | P2 | Missing required fields | Empty title / author / ISBN / genre | "Please fill in all required fields." (**Existing**) |
| ADMBOOK-03 | U | P2 | Negative copies | `-1` | The browser blocks it (`min=0`); no book is created (**Existing**) |
| ADMBOOK-04 | U | P2 | Duplicate ISBN | ISBN of an existing book (also with different letter case) | Conflict "A book with ISBN '<isbn>' already exists." (**Existing** for update; add for create) |
| ADMBOOK-05 | U | P3 | Invalid cover URL | `not a url` | Blocked by the browser (`type="url"`) |
| ADMBOOK-06 | U | P3 | Title over 200 characters | Add form with a 250-character title; Edit form typing the same | Add: "Failed to add book. Please verify your inputs and try again." (the server enforces 200; the Add form has no limit). Edit: the field stops at 200 |
| ADMBOOK-07 | H | P2 | Edit a book | Edit dialog → change title and description | "Successfully updated "<title>"."; changes show on the book page (**Existing**) |
| ADMBOOK-08 | U | P2 | Copies below current loans | 2 copies on loan → set total to 1 | "Total copies cannot be less than currently borrowed copies (2)." (**Existing**) |
| ADMBOOK-09 | H | P2 | Mark unavailable | Confirm "Mark … as unavailable now?" | ""<title>" is now marked as unavailable."; members see a greyed Borrow |
| ADMBOOK-10 | H | P2 | Make available again | Confirm | ""<title>" is available again with 1 copy." |
| ADMBOOK-11 | H | P1 | Delete a book with no loans | Remove → confirm | "Successfully removed "<title>"."; gone from search (**Existing**) |
| ADMBOOK-12 | U | P1 | Delete a book that is on loan | Book with an active loan → Remove | "A book cannot be removed while copies are currently borrowed." (**Existing**) |
| ADMBOOK-13 | U | P3 | Delete dialog cancelled | Remove → Cancel | Book remains |
| ADMBOOK-14 | H | P2 | Add, rename and delete a genre | Genre manager | The genre appears in the Add-book genre list; renaming and deleting work |
| ADMBOOK-15 | U | P2 | Duplicate genre | Existing name | "A genre named '<name>' already exists." |
| ADMBOOK-16 | U | P2 | Delete a genre in use | Genre assigned to a book | "This genre cannot be deleted while it is assigned to a book." |
| ADMBOOK-17 | U | P3 | Inventory down | Stop Inventory → Add Book | "Unable to connect to the Inventory Service. Please ensure it is running on port 5001." |
| ADMBOOK-18 | U | P3 | Kafka down while saving | Stop Kafka → Add Book | The book is saved, but the page reports that the Kafka event could not be published (Inventory publishes without an outbox, so no "new book" notification is sent later) |
| ADMBOOK-19 | H | P3 | Dialog focus | Open Add / Edit | Focus moves to the first field, not the close button; Esc closes the dialog |

### 5.15 Authors, administration (`ADMAUTH`)

| ID | Type | Pri | Scenario | Steps / data | Expected result |
|---|---|---|---|---|---|
| ADMAUTH-01 | H | P2 | Add an author | Name, portrait URL, biography | "Added <name>."; author page works |
| ADMAUTH-02 | H | P2 | Edit an author | Change the biography | "Saved <name>." |
| ADMAUTH-03 | H | P2 | Rename an author | New name | The name on their books changes too ("Renaming an author also renames it on their books.") |
| ADMAUTH-04 | U | P2 | Duplicate author | Existing name | "An author named '<name>' already exists." |
| ADMAUTH-05 | H | P2 | Clear the portrait URL | Edit, empty the portrait, save | Saves (empty is allowed; regression for an earlier `[Url]` bug) |
| ADMAUTH-06 | U | P3 | Non-web portrait link | `ftp://example.com/portrait.jpg` (the browser accepts it as a URL) | "Unable to add the author." (only `http(s)://` or empty is accepted); nothing created |
| ADMAUTH-07 | H | P2 | Remove an author | Remove → confirm | "Removed <name>."; their books stay; they leave any author list |
| ADMAUTH-08 | U | P3 | Remove dialog cancelled | Remove → Cancel | Nothing changes |

### 5.16 Curated lists, administration (`ADMLIST`)

| ID | Type | Pri | Scenario | Steps / data | Expected result |
|---|---|---|---|---|---|
| ADMLIST-01 | H | P2 | Create a book list | Name "E2E Picks" → key auto-filled as `e2e-picks`, type Books | "Created "E2E Picks". Add its books below." |
| ADMLIST-02 | H | P2 | Add, order and save items | Add three books, reorder, Save | Order saved; reopening shows the same order |
| ADMLIST-03 | H | P2 | Author list | Type Authors, add authors | Saved; the authors show where the list is used |
| ADMLIST-04 | U | P2 | Duplicate key | Key of an existing list (e.g. `top-100`) | "A list with the key 'top-100' already exists." |
| ADMLIST-05 | U | P3 | Invalid key | `a--b` or `-a-` (the input allows hyphens, the server requires single inner hyphens) | "Unable to create the list."; nothing created |
| ADMLIST-06 | U | P3 | Unsaved changes | Change the items, close the dialog | "Discard the unsaved changes?" prompt; Cancel keeps the edits |
| ADMLIST-07 | U | P3 | Every item already on the list | Add all books | "Every one of the books is already on this list." |
| ADMLIST-08 | H | P2 | Rename a list | New name | "Renamed to "<name>"." |
| ADMLIST-09 | H | P2 | Delete a list | Confirm | "Deleted "<name>"."; the list is gone and its books remain |
| ADMLIST-10 | H | P3 | Item search | Search for an item that is not in the catalogue | "No books match that search." |

### 5.17 Notifications, end to end through Kafka (`NOTIF`)

Each case triggers the action in one browser and checks the result in another (or after switching
user), waiting up to 30 s for the event to pass through Kafka.

| ID | Type | Pri | Scenario | Trigger | Expected result |
|---|---|---|---|---|---|
| NOTIF-01 | H | P1 | Admins hear about a new reservation | Member reserves | Admin bell count +1; message "<member name> reserved '<title>' on <date>." |
| NOTIF-02 | H | P1 | Member hears about acceptance | Admin accepts | Member: "Your reservation for '<title>' was accepted. Please return it by <date>." |
| NOTIF-03 | H | P2 | Member hears about rejection | Admin rejects | Member: "Your reservation for '<title>' was rejected by the library."; admins get nothing |
| NOTIF-04 | H | P2 | Member cancels | Member cancels | Member: "You cancelled your reservation for '<title>'."; admins: "<name> cancelled their pending reservation for '<title>'." |
| NOTIF-05 | H | P2 | Return | Admin records the return | Member: "'<title>' was returned on <date>. Thank you!" |
| NOTIF-06 | H | P2 | Fine | Late return (LOAN-02) | Member also gets "A fine of Rs. 30.00 was recorded for '<title>': returned 3 days late." |
| NOTIF-07 | H | P3 | Due-soon reminder | Move a loan's due date to 2 days ahead; restart Lending (the check runs at start-up, then hourly) | Member: "Reminder: '<title>' is due on <date>. Please return it on time to avoid a fine."; still one reminder after another restart |
| NOTIF-08 | H | P2 | New book announcement | Admin A adds a book | Approved members: "New in the catalogue: '<title>' by <author>."; Admin B: "<admin email> added '<title>' to the catalogue."; Admin A gets nothing |
| NOTIF-09 | H | P3 | Book edited / availability | Admin A edits, marks unavailable, marks available | Admin B gets "…updated…", "…marked … as unavailable.", "…marked … as available."; members get nothing |
| NOTIF-10 | H | P2 | Deleted book, member waiting | Member has a **pending** reservation; admin deletes the book | That member: "'<title>', a book on your reservations or borrowings, has been removed from the library catalogue."; other members get nothing |
| NOTIF-11 | U | P3 | Members registered after a new book | Book added, then a member registers and is approved | The new member does **not** see the earlier "New in the catalogue" announcement |
| NOTIF-12 | H | P1 | Mark one as read | Notifications page → Mark as read | It leaves the unread list; the bell count drops at once |
| NOTIF-13 | H | P2 | Mark all as read | Mark all as read | Unread list empty ("No unread notifications…"); bell shows 0 |
| NOTIF-14 | H | P2 | Show / hide viewed | Show Viewed → History section with read items; Hide Viewed hides it; Show again restores it without a reload | Toggle label switches; "Load more history" pages in groups of 20 and disappears at the end |
| NOTIF-15 | H | P3 | Read status is per admin | Admin A marks a notification read | It stays unread for Admin B |
| NOTIF-16 | H | P3 | Bell and page stay in sync | On the Notifications page, "Mark all as read" in the bell | The page empties without a reload |
| NOTIF-17 | U | P2 | Notification Service down | Stop it, open the bell / page | Bell: "Notifications are unavailable right now."; page: "Your notifications could not be loaded." with Retry |
| NOTIF-18 | U | P2 | Kafka down, then back | Stop Kafka; member reserves (succeeds); start Kafka | No notification while Kafka is down; once it is back, the admin notification arrives (Lending's outbox resends; nothing is lost) |
| NOTIF-19 | U | P3 | Notification Service restarted mid-stream | Restart it while events are being produced | No duplicate notifications (redelivered events are skipped by event ID) |

### 5.18 Cross-cutting (`X`)

| ID | Type | Pri | Scenario | Expected result |
|---|---|---|---|---|
| X-01 | H | P2 | Phone width (390 px): login, register, home, search, book, My Borrowings, admin Borrowed books | No horizontal page scroll; the admin table scrolls inside its container |
| X-02 | H | P3 | Keyboard only: sign in, Tab to Borrow and press Enter, close a dialog with Esc | All reachable; the focused button shows a focus ring |
| X-03 | H | P3 | Reduced motion (emulate `prefers-reduced-motion`) | No shimmer or slider animation |
| X-04 | U | P3 | Browser console | No uncaught errors on the main pages during the P1 journey |
| X-05 | U | P3 | Script injection through data | A book titled `<img src=x onerror=alert(1)>` is shown as text on cards, the book page, the admin queue and in notifications; no alert fires |
| X-06 | H | P3 | Dates and times | Dates show in Sri Lanka time (`Asia/Colombo`), e.g. "14 Oct 2026, 10:39" |

---

## 6. P1 smoke journey (one connected run)

The fastest test that proves the system works end to end. Run it on every PR.

1. Register member M (REG-01), then admin approves M (ACC-01).
2. M signs in (AUTH-01), searches for book B (SRCH-01), opens it (BOOK-01), favourites it (FAV-01).
3. M reserves B (RES-01), then the button shows Pending (RES-02).
4. The admin bell shows the reservation (NOTIF-01). Admin accepts it (ADMRES-02).
5. M sees Borrowed (RES-03), the loan in My Borrowings (MYB-02), and the acceptance notification (NOTIF-02).
6. Admin records the return (LOAN-01). M sees the returned notification and the book back in history.
7. Admin tries to delete a book that is on loan, which is refused (ADMBOOK-12), and deletes a book with no loans (ADMBOOK-11).
8. Member and admin each try the other role's pages, and are redirected (RBAC-02, RBAC-03). Both log out (AUTH-09).

## 7. Entry and exit criteria

**Entry:** all containers are healthy (`/health` is 200 on 5050, 5001, 5148 and 5003); the Kafka
consumer group `notification-service` has no lag; the frontend is served from `http://localhost:5173`
and pointed at the local services.

**Exit:** every P1 case passes; at least 95% of P2 cases pass, with failures logged as GitHub issues; P3
failures are triaged. No open defect of severity "major" or above stays on the core journey.

## 8. Coverage matrix

| Feature | Cases | Happy | Unhappy | Already automated | New |
|---|---|---|---|---|---|
| Authentication (`AUTH`) | 15 | 7 | 8 | 3 | 12 |
| Registration (`REG`) | 9 | 4 | 5 | 4 | 5 |
| Access control (`RBAC`) | 6 | 2 | 4 | 4 | 2 |
| Member accounts (admin) (`ACC`) | 9 | 6 | 3 | 1 | 8 |
| Profile (`PROF`) | 7 | 5 | 2 | 4 | 3 |
| Discover / catalogue (`CAT`) | 8 | 7 | 1 | 1 | 7 |
| Search (`SRCH`) | 13 | 9 | 4 | 0 | 13 |
| Book and author pages (`BOOK`) | 8 | 6 | 2 | 0 | 8 |
| Favourites (`FAV`) | 8 | 6 | 2 | 0 | 8 |
| Reserving (`RES`) | 14 | 7 | 7 | 3 | 11 |
| My Borrowings (`MYB`) | 10 | 6 | 4 | 3 | 7 |
| Reservation queue (`ADMRES`) | 10 | 5 | 5 | 3 | 7 |
| Returns and fines (`LOAN`) | 8 | 5 | 3 | 3 | 5 |
| Books and genres (`ADMBOOK`) | 19 | 7 | 12 | 8 | 11 |
| Authors (`ADMAUTH`) | 8 | 5 | 3 | 0 | 8 |
| Curated lists (`ADMLIST`) | 10 | 6 | 4 | 0 | 10 |
| Notifications (`NOTIF`) | 19 | 15 | 4 | 0 | 19 |
| Cross-cutting (`X`) | 6 | 4 | 2 | 0 | 6 |
| **Total** | **187** | **112** | **75** | **37** | **150** |

## 9. Not tested, and why

These would be natural tests for a library system, but the current system does not have the behaviour,
so a test would only fail by design. Each one is a candidate feature or backlog item, not a test case.

| Idea | Why it is not a test |
|---|---|
| Paying a fine | No payment endpoint or UI; fines stay "Unpaid" |
| Maximum loans per member | No limit exists; only one open request per book (RES-14) |
| Password reset, email verification, "remember me" | Not implemented |
| Registering an admin | Admins are the built-in account or set in the database |
| Members renewing a loan | No renewal endpoint |
| Waiting list when no copies are left | Borrow is disabled at 0 copies; there is no queue to join |
| Lending cancelling reservations of a deleted book | It does not; the pending reservation stays. Recorded as a finding (§10) |

## 10. Findings made while writing this plan

These came out of reading the code for the plan. They are worth raising with the team; the related
test cases assert today's behaviour and should be updated if the behaviour changes.

1. **Admins see a Borrow button they can never use** (RES-09). `useBorrow` allows anyone whose token says
   "approved", and the admin token always does. The server then refuses with "Admins cannot reserve
   books…". Hiding the button for admins would fix it.
2. **A revoked member keeps the Borrow button until they sign in again** (ACC-06). The server correctly
   refuses (it checks approval live), but the UI promises an action it cannot complete.
3. **Deleting a book leaves its pending reservations open** (§9). Members are told the book was removed,
   but the request stays in the admin queue, and accepting it then fails with "Inventory could not
   allocate a copy…", leaving it stuck in "Retry Accept".
4. **A reservation stuck in "Accepting" cannot be rejected** (ADMRES-04). If no copy ever comes back,
   it has no way out of the queue.
5. **Inventory publishes events without an outbox** (ADMBOOK-18). If Kafka is down when a book is added,
   edited or deleted, those notifications are lost for good (Lending's events are not).
6. **The built-in admin's email can be registered as a member** (REG-07). Sign-in with that email
   then depends on which password is used.
7. **Revoking a member always fails** (ACC-04, found while implementing). `AdminUsersPage.tsx` sends the
   revoke request to a hard-coded `http://localhost:5000` instead of the configured User Service address,
   so the admin sees "Failed to revoke user access. Please try again." locally (port 5050) and on Azure.
   Accept and Reject use the configured address and work.
   **Fixed** on branch `fix/revoke-member-url` (commit f6e9b26), which uses the configured address; with it ACC-04
   passes (TEST-CATALOG.md, run 2).
8. **The Add book form has no length limits** (ADMBOOK-06). The Edit form stops typing at the server's limits
   (title 200, author 150, ISBN 30); the Add form lets anything through and then shows the generic
   "Failed to add book…" message when the server rejects it.
9. **Test configuration:** `LendingSeleniumTestBase` defaults the User Service to port 5000; locally it
   runs on 5050, so `SELENIUM_USER_SERVICE_URL` must be set.

## 11. Running the suite

The suites are implemented in this folder as four xUnit projects, one per service, plus a shared support
library. README.md gives the commands, the settings and the latest results; TEST-CATALOG.md lists every
case with its test name and result.

Each test is tagged `[Trait("Id", "RES-01")]`, `[Trait("Priority", "P1")]` and `[Trait("Type", "Happy")]`, so a
run can be limited to one case, the smoke set (`--filter "Priority=P1"`) or the unhappy paths. Tests in a
project never run in parallel, and the projects run one after another, because the outage cases stop
shared containers.
