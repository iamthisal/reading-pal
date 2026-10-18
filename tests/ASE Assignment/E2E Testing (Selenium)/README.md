# ASE assignment: end-to-end testing with Selenium

## Verified coverage and results

The four projects were run one after another on 18 October 2026 against the local Docker stack (.NET SDK
10.0.401, Chrome 155, headless): **187 test cases (112 happy, 75 unhappy), 198 executed runs, 197 passed,
1 failed, 0 skipped.** Theory rows explain why executed runs exceed cases. The one failure is a real defect
(below), not a broken test. Every outage case stopped and restarted its container successfully, and the
test data cleanup left no E2E books, authors or lists behind.

| Project | Cases | Executed | Passed | Failed | Run time |
| --- | ---: | ---: | ---: | ---: | ---: |
| UserService.E2ETests | 46 | 55 | 54 | 1 | 5 min |
| InventoryService.E2ETests | 80 | 82 | 82 | 0 | 10 min |
| LendingService.E2ETests | 42 | 42 | 42 | 0 | 4 min |
| NotificationService.E2ETests | 19 | 19 | 19 | 0 | 5 min |
| Total | 187 | 198 | 197 | 1 | about 25 min |

The failing case is **ACC-04** (an admin revokes an active member): it asserts the required behaviour and
fails because of finding 1 below.

Each case is described in [TEST-PLAN.md](TEST-PLAN.md) and listed with its result in
[TEST-CATALOG.md](TEST-CATALOG.md). TRX files are in each project's ignored `TestResults` folder.

## What is tested

The whole running system through a real Chrome browser: the React frontend, the four .NET services, MySQL
and Kafka. Every case was written against the current code on `develop`, and expected results quote the
messages the app actually shows.

| Project | Areas (TEST-PLAN.md section) |
| --- | --- |
| UserService.E2ETests | Sign-in and sessions (5.1), registration (5.2), role-based access (5.3), approving / rejecting / revoking members (5.4), profile (5.5) |
| InventoryService.E2ETests | Discover page (5.6), search (5.7), book and author pages (5.8), favourites (5.9), admin books and genres (5.14), authors (5.15), curated lists (5.16), cross-cutting checks (5.18) |
| LendingService.E2ETests | Borrow button and reserving (5.10), My Borrowings (5.11), the admin reservation queue (5.12), returns, fines and history (5.13) |
| NotificationService.E2ETests | Notifications through Kafka for members and admins, the bell and the Notifications page (5.17) |
| ReadingPal.E2E.Support | Shared code: browser helpers, API set-up client, database and Docker helpers, test base class |

**Happy paths** check that each feature does what it should. **Unhappy paths** check refusals (invalid input,
the wrong role, duplicate requests), races between two people (two admins, a member and an admin), and
outages: the tests stop or pause the Inventory, Lending, User and Notification containers and Kafka, check
the exact message the UI shows, then start them again.

Behaviour the system does not have (paying fines, loan limits, password reset…) is not tested; TEST-PLAN.md
§9 lists it so no test fails by design.

## Tools and why

- **Selenium WebDriver 4.48** with **ChromeDriver** (downloaded automatically by Selenium Manager) drives a real browser, the same version as the repository's existing Selenium suites.
- **xUnit 2.9.3** runs the tests. `Fact` is one case; `Theory` with `InlineData` runs one case over several inputs (for example every required registration field). Each test carries `Id`, `Priority` and `Type` traits, so a run can be limited to one case, the P1 smoke set or the unhappy paths.
- **MySqlConnector 2.4** is used only for the two set-ups the UI cannot do: promoting a second admin, and moving a loan's dates into the past for fines and reminders.
- **The Docker CLI** stops, pauses and starts containers for the outage cases.
- **.NET 10 SDK**, like the services.

## How the tests work

- **Arrange through the API, act and assert in the browser.** Members, books, loans and lists are created through the REST APIs with an admin token in a second or two; the behaviour under test is then done in Chrome, the way a person would.
- **Signing in:** the sign-in cases type into the form; every other test stores the API token in the page's local storage, which is what the login page does, and saves several seconds per test.
- **Isolation:** every test makes its own data with a random suffix (`E2E Book 3f2a1b9c`). Afterwards the base class rejects its open requests, returns its loans and deletes its books, authors, lists, genres and members, even when the test failed.
- **Waiting:** only explicit waits on a condition, never fixed sleeps, except where a test proves that something does *not* happen (for example no duplicate notifications). Kafka results are polled for up to 45 seconds.
- **Outages:** a stopped container is always started again after the test, even if it fails.
- **One at a time:** tests in a project never run in parallel (they share one system), and the projects are run one after another.

Two fixtures are kept between runs on purpose: the genre `E2E Testing` and a second admin account,
`e2e.admin.b@library.test`, made by registering a member and setting its role in the database.

## Run the tests

### GitHub Actions

The shared **ASE Assignment Tests** workflow detects this folder's run-ci.ps1 and runs the UI job on
ubuntu-24.04. The entry point builds all four projects, starts a disposable local MySQL/Kafka/four-API
stack using the repository Compose files, builds and serves the frontend, waits for readiness and runs
the projects sequentially. Each project's TRX file and infrastructure logs are uploaded as
ase-ui-results. An assertion failure fails the job; remaining projects still run so their results are saved.

CI uses User Service port 5000 and explicitly sets all four frontend/API addresses. The local defaults
below remain unchanged. The existing documented application findings are not suppressed or excluded.
The CI runner cleans up its own containers and volumes in a finally block. A developer run must use
an existing prepared stack explicitly:

~~~powershell
pwsh -NoProfile -File "tests/ASE Assignment/E2E Testing (Selenium)/run-ci.ps1" -ResultsDirectory ".ase-results/ui" -ApplicationCommit "$(git rev-parse HEAD)" -UseExistingStack
~~~

Existing-stack mode builds/runs the tests but does not provision/reset infrastructure. The tests themselves
still exercise outages and modify test data, so this mode must target a local test stack as described below.

Start the local stack first (see TEST-PLAN.md §2): Kafka, MySQL and the four services in Docker with
`ASPNETCORE_ENVIRONMENT=Development`, and the frontend with `npm run dev` pointed at the local services by
`frontend/.env.local`. Chrome must be installed.

From the repository root, run the four projects one after another (macOS / Linux):

~~~bash
for project in UserService InventoryService LendingService NotificationService; do
  dotnet test "tests/ASE Assignment/E2E Testing (Selenium)/$project.E2ETests/$project.E2ETests.csproj" \
    --logger "trx;LogFileName=$project.E2ETests.trx" || exit 1
done
~~~

PowerShell:

~~~powershell
foreach ($project in "UserService", "InventoryService", "LendingService", "NotificationService") {
    dotnet test "tests/ASE Assignment/E2E Testing (Selenium)/$project.E2ETests/$project.E2ETests.csproj" --logger "trx;LogFileName=$project.E2ETests.trx"
    if ($LASTEXITCODE -ne 0) { throw "Tests failed: $project" }
}
~~~

Useful filters:

~~~bash
dotnet test "tests/ASE Assignment/E2E Testing (Selenium)/LendingService.E2ETests/LendingService.E2ETests.csproj" --filter "Priority=P1"
dotnet test "tests/ASE Assignment/E2E Testing (Selenium)/UserService.E2ETests/UserService.E2ETests.csproj" --filter "Id=ACC-04"
dotnet test "tests/ASE Assignment/E2E Testing (Selenium)/InventoryService.E2ETests/InventoryService.E2ETests.csproj" --filter "Type=Unhappy"
~~~

Rebuild the catalogue from the latest TRX files:

~~~bash
python3 "tests/ASE Assignment/E2E Testing (Selenium)/scripts/build-catalog.py"
~~~

| Setting | Default | Purpose |
| --- | --- | --- |
| `E2E_FRONTEND_URL` | `http://localhost:5173` | The React app |
| `E2E_USER_SERVICE_URL` | `http://localhost:5050` | User Service (5000 is taken by macOS AirPlay) |
| `E2E_INVENTORY_SERVICE_URL` | `http://localhost:5001` | Inventory Service |
| `E2E_LENDING_SERVICE_URL` | `http://localhost:5148` | Lending Service |
| `E2E_NOTIFICATION_SERVICE_URL` | `http://localhost:5003` | Notification Service |
| `E2E_ADMIN_EMAIL`, `E2E_ADMIN_PASSWORD` | the Development admin | The built-in admin |
| `E2E_MYSQL_CONNECTION` | read from the repository's `.env` | MySQL server and credentials, without a database name |
| `E2E_HEADLESS` | `true` | Set to `false` to watch Chrome |

The outage cases stop local Docker containers, so run the suites only against a local stack, never a
shared or deployed one.

## Simple explanations

**End-to-end test:** a test that uses the finished system the way a person does (open a page, type, click,
read what appears), with every real service, database and message broker running behind it.

**Page element, selector and wait:** Selenium finds things on the page with selectors: an element `id`
(`#email`), the label a screen reader reads (`aria-label="Borrow Dune"`) or visible text. The page changes
as data arrives, so a test waits until the element or text it needs is there, instead of pausing for a
fixed time.

**Happy and unhappy path:** a happy path is the normal use of a feature. An unhappy path is everything
else the system must handle: wrong input, a refused action, two people changing the same thing, or a
service that is down.

**Arrange, Act, Assert:** each test first prepares what it needs (through the API), then does one thing in
the browser, then checks what the browser shows and, where it matters, what the system stored.

## Findings

Writing and running the suite found real problems; the related tests assert the *correct* behaviour and
fail, or document today's behaviour where the plan says so. Details are in TEST-PLAN.md §10.

1. **Revoking a member always fails** (ACC-04 fails). `frontend/src/pages/AdminUsersPage.tsx` sends the revoke request to a hard-coded `http://localhost:5000` instead of the configured User Service address, so the admin sees "Failed to revoke user access. Please try again." locally and on Azure.
2. **Admins see a Borrow button the service always refuses** (RES-09 documents it).
3. **A revoked member keeps the Borrow button until they sign in again**; the service correctly refuses (ACC-06).
4. **Deleting a book leaves its pending reservations open**, and a reservation stuck on "Retry Accept" cannot be rejected (ADMRES-04).
5. **Inventory publishes its events without an outbox**, so book notifications made while Kafka is down are lost (ADMBOOK-18).
6. **The Add book form has no length limits** (the Edit form has them), so overlong input only fails with a generic message (ADMBOOK-06).
7. **The built-in admin's email can be registered as a member** (REG-07).
