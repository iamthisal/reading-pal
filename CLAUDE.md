# reading-pal — Library Management System

Microservice project for SE3022 Case Study Project (Assignment 01). Full context: `specifications.md` at repo root, and `docs/lending-service.md` for the Lending Service's implemented behavior.

## Stack

React frontend · ASP.NET Core Web API + EF Core · MySQL (database-per-service) · Apache Kafka (KRaft mode) · Docker · GitHub Actions.

## Services

- `services/user-service` — auth, profiles (built)
- `services/inventory-service` — books, genres, copies (built)
- `services/lending-service` — reservations, accept/reject, returns, fines, Kafka outbox (Sprint 3, in progress)
- `services/notification-service` — not yet started

## Sprint 3 — QA role

Current role: QA. The Lending Service test plan lives at `docs/lending-service.md` (developer guide) and in the Claude Docs "Lending Service — QA Test Plan" artifact. This file gives the exact commands to execute that plan and produce the report artifacts the assignment's documentation criterion requires.

## Testing — exact steps and commands

### 1. Environment (MySQL + Kafka)

```bash
docker compose up -d mysql db-init
docker compose -f infrastructure/kafka/docker-compose.yml up -d kafka
```

Auto-create-topics is disabled on the broker — create the three topics explicitly before running anything that touches Kafka:

```bash
docker exec -it <kafka-container> /opt/kafka/bin/kafka-topics.sh --create --bootstrap-server kafka:19092 --topic reservation-accepted --partitions 1 --replication-factor 1
docker exec -it <kafka-container> /opt/kafka/bin/kafka-topics.sh --create --bootstrap-server kafka:19092 --topic reservation-cancelled --partitions 1 --replication-factor 1
docker exec -it <kafka-container> /opt/kafka/bin/kafka-topics.sh --create --bootstrap-server kafka:19092 --topic book-returned --partitions 1 --replication-factor 1
```

Optional visual inspection: `docker compose -f infrastructure/kafka/docker-compose.yml --profile tools up -d kafka-ui` → `localhost:8085`.

Start the service under test: `docker compose up -d lending-service`.

### 2. Unit tests + coverage

```bash
dotnet test tests/unit-tests/LendingService.Tests --collect:"XPlat Code Coverage" --results-directory tests/unit-tests/LendingService.Tests/TestResults
```

Produces `tests/unit-tests/LendingService.Tests/TestResults/<run-id>/coverage.cobertura.xml`.

### 3. Integration tests

Project: `tests/integration-tests/LendingService.IntegrationTests` (or tests tagged `[Trait("Category","Integration")]` inside `LendingService.Tests`). Requires `Microsoft.AspNetCore.Mvc.Testing`, `Testcontainers.MySql`, `Testcontainers.Kafka`.

```bash
dotnet test --filter Category=Integration --collect:"XPlat Code Coverage" --results-directory tests/unit-tests/LendingService.Tests/TestResults
```

Docker must be running (Testcontainers provisions its own MySQL/Kafka containers per test run).

### 4. Coverage report (turn raw data into the HTML artifact)

```bash
dotnet tool install -g dotnet-reportgenerator-globaltool   # once
reportgenerator -reports:"tests/unit-tests/LendingService.Tests/TestResults/**/coverage.cobertura.xml" -targetdir:"tests/unit-tests/LendingService.Tests/TestResults/CoverageReport" -reporttypes:Html
```

Open `tests/unit-tests/LendingService.Tests/TestResults/CoverageReport/index.html` — this is the coverage artifact to keep for submission.

### 5. Security / access-control tests + dependency scan

Role-boundary tests (non-admin token on an admin endpoint → 403, no token → 401) run as part of the integration suite above. Dependency scan:

```bash
cd services/lending-service
dotnet list package --vulnerable --include-transitive > ../../security-scan-lending.txt
cd ../..
```

### 6. Selenium E2E tests

Project: `tests/selenium-tests/LendingService.Selenium` (create following the pattern of `tests/selenium-tests/UserService.Selenium` / `tests/selenium-tests/InventoryService.Selenium`). Full stack must be running first:

```bash
docker compose up -d
dotnet test tests/selenium-tests/LendingService.Selenium
```

### 7. JMeter performance tests

Test plan file: `tests/performance-tests/lending/lending-load-test.jmx` (create with Thread Groups for `POST /api/reservations`, `POST /api/reservations/{id}/accept`, `GET /api/my-borrowings`, `GET /api/reservations/pending`; include an HTTP Header Manager carrying a JWT bearer token, since every endpoint is authenticated).

```bash
jmeter -n -t tests/performance-tests/lending/lending-load-test.jmx -l tests/performance-tests/lending/jmeter-results/results.jtl -e -o tests/performance-tests/lending/jmeter-results/report
```

Open `tests/performance-tests/lending/jmeter-results/report/index.html` — keep this dashboard, and re-run the identical `.jmx` file against the Azure App Service URL once deployed, keeping both dashboards for local-vs-deployed comparison.

## Report artifacts to keep for submission

- [ ] `tests/unit-tests/LendingService.Tests/TestResults/CoverageReport/index.html` (coverage)
- [ ] `.trx`/console output from unit + integration runs
- [ ] `security-scan-lending.txt`
- [ ] Selenium test results
- [ ] `tests/performance-tests/lending/jmeter-results/report/index.html` (local run) + the Azure-deployed run's equivalent

## AI usage disclosure (per assignment brief)

The brief permits AI for research/planning, not for generating project code. Document any AI-assisted research or planning per that requirement — do not have it generate/complete feature code directly.
