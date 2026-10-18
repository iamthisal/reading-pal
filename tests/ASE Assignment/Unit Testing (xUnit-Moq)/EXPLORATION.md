# Backend exploration

Inspected after updating the default branch, main, and creating unit-testing-ASE. This inventory was completed before writing the assignment tests.

## User

Project: services/user-service/UserService.csproj

| Class | Business rules | Constructor dependencies and mockability |
| --- | --- | --- |
| AuthController | Duplicate emails, password hashing/checking, built-in admin login, token claims and approval status | UserDbContext: concrete database context, not an interface. IConfiguration: interface, mockable. |
| UserController | Token identity, missing users, duplicate email prevention, optional password replacement | UserDbContext: concrete. |
| AdminController | Pending/active member filtering, approval, rejection and revocation; prevents revoking admins | UserDbContext: concrete. |

GetAdminMessage only returns a fixed greeting, so it is skipped.

## Inventory

Project: services/inventory-service/InventoryService.csproj

| Class | Business rules | Constructor dependencies and mockability |
| --- | --- | --- |
| BooksController | Duplicate ISBNs, text cleanup, borrowed/shelf copy counts, availability, deletion restrictions, checkout/return retry rules | InventoryDbContext: concrete. IBookEventPublisher and ILogger<BooksController>: interfaces, mockable. The convenience constructor takes only the context and creates a no-op publisher and logger. |
| GenresController | Duplicate names, renaming matching books, blocking deletion of a genre in use | InventoryDbContext: concrete. |
| AuthorsController | Duplicate names, optional text cleanup, author renames carried to books, book counts | InventoryDbContext: concrete. |
| BookSearchController | Search terms, genre/availability filtering, ranking/sorting, page and size limits, facet counts | InventoryDbContext: concrete. |
| FavouritesController | Reader ownership, missing books, safe repeated additions/removals | InventoryDbContext: concrete. |
| ListsController | Valid item types, unique keys, duplicate/unknown item rejection, ordered replacement | InventoryDbContext: concrete. |
| KafkaBookEventPublisher | Routes supported event types to configured topics, rejects unknown types | IProducer<string,string> and IOptions<KafkaOptions>: interfaces, mockable. |

CatalogueSeeder is development catalogue setup rather than a request business service. NoOpBookEventPublisher is a no-op adapter. Neither needs assignment unit tests.

## Lending

Project: services/lending-service/LendingService.csproj

| Class | Business rules | Constructor dependencies and mockability |
| --- | --- | --- |
| ReservationsController | Member identity/approval, duplicate requests, availability, allowed acceptance/rejection states, checkout intent, outbox events | LendingDbContext: concrete. IHttpClientFactory and IConfiguration: interfaces, mockable. |
| ReturnsController | Return eligibility, repeated returns, original return time, overdue fine amount and event creation | LendingDbContext: concrete. IHttpClientFactory and IConfiguration: interfaces, mockable. |
| MyBorrowingsController | Reader ownership, cancellation eligibility, current/historical loan grouping, fine totals | LendingDbContext: concrete. IHttpClientFactory and IConfiguration: interfaces, mockable. |
| ReturnFineCalculator | Sri Lankan calendar-day overdue/due differences; overdue days cannot be negative | Static: no constructor or dependencies. No mock is necessary. |
| DueDateReminderService | Reminds only active, unreminded loans due within zero to two local calendar days | LendingDbContext: concrete. ILogger<DueDateReminderService>: interface, mockable. |
| ReservationSnapshotService | Snapshots active reservations only and avoids already-queued snapshots | LendingDbContext: concrete. ILogger<ReservationSnapshotService>: interface, mockable. |
| ReservationOutboxDispatcher | Publishes up to 20 pending events in order, marks only successful sends, keeps failures retryable | LendingDbContext: concrete. IReservationEventPublisher: interface, mockable. |
| KafkaReservationEventPublisher | Routes each supported lending event to its configured Kafka topic | IProducer<string,string> and IOptions<KafkaOptions>: interfaces, mockable. |

DueDateReminderWorker takes IServiceScopeFactory, IConfiguration and ILogger<DueDateReminderWorker>. ReservationSnapshotWorker and ReservationOutboxWorker each take IServiceScopeFactory and their typed ILogger. All these dependencies are interfaces. The workers mainly schedule and delegate work; their service rules are tested directly instead of starting background loops.

## Notification

Project: services/notification-service/NotificationService.csproj

| Class | Business rules | Constructor dependencies and mockability |
| --- | --- | --- |
| NotificationFactory | Selects audiences, suppresses invalid fine notices, chooses message wording, fallback titles, currency and local dates | Static: no dependencies. |
| LendingEventHandler | Validates payloads, ignores duplicates, tracks forward-only reservation state, creates missing audiences and handles earlier book deletion | NotificationDbContext: concrete. IBookTitleLookup and ILogger<LendingEventHandler>: interfaces, mockable. |
| CatalogEventHandler | Validates payloads, creates customer/admin notices, remembers deleted books, targets affected readers and ignores duplicates | NotificationDbContext: concrete. ILogger<CatalogEventHandler>: interface, mockable. |
| NotificationReadStore | Independent per-recipient read records, duplicate IDs, repeat-read safety | NotificationDbContext: concrete. |
| NotificationsController | Personal/shared announcement visibility, activation time, reader ownership, paging and marking read | NotificationDbContext and NotificationReadStore: concrete; read store is sealed and cannot be mocked directly with Moq. |
| AdminNotificationsController | Hides an admin's own changes, per-admin reads, customer-name substitution, paging | NotificationDbContext and NotificationReadStore: concrete. ICustomerDirectory: interface, mockable. |
| InventoryBookTitleLookup | Optional title lookup, invalid configuration and unavailable/missing-title fallbacks | IHttpClientFactory, IConfiguration and ILogger<InventoryBookTitleLookup>: interfaces, mockable. |
| UserServiceCustomerDirectory | Customer-name lookup, invalid configuration and unavailable-directory fallbacks | IHttpClientFactory, IConfiguration and ILogger<UserServiceCustomerDirectory>: interfaces, mockable. |

ReservationStatuses.Rank also contains a small status-ordering rule despite living beside a model. RecipientKeys and response helpers mainly format identifiers or map fields.

NotificationConsumerWorker takes IServiceScopeFactory, IOptions<KafkaOptions> and ILogger<NotificationConsumerWorker> (interfaces), plus the concrete KafkaTopicInitializer from the pre-existing local edits. Its Kafka subscription/offset loop is infrastructure. KafkaTopicInitializer takes mockable IAdminClient and IOptions<KafkaOptions>; broker administration is outside this assignment's business-rule tests.

## Testing decision

The database contexts are concrete and their DbSet properties are not virtual. They cannot be substituted as repository interfaces using simple Moq setups. Each test therefore gets a real context backed by a separate EF Core InMemory database. Interface dependencies use Moq. No production refactor is needed.

These are direct class tests. Database-backed tests exercise EF Core's in-memory provider, so they are not fully isolated repository-mocked unit tests. They require no MySQL, Kafka, running API or HTTP calls. They do not establish relational constraints, transactions, ExecuteUpdate behavior, concurrent delivery safety, authentication middleware, or full multi-service checkout/return behavior. Those need integration tests.
