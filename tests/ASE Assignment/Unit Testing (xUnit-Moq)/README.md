# ASE assignment: unit testing and mocking

## Verified coverage and results

All four projects built and passed on 18 October 2026 using .NET SDK 10.0.401: **71 test methods, 120 executed cases, 0 failures, 0 skipped**. Theory rows explain why executed cases exceed method counts. The broader services have extra examples because their rules span several classes.

| Service | Test class | Methods | Executed cases | What it covers |
| --- | --- | ---: | ---: | --- |
| User | AdminControllerTests | 5 | 7 | Approval, rejection, revocation, invalid IDs, protected admin accounts |
| User | AuthControllerTests | 5 | 6 | Registration, duplicate emails, password checks, tokens, JWT dependency calls |
| User | UserControllerTests | 5 | 7 | Profile changes, optional passwords, duplicate emails, invalid/missing identities |
| Inventory | BooksControllerTests | 8 | 10 | Creation, ISBN duplicates, borrowed-copy boundaries, optional description, deletion, unavailability, invalid model state |
| Inventory | BookSearchControllerTests | 2 | 6 | Page-size defaults/limits and available-only filtering |
| Inventory | CatalogueRulesTests | 6 | 6 | Genre deletion restrictions, author renaming, repeated favourites, identity, duplicate/unknown list items |
| Inventory | KafkaBookEventPublisherTests | 1 | 1 | Unsupported inventory event rejection and one-time options access |
| Lending | DueDateReminderServiceTests | 3 | 7 | Zero-to-two-day reminder window, returned loans, repeat processing |
| Lending | KafkaReservationEventPublisherTests | 1 | 1 | Unsupported lending event rejection before topic access |
| Lending | ReservationOutboxDispatcherTests | 3 | 3 | Publish once, skip published messages, keep failed publication retryable |
| Lending | ReservationRulesTests | 6 | 11 | Admin rejection, invalid acceptance IDs, invalid reserver identity, cancellation ownership, missing returns |
| Lending | ReservationSnapshotServiceTests | 2 | 7 | Active/completed statuses and duplicate snapshots |
| Lending | ReturnFineCalculatorTests | 3 | 9 | Early/on-time/late returns, daily fine rate, Colombo midnight, signed due-day differences |
| Notification | CatalogEventHandlerTests | 3 | 6 | Customer/admin announcements, duplicate delivery, invalid payloads |
| Notification | LendingEventHandlerTests | 6 | 9 | Notification creation, duplicates, payload validation, title fallback/failure, closed-state protection |
| Notification | LookupFallbackTests | 2 | 6 | Missing/invalid inventory and user URLs avoid HTTP calls |
| Notification | NotificationFactoryTests | 5 | 13 | Accepted messages, positive-fine boundaries, title fallback, cancellation audience, unsupported events |
| Notification | NotificationReadStoreTests | 3 | 3 | Duplicate IDs, repeated reads, separate recipients |
| Notification | NotificationVisibilityTests | 2 | 2 | Customer ownership and hiding an admin's own catalogue change |

| Project | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| UserService.UnitTests | 20 | 0 | 0 |
| InventoryService.UnitTests | 23 | 0 | 0 |
| LendingService.UnitTests | 38 | 0 | 0 |
| NotificationService.UnitTests | 39 | 0 | 0 |
| Total | 120 | 0 | 0 |

TRX files are in the ignored TestResults directory. Build/restore produced warnings about EF Core Relational 9.0.0/9.0.2 references, dependency vulnerability advisories, and xUnit suggestions to use assertion helpers outside this assignment's allowed list. These did not fail the builds or tests. Production dependency versions were left as supplied.


Four separate test projects reference the matching backend services. The branch is unit-testing-ASE, created from updated main. Existing tests elsewhere in the repository are separate from this assignment.

## Tools and why

- **xUnit 2.9.3** runs tests and supplies Fact, Theory, InlineData and assertions. It makes individual examples and repeated boundary examples easy to read.
- **Moq 4.20.72** supplies replacement interface dependencies, so tests control configuration, title lookups and event publication without external services.
- **Microsoft.NET.Test.Sdk 17.13.0** and **xunit.runner.visualstudio 3.0.2** let dotnet test and compatible IDEs discover and run the tests.
- **EF Core InMemory** supplies a fresh database per test. User and inventory use 9.0.2; lending and notification use 8.0.0, matching their services.
- **.NET 10 SDK** is required because all four services target net10.0.

Only basic Moq Mock, Setup, Returns, ReturnsAsync and Verify with Times.Once/Times.Never are used. Faulted tasks model exceptions without advanced mocking APIs. No mocked HttpClient, SetupSequence, Callback, MemberData or argument matchers are needed.

## Run the tests

Open PowerShell at the repository root (ReadingPal). The first run needs NuGet access. These tests require no running backend services, MySQL, Kafka or Docker.

Run all four projects, stopping with a failing exit code if any project fails:

~~~powershell
$aseProjects = @(
    "tests/ASE Assignment/Unit Testing (xUnit-Moq)/UserService.UnitTests/UserService.UnitTests.csproj"
    "tests/ASE Assignment/Unit Testing (xUnit-Moq)/InventoryService.UnitTests/InventoryService.UnitTests.csproj"
    "tests/ASE Assignment/Unit Testing (xUnit-Moq)/LendingService.UnitTests/LendingService.UnitTests.csproj"
    "tests/ASE Assignment/Unit Testing (xUnit-Moq)/NotificationService.UnitTests/NotificationService.UnitTests.csproj"
)
foreach ($aseProject in $aseProjects) {
    dotnet test "$aseProject"
    if ($LASTEXITCODE -ne 0) { throw "Tests failed: $aseProject" }
}
~~~

Run one project:

~~~powershell
dotnet test "tests/ASE Assignment/Unit Testing (xUnit-Moq)/UserService.UnitTests/UserService.UnitTests.csproj"
dotnet test "tests/ASE Assignment/Unit Testing (xUnit-Moq)/InventoryService.UnitTests/InventoryService.UnitTests.csproj"
dotnet test "tests/ASE Assignment/Unit Testing (xUnit-Moq)/LendingService.UnitTests/LendingService.UnitTests.csproj"
dotnet test "tests/ASE Assignment/Unit Testing (xUnit-Moq)/NotificationService.UnitTests/NotificationService.UnitTests.csproj"
~~~

To save a result file, add these arguments to a project command (use a different file name for each project):

~~~powershell
--logger "trx;LogFileName=UserService.UnitTests.trx" --results-directory "tests/ASE Assignment/Unit Testing (xUnit-Moq)/TestResults"
~~~

## Simple explanations

**Mocking:** give a class a pretend dependency whose answer we choose. A mocked IBookTitleLookup returns "Dune" immediately, so the test does not contact Inventory. The class being tested is still real.

**Theory and InlineData:** a Theory runs the same test with several inputs. Each InlineData is one example. For a reminder window, -1, 0, 1, 2 and 3 days check just outside and inside the allowed range.

**Verify:** check whether the real class used a mock as expected. Times.Once means exactly one call. Times.Never means no call. A pending outbox message must be published once; an already-published message must not be sent again.

**Arrange, Act, Assert:** prepare inputs and dependencies, call the method, then check the result. Every test labels these three parts and has a one-line comment explaining what it proves.

## Scope

The user and inventory services hold business rules inside controllers, so controllers with rules are tested directly. Plain models and fixed-response actions are skipped. No production code was changed to make the tests pass.

The concrete database contexts use EF Core InMemory rather than mocked repositories. These database-backed examples are direct class/component tests; the calculators and factories are isolated unit tests. InMemory does not prove MySQL constraints, transactions or concurrency. HTTP checkout/return success paths, Kafka delivery/topic administration and background scheduling require integration tests. ModelState is populated explicitly where an action checks it: calling a controller directly does not run ASP.NET model validation or authorization middleware.

See [EXPLORATION.md](EXPLORATION.md) for project locations, business classes and constructor dependencies. See [TEST-CATALOG.md](TEST-CATALOG.md) for every test name and its one-line explanation.

## Two-minute live demo

Open the four methods below in advance. Restore/build the projects before presenting, then use --no-build --no-restore for a quick run.

| Time | Kind | Test | Explain |
| --- | --- | --- | --- |
| 0:00–0:30 | Happy path | AuthControllerTests.Register_NewEmail_SavesUnapprovedUser | A valid registration saves one unapproved reader and hashes the password. |
| 0:30–1:00 | Theory | BooksControllerTests.Update_CopyBoundary_PreservesBorrowedCopies | With two copies borrowed, totals of 1, 2 and 3 show rejection, the exact limit and one shelf copy. |
| 1:00–1:30 | Failure | ReservationOutboxDispatcherTests.DispatchAsync_PublisherUnavailable_KeepsMessagePending | A faulted task represents a publisher outage; the exception is checked and the event stays retryable. |
| 1:30–2:00 | Verify | LendingEventHandlerTests.HandleAsync_AcceptedEvent_CreatesNotification | ReturnsAsync supplies a title; Verify with Times.Once proves the title dependency was called once. |

Exact demo commands from the repository root:

~~~powershell
dotnet test "tests/ASE Assignment/Unit Testing (xUnit-Moq)/UserService.UnitTests/UserService.UnitTests.csproj" --no-build --no-restore --filter "FullyQualifiedName~Register_NewEmail_SavesUnapprovedUser"
dotnet test "tests/ASE Assignment/Unit Testing (xUnit-Moq)/InventoryService.UnitTests/InventoryService.UnitTests.csproj" --no-build --no-restore --filter "FullyQualifiedName~Update_CopyBoundary_PreservesBorrowedCopies"
dotnet test "tests/ASE Assignment/Unit Testing (xUnit-Moq)/LendingService.UnitTests/LendingService.UnitTests.csproj" --no-build --no-restore --filter "FullyQualifiedName~DispatchAsync_PublisherUnavailable_KeepsMessagePending"
dotnet test "tests/ASE Assignment/Unit Testing (xUnit-Moq)/NotificationService.UnitTests/NotificationService.UnitTests.csproj" --no-build --no-restore --filter "FullyQualifiedName~HandleAsync_AcceptedEvent_CreatesNotification"
~~~
