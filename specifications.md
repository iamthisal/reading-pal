# Library Management System — Specifications
**SE3022 Case Study Project — Assignment 01**

---

## 1. System Scenario & Stakeholder Interactions

The system is a web application for a physical library. It exists to keep records of the library's book inventory, borrowings, and returns — it does not manage e-books, digital lending, or any library function outside of that scope.

**Regular users**
- Can register a personal account on the system.
- Can browse which books are physically available in the library without needing to visit in person.
- Can reserve a book online, provided the library currently holds at least one available physical copy.
- Do not receive the book automatically online — after reserving, the user must go to the library counter in person to collect it.
- Can view their own currently borrowed book(s) and their full borrowing history.
- Are notified in-app (on their profile) when: their reservation is accepted by the admin, their book's due date is approaching, and when a fine calculation has been initiated because a book is overdue.

**Admin (elevated privileges)**
- Can add new books to the inventory whenever a new physical book is introduced to the library.
- Can create genres, which act as containers/categories that books are organized into.
- Can view a list of all registered user accounts with identifying profile details.
- Can open a specific user's profile to see whether they currently have a book on loan, and their complete borrowing history.
- Can see all pending reservations made online by users.
- When a user arrives at the counter to collect a reserved book, the admin accepts the reservation in the system — this marks the book as borrowed and, if the book has multiple physical copies, deducts one from the available count.
- Marks a book as returned when a user brings it back to the counter.

**Shared behavior (both users and admin)**
- Can view the full list of books.
- Can filter the book list by genre, availability status (copies available or not), and author.
- Can search the book list by keyword (title/author) — included despite initial scope concerns, since it layers cheaply on top of the filtering feature rather than requiring separate infrastructure.

**Borrowing & fines lifecycle**
- A user has 14 days from checkout to return a borrowed book.
- If a book is not returned within 14 days, a fine is calculated on the user's behalf.
- Fine payments are handled physically at the library counter — the web application only tracks and displays fines, it does not process payment.

---

## 2. Technology Stack

| Layer | Choice |
|---|---|
| Frontend | React.js |
| Backend framework | ASP.NET Core (Web API) |
| ORM | Entity Framework Core |
| Database | MySQL — one database per microservice (database-per-service pattern) |
| Version control | GitHub |
| CI/CD | GitHub Actions |
| Containerization | Docker |
| Cloud platform | Azure |
| Event streaming | Apache Kafka (compulsory per assignment brief) |

---

## 3. System Architecture

**Style:** Microservice architecture — independently deployable services communicating over well-defined APIs (assignment requirement; minimum 4 services).

**Services (4):**

| Service | Responsibility | Own database |
|---|---|---|
| User Service | Registration, login/authentication, profile management, admin's list/detail view of user accounts | `users_db` |
| Catalog Service | Books, genres, copy counts, listing/filtering/search | `catalog_db` |
| Borrowing Service | Reservations, borrow/return processing, due dates, fine calculation, borrowing history, activity reports | `borrowing_db` |
| Notification Service | In-app notifications, consumes events from the other services | `notification_db` |

**Authentication & authorization:** JWT-based authentication issued by the User Service; role claim (`User` / `Admin`) checked by every service to enforce role-based access control on admin-only endpoints.

**Inter-service communication:**
- **Synchronous (REST):** Borrowing Service calls Catalog Service to check copy availability before creating a reservation. The frontend independently calls User Service and Borrowing Service to compose the admin's "user profile + borrowing history" view (no direct service-to-service call needed for this read).
- **Asynchronous (Kafka):** Borrowing Service publishes state-change events; Catalog Service and Notification Service consume them (see §4).

---

## 4. Event-Driven Architecture (Kafka)

Kafka is used for real inter-service consistency, not only as a notification pipe.

| Event | Published by | Consumed by | Purpose |
|---|---|---|---|
| `ReservationAccepted` | Borrowing Service | Catalog Service, Notification Service | Decrement available copy count; notify user their reservation was accepted |
| `BookReturned` | Borrowing Service | Catalog Service | Increment available copy count |
| `DueDateApproaching` | Borrowing Service (scheduled job) | Notification Service | Remind user their due date is near |
| `FineInitiated` | Borrowing Service (scheduled job) | Notification Service | Alert user a fine has started accruing on an overdue book |

A daily scheduled background job inside the Borrowing Service scans active loans to detect approaching due dates and newly-overdue loans, and publishes `DueDateApproaching` / `FineInitiated` accordingly (flagged so each fires only once per loan).

---

## 5. Deployment Architecture

- Each of the 4 microservices is containerized (Docker) and pushed to a shared Azure Container Registry.
- Each service is deployed as its own Azure App Service instance using Web App for Containers.
- **Kafka is containerized but not deployed to Azure App Service** — App Service is built to proxy HTTP(S) traffic on one port per app, while a Kafka broker needs raw TCP connections from every other service. Instead, the broker (Kafka in KRaft mode, or the lighter Kafka-protocol-compatible Redpanda) runs on **Azure Container Apps**, which supports internal TCP networking and persistent storage between containers.
- Managed alternative if broker operations are undesirable: Azure Event Hubs, which exposes a Kafka-protocol-compatible endpoint that Kafka client code can connect to with a configuration change instead of a code change (available on Standard tier and above).
- CI/CD (GitHub Actions) pipeline pattern, identical across all 4 services: build → run tests → `docker build` → push image to ACR → deploy new tag to the corresponding App Service.

---

## 6. Monitoring & Observability

Assignment requirement: use one of the following (final tool choice to be confirmed by the team):
- Prometheus + Grafana
- ELK / EFK Stack
- Azure Application Insights

---

## 7. Testing Tools

| Tool | Purpose |
|---|---|
| Unit testing framework (e.g. xUnit for .NET) | Unit tests per service |
| Selenium | End-to-end UI test automation |
| JMeter | Performance / load testing |

Required test coverage areas per assignment brief: performance testing and code coverage.

---

## 8. Domain / Business Rules

- **Loan period:** 14 days from checkout date.
- **Fine calculation:** `daysOverdue × dailyRate` (rate configurable, not hardcoded).
- **Fine payment:** tracked in-system as paid/unpaid for admin visibility, but always settled physically at the counter — no payment gateway integration.
- **Copy availability:** `availableCopies` is decremented only when the admin *accepts* a reservation at the counter (not at the moment the user reserves online), and incremented when a book is marked returned.
- **Reservation → borrow flow:** Reserve online (status `Pending`) → admin reviews pending queue → admin accepts at counter (status `Borrowed`, due date set) or rejects (status `Cancelled`, no copy count change).
