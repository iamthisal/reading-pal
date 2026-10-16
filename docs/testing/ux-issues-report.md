# Reading Pal (Library Management System) - UX Issues Report

**Site reviewed:** https://polite-water-0c0e68a00.3.azurestaticapps.net
**Date:** 2026-09-30
**Reviewed as:** Administrator (`admin@library.com`), desktop Chrome, viewport about 1393 x 845
**Method:** Manual walkthrough of every page reachable from the admin sidebar, plus the member-facing Discover page ("Public catalogue"), the Login page and the Register page. Modals, menus, search, filters, tables and error states were opened and exercised. No data was intentionally created, changed or deleted (see "Not tested").

## 1. Summary

| ID | Issue | Page(s) | Severity |
|---|---|---|---|
| UX-01 | "Close" button in User Details modal is nearly invisible; Escape and outside-click do not close it | Active users, User approvals | High |
| UX-02 | Manage Genres and Edit Book popups render as unstyled panels stuck in the top-left corner | Book inventory | High |
| UX-03 | Refreshing or opening a protected URL directly sends a signed-in user to the login page | All protected pages | High |
| UX-04 | Log out and the way back to the admin dashboard sit at the very bottom of a non-sticky sidebar | All pages, worst on Discover | High |
| UX-05 | Tables are clipped: action buttons and status columns are cut off or hidden until you scroll sideways | Book inventory, Borrowed books | High |
| UX-06 | Reserve Book buttons shown to admins always fail, with the error shown far from the button | Discover (from admin) | Medium |
| UX-07 | "Refresh" button is almost invisible | Pending reservations, Borrowed books, Reservation history | Medium |
| UX-08 | Text styled as links that do nothing, and controls that look interactive but are not | Several pages | Medium |
| UX-09 | Long book titles overflow their cards and cause sideways page scroll | Discover | Medium |
| UX-10 | "My Library" and "Favorite" go nowhere (same anchor as the recommendations list) | Discover | Medium |
| UX-11 | Create Account form is pre-filled with saved sign-in credentials; Register and Login look like different products | Register | Medium |
| UX-12 | Decorative right-hand band overlaps content cards and tables | All admin pages | Low |
| UX-13 | Search jumps the page away from the search box; empty state has no action | Discover | Low |
| UX-14 | Placeholder "Classified admin message" shown on the dashboard | Admin dashboard | Low |
| UX-15 | Inconsistent names for the same thing | Sidebar, page titles, dashboard cards | Low |
| UX-16 | Inconsistent date and time formats | Several pages | Low |
| UX-17 | Reservation history cannot tell a rejection from a user cancellation | Reservation history | Low |
| UX-18 | Browser tab title is "frontend" on every page | All pages | Low |
| UX-19 | Unknown URLs show the dashboard instead of a "page not found" message | All | Low |
| UX-20 | Long lists have no pagination, sorting or filtering; category filter list is unbounded | Several pages | Low |
| UX-21 | Dashboard cards show no counts and are not exhaustive | Admin dashboard | Low |
| UX-22 | Login page has no show-password toggle or "forgot password" link | Login | Low |
| UX-23 | Loading states show misleading values or bare text | User approvals, Pending reservations | Low |

## 2. Detailed findings

### UX-01 - User Details modal has no visible way to close it (High)
**Where:** Active users and User approvals, after clicking a user's name.
**Steps:** Open Active users, click any user name. The User Details modal opens. Look for a way to dismiss it.
**Actual:**
- There is no "X". A "Close" button exists (it is present in the accessibility tree) but is drawn in white text on a near-white background, so it is effectively invisible; it sits next to the bright red "Revoke Access" button, which draws the eye instead.
- Pressing Escape does nothing.
- Clicking the dimmed area outside the modal does nothing.
- The same invisible Close button is on the pending-user modal, where the red "Reject" button is a different size from the wide blue "Accept User" button.
- Inside the modal, "Current Borrowings" and "Past Borrowings" use light text on a grey card, which is hard to read.
**Expected:** A clearly visible close control (X and/or a styled Close button), Escape and outside-click to close, readable text.
**Impact:** Users hunt for the exit and may trigger "Revoke Access" or "Reject" by mistake.

### UX-02 - Manage Genres and Edit Book popups are broken in layout (High)
**Where:** Book inventory, "Manage Genres" button and the pencil (edit) icon on any book.
**Steps:** Open Book inventory, click "Manage Genres" or a pencil icon.
**Actual:**
- The popup appears as a dark, narrow panel pinned to the top-left corner of the browser, on top of the sidebar and the page title, with no dimmed backdrop, instead of a centred modal.
- In Manage Genres the genre names are dark grey text on the dark panel and are hard to read.
- Escape does not close Manage Genres; only the small "x" works. Edit Book has an "x" and a "Cancel" button.
**Expected:** A centred modal matching the User Details modal style, readable text, Escape and outside-click to close.

### UX-03 - Refresh or direct URL logs the user out of the UI (High)
**Where:** Any protected page (for example `/admin/reservations/pending`).
**Steps:** While signed in, reload the page or open the page's URL directly (bookmark, shared link, new tab).
**Actual:** The sign-in page appears even though the login token is still stored in the browser, and it stays there. Reproduced twice on the deployed site. Navigating inside the app (sidebar links) works.
**Expected:** The user stays on the requested page.
**Note:** This matches the `ProtectedRoute` defect already documented in the test results (section 8.1).

### UX-04 - Log out and "back to dashboard" are buried at the bottom (High)
**Where:** Every page.
**Actual:**
- The sidebar is not sticky. It scrolls away with the page, so on long pages (Book inventory, Discover) the navigation disappears and you must scroll back to the top to move anywhere.
- "Log out" is at the bottom of the sidebar. On the admin dashboard it is below the visible area on a normal screen, and on the Discover page you must scroll the entire book grid to reach it.
- When an admin opens "Public catalogue" (the member Discover page), the sidebar shows only Discover, My Library and Favorite. The only link back to the admin dashboard is a "Dashboard" link at the very bottom of the page, so the natural way back is the browser's back button.
**Expected:** A sticky sidebar or header with Log out and a persistent link back to the admin area.

### UX-05 - Table content is clipped or hidden (High)
**Where:** Book inventory, Borrowed books.
**Actual:**
- Book inventory: the Actions column (edit and delete icons) is cut off at the right edge of the card and the column header reads "ACT".
- Borrowed books: the Status column is cut off ("Overdu", "Borrow") and the Return button is not visible at all until you scroll the table sideways. Nothing signals that the table scrolls.
**Expected:** Columns fit or wrap, or there is a visible horizontal scroll cue; primary actions (Return, Edit, Delete) are always visible.

### UX-06 - Admins are offered "Reserve Book" and it always fails (Medium)
**Where:** Discover page opened from the admin sidebar ("Public catalogue").
**Steps:** Click "Reserve Book" on any card.
**Actual:** The button shows "Reserving..." and then the message "Admins cannot reserve books. Please log in with a regular user account." appears at the top of the grid, far above the clicked button on a long page. (This happened once during the review by an accidental click; the app blocked it and no copies were taken.)
**Expected:** Hide or disable Reserve for admins, or show the message next to the button.

### UX-07 - "Refresh" button is barely visible (Medium)
**Where:** Pending reservations, Borrowed books, Reservation history (top right).
**Actual:** White text on the beige decorative band, almost unreadable. The same control shows a faint "Loading..." pill on first load.
**Expected:** A button with sufficient contrast.

### UX-08 - Things that look clickable but are not (Medium)
- Book titles (red, bold) in Pending reservations and Borrowed books are plain text, not links.
- User names (red, bold) in Reservation history do nothing when clicked, unlike user names on Active users, which open a modal.
- On Discover, book covers and titles are not clickable, so there is no book detail view.
- The account chip in the Discover header shows a dropdown arrow, but clicking the email or the arrow opens nothing. The notification bell shows no visible response when clicked.
- The "Approval queue" and "Member directory" chips at the top right of the user pages look like buttons but are not interactive.
- On the admin dashboard, the admin email chip is not interactive.

### UX-09 - Long titles overflow book cards (Medium)
**Where:** Discover.
**Actual:** Long titles are drawn across the cover image and spill over neighbouring cards, overlapping text. The overflow also creates a horizontal scrollbar for the whole page. Card contents are not vertically aligned across a row (Reserve buttons sit at different heights).
**Expected:** Truncate with ellipsis or wrap inside the card.

### UX-10 - "My Library" and "Favorite" do not lead anywhere (Medium)
**Where:** Discover sidebar.
**Actual:** Both links point to the same in-page anchor as the recommendations list (`#recommendations`); "Discover" points to `#discover`. No separate pages or content exist for them.
**Expected:** Working pages, or remove the links.

### UX-11 - Register page issues (Medium)
**Where:** Register ("Create Account").
**Actual:**
- On opening the form, the browser had filled Email and Password with the saved admin sign-in credentials, so a new user could easily register with the wrong details. The fields appear to lack hints such as `autocomplete="new-password"` or `autocomplete="off"`. (The form was not submitted.)
- The page uses a dark theme with a centred card, while Login uses a light theme with a photo panel, so the two look like different products.
**Expected:** Autocomplete attributes set correctly; consistent visual design.

### UX-12 - Decorative band overlaps content (Low)
**Where:** All admin pages.
**Actual:** A beige vertical band on the right edge overlaps cards and table rows, so the right part of every card appears tinted differently from the rest.

### UX-13 - Search behaviour on Discover (Low)
**Actual:** Submitting a search scrolls the page down past the search box, so you cannot see or edit what you typed. With no results the message "No books match your search." has no button; the "Clear filters" link is up in the header, out of view. After clearing, the page layout shifts, which made it easy to click the wrong card.
**Expected:** Keep the search box in view and put "Clear search" in the empty state.

### UX-14 - Placeholder message on the dashboard (Low)
**Actual:** The dashboard ends with a "Classified admin message" panel reading "Hello Admin! This is a highly classified message from the backend that only administrators can see." It looks like leftover development text.

### UX-15 - Inconsistent naming (Low)
The same screen is called "User approvals" in the sidebar, "Pending Approvals" as the page title, and "Pending requests" on the dashboard card. The member catalogue is "Public catalogue" for admins and "Discover" on the page itself. Sidebar labels for "Pending reservations" and "Reservation history" wrap onto two lines.

### UX-16 - Inconsistent date and time formats (Low)
- Pending reservations and Borrowed books: `Sep 30, 2026, 02:29:13 AM GMT+5:30`
- Reservation history: `9/30/2026, 2:36:43 AM`
- User details modal: `9/7/2026 at 12:52:57 PM`
- Users lists: `9/7/2026`

### UX-17 - Reservation history cannot separate rejections from cancellations (Low)
**Actual:** A "Cancelled" badge covers both admin rejections and user cancellations, and the page itself says the cancellation date and who cancelled are not recorded. "Returned" and "Cancelled" badges use similar blue chips. The Status filter is an unstyled native dropdown that does not match the rest of the design.

### UX-18 - Tab title is "frontend" (Low)
The browser tab reads "frontend" on every page (verified on login, admin and member pages), which makes tabs and history entries indistinguishable.

### UX-19 - No "page not found" screen (Low)
Opening an unknown admin URL shows the admin dashboard content with no message explaining that the page does not exist.

### UX-20 - Long lists have no controls (Low)
User approvals (19 rows), Active users, Book inventory (28 rows) and Discover (28 books) show everything on one page with no pagination and no sorting. Only Book inventory and Discover have search. The Discover category filter is a long, unbounded list of genres (including "Self development" and "Self Development" as separate entries) with no collapse or scroll limit.

### UX-21 - Dashboard cards (Low)
The heading asks "What needs your attention?" but the cards show no counts (for example how many approvals or reservations are pending), even though the data exists on the destination pages. "6 tools available" does not match the sidebar, which lists 8 destinations (Reservation history has no card).

### UX-22 - Login page (Low)
There is no show/hide password toggle and no "forgot password" link.

### UX-23 - Loading states (Low)
User approvals shows "0 members" next to "Loading users..." before the real count (19) arrives. Pending reservations shows a "Loading..." pill and, briefly, a different font. Neither indicates progress clearly.

## 3. What was not tested
To avoid changing live data, the following were not clicked or submitted, so their confirmation and error behaviour is unverified:
- Accept, Reject, Return, Delete, Revoke Access and Save Changes buttons, and the Add Book, Add Genre, Login and Register submit buttons.
- Whether destructive actions ask for confirmation.
- The regular member ("User" role) experience beyond the shared Discover page, because it needs a separate sign-in. My Borrowings, cancellation and fine display were not seen.
- Mobile and tablet layouts, keyboard-only navigation, screen-reader behaviour and other browsers.

## 4. Suggested fix order
1. Modals: UX-01, UX-02 (visible close, consistent styling, Escape and outside-click).
2. Navigation: UX-03, UX-04 (session on refresh, sticky sidebar with Log out and a link back to admin).
3. Tables: UX-05, UX-07 (no clipped actions, visible Refresh).
4. Remove or complete dead controls: UX-06, UX-08, UX-10.
5. Polish: UX-09, UX-11 to UX-23.
