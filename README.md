

# ReadingPal

A library/book rental management system for admin-driven checkout and check-in workflows

## Features

- JWT authentication and role-based authorization
- User registration, viewing, and profile management
- Add new books to the inventory and assign categories to books
- Lend a book and assign it to a member/user
- Calculate fines for overdue books
- Send notifications when a book is issued, returned, or overdue
- Kafka-based asynchronous communication between services

## Technology Stack

- Frontend: React, TypeScript
- Backend: ASP.NET Core Web API, .NET, REST, JWT, and xUnit
- Messaging: Apache Kafka and ZooKeeper
- DevOps: GitHub Actions, Docker, Docker Compose, Microsoft Azure, and GitHub Environments
- Monitoring: Azure Application Insights

## Microservices

- `user-service` - Accounts, login, logout, JWT authentication, users, roles, and profiles; not started yet.
- `inventory-service` - Book catalog CRUD, search/browse, and stock tracking for total and available copies; not started yet.
- `lending-service` - Admin checkout/check-in, rental history, renewals, cancellations, overdue flagging, and late fee display; not started yet.
- `notification-service` - Checkout, due-soon, and overdue alerts, plus admin/user notification logs; not started yet.

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
|       |-- README.md
|       `-- .gitkeep
|-- services/
|   |-- user-service/
|   |   `-- .gitkeep
|   |-- inventory-service/
|   |   `-- .gitkeep
|   |-- lending-service/
|   |   `-- .gitkeep
|   `-- notification-service/
|       `-- .gitkeep
|-- frontend/
|   `-- .gitkeep
|-- tests/
|   `-- .gitkeep
|-- deploy/
|   |-- README.md
|   |-- docker/
|   |   |-- README.md
|   |   `-- .gitkeep
|   `-- azure/
|       |-- README.md
|       `-- .gitkeep
|-- docker-compose.yml
|-- .editorconfig
|-- .env.example
|-- .gitattributes
|-- .gitignore
`-- README.md
```

## Status

Early repository scaffold only. No application code has been written yet.
