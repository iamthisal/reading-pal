

# ReadingPal

A library/book rental management system for user registration, book inventory management, and admin-driven library operations.

## Features

- JWT authentication and role-based authorization
- User registration, login, validation, profile viewing, and profile updates
- Admin dashboards for pending users, active users, and access management
- Book inventory CRUD with ISBN duplicate checks, cover image URLs, copy counts, and availability status
- Genre management for creating, updating, listing, and deleting book categories
- Kafka event publishing for book created, updated, and deleted events
- Book reservations, admin acceptance/rejection, user cancellation, and retry-safe Inventory checkout/return calls
- Borrowing history for users and admins, with unpaid return fines of LKR 10 per overdue calendar day
- Lending outbox publishing for reservation-accepted, reservation-cancelled, and book-returned events
- Docker Compose support for local service/database orchestration
- Unit, integration, and Selenium test coverage for user and inventory workflows

## Technology Stack

- Frontend: React, TypeScript, Vite, React Router, Axios, and lucide-react
- Backend: ASP.NET Core Web API, Entity Framework Core, REST, JWT, BCrypt, and xUnit
- Database: MySQL
- Messaging: Apache Kafka, with a KRaft-based stack under `infrastructure/kafka`
- DevOps: GitHub Actions, Docker, Docker Compose, Microsoft Azure, and Azure Static Web Apps
- Monitoring: Azure Application Insights

## Microservices

- `user-service` - Implemented. Handles registration, login/JWT authentication, profile management, admin-only pending/active user lists, user acceptance, rejection, and access revocation.
- `inventory-service` - Implemented. Handles books, genres, inventory copy counts, availability changes, JWT-protected admin operations, MySQL migrations, and Kafka publication for book events.
- `lending-service` - Implemented. Handles reservations, admin acceptance/rejection, user cancellation, checkout/return coordination, borrowing history, overdue calculation, unpaid fines, and Kafka outbox publication. Renewals and fine payment are not implemented.
- `notification-service` - Planned. Will handle due-soon, overdue, reservation, and user/admin notification logs.

## Branching Strategy

- `main` contains stable, reviewed, releasable code.
- `develop` integrates completed user stories.
- Feature branches start from `develop` and merge back through pull requests.
- Only `develop` should normally merge into `main`.
- Both permanent branches require pull requests, at least one approval, resolved conversations, blocked force pushes, and blocked deletion.
- CI checks become required only after the workflow has completed successfully at least once.

Branch names should use the exact Jira issue key:

```text
feature/YRP-6-user-login
fix/YRP-12-fix-user-search
refactor/YRP-6-improve-authentication
docs/YRP-9-update-registration-documentation
test/YRP-19-add-book-tests
```

## Repository Structure

```text
ReadingPal/
|-- .github/
|   |-- PULL_REQUEST_TEMPLATE.md
|   `-- workflows/
|       |-- ci.yml
|       |-- cd.yml
|       |-- ci_inventry.yml
|       |-- cd_inventry.yml
|       `-- azure-static-web-apps-polite-water-0c0e68a00.yml
|-- services/
|   |-- user-service/
|   |   |-- Controllers/
|   |   |-- Data/
|   |   |-- DTOs/
|   |   |-- Migrations/
|   |   |-- Models/
|   |   |-- Dockerfile
|   |   `-- UserService.csproj
|   |-- inventory-service/
|   |   |-- Controllers/
|   |   |-- Data/
|   |   |-- DTOs/
|   |   |-- Kafka/
|   |   |-- Migrations/
|   |   |-- Models/
|   |   |-- Dockerfile
|   |   `-- InventoryService.csproj
|   `-- lending-service/
|       |-- Controllers/
|       |-- Data/
|       |-- DTOs/
|       |-- Kafka/
|       |-- Migrations/
|       |-- Models/
|       |-- Dockerfile
|       `-- LendingService.csproj
|-- frontend/
|   |-- src/
|   |   |-- components/
|   |   |-- contexts/
|   |   |-- pages/
|   |   `-- config/
|   |-- package.json
|   `-- vite.config.ts
|-- tests/
|   |-- UserService.Tests/
|   |-- UserService.Selenium/
|   |-- InventoryService.Tests/
|   |-- LendingService.Tests/
|   `-- InventoryService.Selenium/
|-- deploy/
|   |-- README.md
|   |-- docker/
|   |   |-- README.md
|   |   `-- .gitkeep
|   `-- azure/
|       |-- README.md
|       `-- .gitkeep
|-- infrastructure/
|   `-- kafka/
|-- docker-compose.yml
|-- .editorconfig
|-- .env.example
|-- .gitattributes
|-- .gitignore
`-- README.md
```

## Status

ReadingPal currently has the core user and inventory foundations in place. The React frontend includes login, registration, home, profile, admin dashboard, admin user management, and admin book management pages. The `user-service` and `inventory-service` are implemented as ASP.NET Core Web APIs with MySQL persistence, EF Core migrations, JWT-based authorization, health endpoints, Dockerfiles, and active GitHub Actions CI/CD workflows for Azure App Services deployment.

Inventory work has progressed beyond the original placeholder state: admins can create, update, remove, and toggle availability for books; manage genres; store cover image URLs; and actively publish Kafka events to an Azure Container Instance when books change. The repository also includes unit/integration tests and Selenium tests for major user and inventory flows.

Lending implements reservation, borrowing, return, cancellation, history, and fine workflows. Inventory copy updates use HTTP; Inventory does not consume Lending's Kafka events. Notification Service remains planned, so published events do not yet generate notifications.

## Lending documentation

See the [Lending Service developer guide](docs/lending-service.md) for features, APIs, data models, events, and frontend behaviour.
