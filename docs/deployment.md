# Deployment — Sprints 3–4 (Lending Service & Notification Service)

**Last updated:** 14 October 2026

This document records the Azure deployment work done to bring the Lending Service (Sprint 3) and Notification Service (Sprint 4) online, alongside the already-deployed User Service, Inventory Service, Static Web App, and Kafka broker. It also covers the production incidents found and fixed along the way. It supplements the existing deployment docs — see [deploy/azure/README.md](../deploy/azure/README.md) for the general resource overview and [infrastructure/kafka/AZURE-SETUP.md](../infrastructure/kafka/AZURE-SETUP.md) for the Kafka broker specifically.

Every value in this document was read back from the live Azure subscription and GitHub repository with the Azure CLI and GitHub CLI on 12 and 14 October 2026, unless marked otherwise. No passwords, keys, tokens or publish-profile contents are recorded here.

## Architecture and current status

| Layer | Azure resource | Status (14 Oct) |
| --- | --- | --- |
| Frontend | Static Web App `reading-pal-frontend` (`polite-water-0c0e68a00.3.azurestaticapps.net`) | **Live**, built with all four service URLs (verified in the deployed bundle) |
| User Service | App Service `reading-pal-userservice` | **Live** — `ghcr.io/iamthisal/reading-pal-userservice:a1fc611…` |
| Inventory Service | App Service `reading-pal-inventoryservice` | **Live** — `ghcr.io/iamthisal/reading-pal-inventoryservice:a1fc611…` |
| Lending Service | App Service `reading-pal-lendingservice` | **Live** — `ghcr.io/iamthisal/reading-pal-lendingservice:a1fc611…` |
| Notification Service | App Service `reading-pal-notificationservice` | **Live** — `ghcr.io/iamthisal/reading-pal-notificationservice:a1fc611…`; `/health` healthy; consuming all ten Kafka topics |
| Kafka broker | Container Instance `readingpal-kafka` (`cp-kafka:8.2.3`) | **Running**, now recreates its topics on every start (see step 3) |
| Database | Azure Database for MySQL Flexible Server `reading-pal-mysql-v3` | **Live** — one database per service |
| Telemetry | Application Insights (one per backend service) + shared Log Analytics workspace | **Live**; Notification's added this sprint |

All four backend apps run the image built from commit `a1fc611`, the merge of `develop` into `main` (PR #97) on 13 October 2026.

Kafka is **not** hosted on App Service. App Service is a stateless, request-driven platform and a poor fit for a long-running broker; the existing Azure Container Instances deployment was kept. Only the four backend microservices run on App Service.

### Resource inventory

All resources live in resource group **`reading-pal-v3`** (UAE North; the Static Web App is in East Asia), subscription **Azure for Students** (`65bc8484-12cb-42f4-a420-42d31f7d261f`).

| Resource | Name | Notes |
| --- | --- | --- |
| App Service plan | `reading-pal-plan` | Linux, **B1 Basic**, shared by all four backend apps |
| User Service | `reading-pal-userservice` | `https://reading-pal-userservice-bwh5bfc0agcvguf6.uaenorth-01.azurewebsites.net` |
| Inventory Service | `reading-pal-inventoryservice` | `https://reading-pal-inventoryservice-dnamc3bufybsa8ah.uaenorth-01.azurewebsites.net` |
| Lending Service | `reading-pal-lendingservice` | `https://reading-pal-lendingservice-a6dybkatfsahfkha.uaenorth-01.azurewebsites.net` |
| Notification Service | `reading-pal-notificationservice` | `https://reading-pal-notificationservice-b4gmcqhxgvhxfebu.uaenorth-01.azurewebsites.net` |
| MySQL Flexible Server | `reading-pal-mysql-v3` | `reading-pal-mysql-v3.mysql.database.azure.com:3306`, SSL required |
| Kafka | `readingpal-kafka` | `readingpal-kafka-grp13.uaenorth.azurecontainer.io:9092` |
| Application Insights | `reading-pal-userservice-insights`, `reading-pal-inventory-insights`, `reading-pal-lending-insights`, `reading-pal-notification-insights` | All in the same Log Analytics workspace |
| Static Web App | `reading-pal-frontend` | `https://polite-water-0c0e68a00.3.azurestaticapps.net` |

App Service hostnames carry a random suffix (`-a6dybkatfsahfkha…`). The short form `https://<app-name>.azurewebsites.net` does **not** resolve for these apps — always use the full hostname from the app's **Overview** page.

### Databases

Each service has its own database on `reading-pal-mysql-v3`. The Azure names differ from the local Docker Compose names (`ReadingPal_<service>`); that's fine, because each App Service's connection string names its own database explicitly.

| Service | Azure database | Local Docker database |
| --- | --- | --- |
| User | `userservicedb` | `ReadingPal_auth` |
| Inventory | `ReadingPal_inventory` (listed by the server as `readingpal_inventory`) | `ReadingPal_inventory` |
| Lending | `lendingservicedb` | `ReadingPal_lending` |
| Notification | `readingPalNotificationService` (listed by the server as `readingpalnotificationservice`) | `ReadingPal_notification` |

All four apps connect as the server admin login `Adminreadingpal`. The server lists database names in lowercase, while some connection strings use mixed case. This works because the server matches names case-insensitively. That is confirmed in practice: since 14 October the Notification Service has been reading and writing notifications through its mixed-case connection string.

## Steps carried out

### 1. App Service creation

`reading-pal-lendingservice` and `reading-pal-notificationservice` were created in the portal (**App Services → + Create → Web App**): resource group `reading-pal-v3`, publish **Container**, OS **Linux**, region UAE North, existing plan `reading-pal-plan`.

**Gotcha — sidecar mode.** The portal's create wizard now creates Linux custom-container apps in **sidecar mode** (`linuxFxVersion = sitecontainers`) with a placeholder container (`mcr.microsoft.com/appsvc/staticsite`, port 80). In that mode Azure ignores `DOCKER_REGISTRY_SERVER_*` and `WEBSITES_PORT`, so the app can't pull a private GHCR image the way the CD workflows expect. The Notification app was converted back to classic single-container mode in step 6. The Lending app was already in classic mode.

### 2. Databases

The MySQL Flexible Server has no database-browser blade, so databases are created with Cloud Shell's `mysql` client (**MySQL server → Connect → Connect using Azure Cloud Shell**), e.g. `CREATE DATABASE <name>;`. Both new databases exist (see the table above). The services apply their EF Core migrations themselves at startup.

**Networking:** the server allows "Allow public access from any Azure service" (rule `0.0.0.0`) so App Service can reach it. Cloud Shell or a local client also needs a firewall rule for its own IP. Temporary rules should be removed when the work is done (see Status).

### 3. Kafka topics

No new Container Instance is needed. The existing broker serves all services. Across Inventory, Lending and Notification there are now **ten** topics:

| Topic | Producer | Consumer |
| --- | --- | --- |
| `book-created`, `book-updated`, `book-deleted` | Inventory | Notification |
| `reservation-created`, `reservation-accepted`, `reservation-cancelled`, `book-returned` | Lending | Notification |
| `fine-recorded`, `book-due-soon`, `reservation-snapshot` | Lending | Notification |

Topic auto-creation is disabled on the broker, and the broker has **no persistent storage**: its data lives inside the container, so every restart starts it empty. That caused the notification outage described below.

- **Locally**, PR #88 added a `kafka-init` service ([`infrastructure/kafka/create-topics.sh`](../infrastructure/kafka/create-topics.sh)) that creates all ten topics on every stack start.
- **On Azure (since 14 Oct 02:39 UTC)**, the broker's startup command is [`infrastructure/kafka/aci-start-with-topics.sh`](../infrastructure/kafka/aci-start-with-topics.sh). It starts Kafka exactly as the image normally does, waits until the broker answers, creates the ten topics with `--if-not-exists`, and stays attached to the broker process, so ACI's `Always` restart policy still applies. A successful start logs `topic-init: done - 10 application topics present.`
  - It's applied with [`infrastructure/kafka/deploy-aci-kafka.ps1`](../infrastructure/kafka/deploy-aci-kafka.ps1). The script reads the live container group and changes **only** the startup command; the image, the 22 environment variables, size (1 vCPU / 2 GB), DNS label, ports and Log Analytics logging are all kept. It saves a backup of the current definition first. `-DryRun <file>` previews the exact definition without changing anything, and `-Restore` goes back to the image's default startup.
  - Before deploying, the script was tested locally with the same image and broker settings: both a first start and a completely fresh container ended with all ten topics, and a stop shut Kafka down cleanly. The dry-run output was validated with the Azure CLI's own YAML parser.
  - After deploying: the startup log shows the `topic-init: done` line, all ten topics are listed on the public endpoint, and the Notification consumer group re-attached to all ten topics within about 2 minutes, without restarting the Notification Service.
  - **Keep the topic lists in `create-topics.sh` and `aci-start-with-topics.sh` identical.**
- **Why not persistent storage instead?** The only persistent volume ACI offers is an Azure Files share over SMB, and Kafka's guidance is not to put its data on network or shared file systems (risk of index and segment failures). Recreating the topics on startup prevents the outage without that risk. Messages still don't survive a restart: Lending's outbox resends what it hadn't delivered, but anything already in the broker and not yet consumed — and Inventory events sent while the broker is down — is lost. Durable messages would need a managed service such as Azure Event Hubs with its Kafka endpoint.

To create the topics by hand (e.g. if the startup line is missing), use the ACI console (**Container instances → readingpal-kafka → Containers → Connect → `/bin/bash`**):

```bash
for topic in book-created book-updated book-deleted reservation-created reservation-accepted reservation-cancelled book-returned fine-recorded book-due-soon reservation-snapshot; do
  kafka-topics --bootstrap-server localhost:19092 --create --if-not-exists --topic "$topic" --partitions 1 --replication-factor 1
done
kafka-topics --bootstrap-server localhost:19092 --list
```

From a machine with Docker, the same tools can be run against the public endpoint without installing Kafka: `docker run --rm confluentinc/cp-kafka:8.2.3 kafka-topics --bootstrap-server readingpal-kafka-grp13.uaenorth.azurecontainer.io:9092 --list`.

The topic names are defaults in each service's `appsettings.json`, so App Service only needs `Kafka__Topics__*` settings to *override* a name. That's why the Lending app works without settings for the three newer topics.

### 4. App settings

Set in **App Service → Settings → Environment variables**. Saving restarts the app. The double underscore maps a flat variable onto nested configuration (`Kafka__Topics__BookReturned` → `Kafka:Topics:BookReturned`).

**Values that must match across services:**

| Setting | Value | Why |
| --- | --- | --- |
| `Jwt__Issuer` | `ReadingPal.UserService` | Must equal what User Service puts in its tokens (from its `appsettings.json`) |
| `Jwt__Audience` | `ReadingPal.Frontend` | Same |
| `Jwt__Key` | same secret as User and Lending | Tokens fail signature validation otherwise. Verified identical by hash comparison, without revealing the value. |

> Earlier drafts of this document listed `ReadingPal` for both issuer and audience — that value is from `.env.example` for **local** Docker only and is wrong for Azure. The Notification app had been configured with it and would have rejected every request with 401; corrected in step 5 and confirmed working on 14 Oct (authenticated notification calls return 200 for users and admins).

**Common to Lending and Notification:**

| Setting | Value |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `WEBSITES_PORT` | `8080` |
| `WEBSITES_ENABLE_APP_SERVICE_STORAGE` | `false` |
| `UserService__BaseUrl` | `https://reading-pal-userservice-bwh5bfc0agcvguf6.uaenorth-01.azurewebsites.net` |
| `InventoryService__BaseUrl` | `https://reading-pal-inventoryservice-dnamc3bufybsa8ah.uaenorth-01.azurewebsites.net` |
| `Frontend__Url` | `https://polite-water-0c0e68a00.3.azurestaticapps.net` (CORS) |
| `Kafka__BootstrapServers` | `readingpal-kafka-grp13.uaenorth.azurecontainer.io:9092` |
| `Kafka__SecurityProtocol` | `Plaintext` |
| `DOCKER_REGISTRY_SERVER_URL` | `ghcr.io` |
| `DOCKER_REGISTRY_SERVER_USERNAME` / `_PASSWORD` | GHCR pull credentials (Notification's were copied from Lending's; not recorded here) |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | from the service's own Application Insights resource |

The service URLs **must** include `https://`. The Notification Service validates them with `Uri.TryCreate(..., UriKind.Absolute)` and silently skips book-title and customer-name lookups when that fails, so a missing scheme doesn't crash anything. It just produces notifications without names. Since the fix, notifications show real customer names (e.g. "Dahamya Kulandi reserved 'atomic habbits'").

**Lending-specific:** the database connection is in the **Connection strings** section as `DefaultConnection` (type MySQL). Topic overrides are set for `ReservationAccepted`, `ReservationCancelled` and `BookReturned`; the rest use the `appsettings.json` defaults.

**Notification-specific:** the database connection is the app setting `ConnectionStrings__DefaultConnection`, which is equivalent to the Connection strings entry. It also has `Kafka__GroupId = notification-service`, plus topic overrides for `ReservationCreated`, `ReservationAccepted`, `ReservationCancelled` and `BookReturned`.

Connection string shape (Azure requires `SslMode=Required`, unlike local Docker):

```text
Server=reading-pal-mysql-v3.mysql.database.azure.com;Port=3306;Database=<database>;Uid=Adminreadingpal;Pwd=<password>;SslMode=Required;
```

### 5. Notification App Service — configuration fixes (12 Oct)

A read-only comparison against the working Lending app found the Notification app misconfigured. Fixed with the Azure CLI:

| Problem | Effect | Fix |
| --- | --- | --- |
| `Jwt__Issuer` / `Jwt__Audience` were `ReadingPal` | Every authenticated request would get 401 | Set to `ReadingPal.UserService` / `ReadingPal.Frontend` |
| `UserService__BaseUrl` / `InventoryService__BaseUrl` had no `https://` | Name/title lookups silently skipped | Added `https://` |
| No GHCR credentials | Image pull from private GHCR would fail | Copied `DOCKER_REGISTRY_SERVER_*` from the Lending app, without displaying them |
| No Application Insights | No telemetry | Created `reading-pal-notification-insights` (workspace-based, same Log Analytics workspace as Lending) and set its connection string |
| Sidecar mode with placeholder container | CD's publish-profile deploy can't target it as expected | Converted in step 6 |

### 6. Notification App Service — sidecar → classic container mode

There is no portal option to leave sidecar mode; Microsoft documents only the CLI route. It was run from **Azure Cloud Shell** (the **>_** icon in the portal top bar):

```bash
az webapp sitecontainers convert --mode docker \
  --name reading-pal-notificationservice \
  --resource-group reading-pal-v3 \
  --subscription 65bc8484-12cb-42f4-a420-42d31f7d261f
```

Result: placeholder container deleted, `linuxFxVersion` now `DOCKER|…`. The first CD run (13 Oct) replaced the placeholder image with `ghcr.io/iamthisal/reading-pal-notificationservice:a1fc611…`.

- **Gotcha 1:** Cloud Shell can default to a different subscription and then report `ResourceGroupNotFound` for a group that exists. Pass `--subscription` explicitly, or run `az account set --subscription <id>` first.
- **Gotcha 2:** the conversion **overwrites `DOCKER_REGISTRY_SERVER_URL`** with the placeholder image's registry (`https://index.docker.io`). It was set back to `ghcr.io` afterward. The username and password survived.

### 7. Publish profiles (Basic Auth)

App Services created since mid-2025 default to **SCM Basic Auth Publishing Credentials disabled**, which blocks **Download publish profile**. It's enabled on both new apps (verified: `basicPublishingCredentialsPolicies/scm` → `allow = true`). To enable it on a new app: **Settings → Configuration → General settings → Platform settings → SCM Basic Auth Publishing Credentials → On → Save**.

Moving to OIDC/federated-credential deployment would be more secure, but it would mean changing every CD workflow away from the `publish-profile:` pattern the project uses. That's out of scope here.

### 8. GitHub Actions — workflows, secrets and variables

**Workflows** (in `.github/workflows/`, documented in its `README.md`):

- `ci_lending.yml` / `cd_lending.yml` (Lending)
- `ci_notification.yml` / `cd_notification.yml` (Notification, added in commit `c259cfe`). These mirror the Lending pair:
  - **CI** runs on pushes and PRs to `main`/`develop`. It validates required files and the compose config, builds, runs `tests/unit-tests/NotificationService.Tests`, and builds the Docker image from `services/notification-service`.
  - **CD** runs after CI succeeds on a push to `main`, or manually. It builds and pushes `ghcr.io/iamthisal/reading-pal-notificationservice:<sha>`, deploys it with the publish profile, then polls `/health` when `NOTIFICATION_SERVICE_URL` is set.
- Before merging, the commands each CI workflow runs were reproduced locally on `develop`. All 295 unit tests passed (User 44, Inventory 63, Lending 89, Notification 99), along with compose validation and the frontend lint and build. New tests added since then live in the existing test projects, so CI picks them up without workflow changes.

**Repository secrets and variables** (Settings → Secrets and variables → Actions). All of these are confirmed present:

| Type | Name | Value |
| --- | --- | --- |
| Secret | `AZURE_LENDING_WEBAPP_PUBLISH_PROFILE` | Lending publish profile XML |
| Secret | `AZURE_NOTIFICATION_WEBAPP_PUBLISH_PROFILE` | Notification publish profile XML |
| Variable | `LENDING_SERVICE_URL` | `https://reading-pal-lendingservice-a6dybkatfsahfkha.uaenorth-01.azurewebsites.net` |
| Variable | `NOTIFICATION_SERVICE_URL` | `https://reading-pal-notificationservice-b4gmcqhxgvhxfebu.uaenorth-01.azurewebsites.net` |

These URLs are **variables** (`vars.`), not secrets. A secret with the same name would be ignored. Both must include `https://`; `NOTIFICATION_SERVICE_URL` was first saved without it and was corrected on 12 October. Each URL is used twice: by its CD workflow's health check, and by the Static Web App build.

### 9. Frontend — pointing the deployed site at the new services

Vite bakes API URLs into the frontend at **build time**. The frontend reads `VITE_LENDING_API_BASE_URL` and `VITE_NOTIFICATION_API_BASE_URL` and falls back to `http://localhost:…` when they're unset. The Static Web Apps workflow passed only the Lending URL, so the deployed notification bell and My Notifications pages would have called `localhost`. The fix (commit `736d28e`, merged into `develop` via PR #95) adds this to `azure-static-web-apps-polite-water-0c0e68a00.yml`:

```yaml
VITE_NOTIFICATION_API_BASE_URL: ${{ vars.NOTIFICATION_SERVICE_URL }}
```

Verified on 14 Oct: the deployed bundle contains all four Azure service hostnames and no `localhost` fallback.

`develop` only accepts changes through a pull request; direct pushes are rejected by a repository rule.

### 10. Deploy (13 Oct 2026)

PR #97 merged `develop` into `main` at about 23:21 UTC. Every workflow it triggered succeeded:

1. **CI:** `CI` (user service and frontend), `CI - Inventory Service`, `CI - Lending Service`, `CI - Notification Service`, and `Azure Static Web Apps CI/CD`, which rebuilt and deployed the frontend.
2. **CD:** `CD - Notification Service` (the first Notification deploy), `CD - Lending Service`, `CD - Inventory Service` and `CD` (user service). Each built and pushed its image to GHCR, deployed it with the publish profile, and passed its health check.

## Incident: MySQL admin password reset (12 Oct 2026)

The server admin password for `reading-pal-mysql-v3` was reset. Because **all four apps** connect as `Adminreadingpal`, every one of them kept the old password, and database calls started failing in production.

- **Symptom:** `/health` kept returning 200, because it doesn't touch the database. Real endpoints failed — Inventory's `GET /api/Books` returned **500**.
- **Fix:** in each app, only the `Pwd=` part of the connection string was replaced; server, database, user and SSL settings were unchanged. The changes were the `DefaultConnection` connection string on User, Inventory and Lending, and the `ConnectionStrings__DefaultConnection` app setting on Notification. User, Inventory and Lending were then restarted.
- **Verified afterwards:** Inventory `GET /api/Books` → 200; a regular-user login (User Service reads its database) → OK; Lending `GET /api/reservations/pending` with an admin token → OK (11 pending).
- **Lesson:** after any credential change, test a database-backed endpoint, not `/health`. A database-touching health check (e.g. ASP.NET Core `AddDbContextCheck`) would catch this automatically.

To rotate the password again, use the same procedure: update all four apps, restart them, and test a database-backed endpoint on each.

## Incident: no notifications in production (12–14 Oct 2026)

- **Symptom:** after the first Notification deploy (13 Oct), the notification bell stayed empty for everyone. `GET /api/notifications` returned `200 []` for both users and admins, and every `/health` endpoint was healthy.
- **Root cause:** the Kafka container on ACI restarted at **15:32 UTC on 12 October**. Azure's event log shows a fresh image pull and container start; it has restarted roughly every two weeks since September, including one platform-initiated kill. The broker keeps its data in the container's own filesystem (`KAFKA_LOG_DIRS=/tmp/kraft-combined-logs`, no volume), so the restart started it with **no topics**. Because `KAFKA_AUTO_CREATE_TOPICS_ENABLE=false`, nothing recreated them:
  - Lending's publishes failed with "Unknown topic or partition". Its outbox kept the events and retried every few seconds.
  - Inventory's book events failed and were lost, because Inventory publishes directly with no outbox.
  - The Notification consumer was connected but subscribed to topics that didn't exist, so it received nothing and created no notifications.
- **Why it wasn't caught:** `/health` doesn't check Kafka, and the topic auto-creation from PR #88 (`kafka-init`) only exists in the local Docker Compose stack. The Notification Service was deployed on 13 Oct, after the topics had already disappeared, so it never received an event in production until the fix.
- **Fix (14 Oct):**
  - Created the ten topics on the broker.
  - Lending's outbox immediately delivered its backlog: 22 reservation snapshots, 5 due-date reminders, plus reservation, return and fine events.
  - The Notification consumer group picked up all ten topics within about 2 minutes and processed everything with lag 0.
  - Notifications then appeared: due-date reminders for the test user, and new-reservation and cancellation notices for admins.
- **Prevention (14 Oct 02:39 UTC):** the broker's startup command now recreates the topics on every start (step 3), so a restart can no longer leave the system without topics. Applied and verified in production.

## Investigation: "regular users can't see the catalogue" (14 Oct 2026)

This was reported together with the notification outage, and investigated separately. It turned out **not to be a backend fault**:

- With a regular user's token, every endpoint the new catalogue page uses returned data with the correct CORS header for the deployed site: `/api/books`, `/api/genres`, `/api/authors`, `/api/favourites`, and all five configured shelves.
- The deployed bundle called the correct service URLs.
- Nothing in the home page hides the catalogue by role.

The likely causes were testing during the deployment window, or a cached older frontend bundle. Inventory only finished seeding the new shelves at 23:41 UTC, about 20 minutes after the deploy started. On a later check the catalogue displayed correctly.

Worth knowing for the future: `CatalogueSections` drops any shelf whose request fails, without an error message. If every shelf fails, the page shows no catalogue and no error, so a hard refresh (Ctrl+Shift+R) and the browser console are the first things to check.

## Clean-up: QA test data in production (14 Oct 2026)

The Selenium E2E suite had been run against the deployed site during Sprint 3 QA. It left QA records in production that showed up on the new home page: the "QA Accept/Reserve/Reject/Return/Cancel …" books filled the Recommended and Top 100 shelves, and "QA Lending Author" was the featured popular author.

- **Scope identified:** 22 QA books (inventory ids 12–33, title `QA …`, ISBN `QA-…`), 24 "QA Genre …" genres, the "QA Lending Author" author, and the rows referencing those books:
  - Inventory: 29 shelf entries and 9 checkout records.
  - Lending: 25 reservations, 9 loans and 38 outbox events.
  - Notification: 7 notifications and 14 reservation-state rows.
  - Real books, real genres and authors, user accounts, and reservations on real books were explicitly excluded.
- **Done by the team (14 Oct):** the QA books and the QA author are gone. On 14 Oct the live catalogue had 0 QA books out of 77, and 0 QA authors out of 56.
- **Still remaining:** **24 "QA Genre …" genres** are still listed (24 of 35 genres). They can be removed in the admin UI (**Books → Manage genres**), or with `DELETE FROM readingpal_inventory.Genres WHERE Name LIKE 'QA Genre%';`.
- **Lesson:** point the Selenium suite at the local Docker stack (`SELENIUM_FRONTEND_URL`), or at a separate test environment, rather than production. Every test creates its own uniquely named book, so runs against production leave data behind.

## Status

- [x] Lending and Notification App Services created
- [x] Databases exist for all four services; database-name case handling confirmed in practice
- [x] Basic Auth enabled and publish profiles saved as GitHub secrets (both services)
- [x] GitHub variables `LENDING_SERVICE_URL` and `NOTIFICATION_SERVICE_URL` set (with `https://`)
- [x] `ci_notification.yml` / `cd_notification.yml` added
- [x] Notification app settings corrected (JWT issuer/audience, `https://` service URLs, GHCR credentials, Application Insights)
- [x] Notification app converted from sidecar to classic container mode; registry URL restored to `ghcr.io`
- [x] MySQL password reset applied to all four apps; production database access re-verified
- [x] `chore/notification-frontend-url` merged into `develop` (PR #95)
- [x] `develop` merged into `main` (PR #97); all CI and CD workflows succeeded; all four apps on `a1fc611`
- [x] Notification Service deployed; `/health` healthy; authenticated calls return 200 for users and admins
- [x] Static Web App rebuilt with `VITE_NOTIFICATION_API_BASE_URL` (verified in the deployed bundle)
- [x] All ten Kafka topics on the Azure broker; Notification consumer group assigned to all ten, lag 0
- [x] Broker startup command that recreates topics applied (`deploy-aci-kafka.ps1`, 14 Oct 02:39 UTC) and verified
- [x] QA test books and author removed from production
- [ ] Remove the remaining 24 "QA Genre …" genres
- [ ] Remove temporary MySQL firewall rules once no longer needed: `temp-qa-cleanup-2026-10-14` (added for the QA clean-up) and `temp-seed-dilum` (added separately by a teammate; check with them before removing)
- [ ] Commit and open a PR for branch `chore/aci-kafka-topic-init` (`aci-start-with-topics.sh`, `deploy-aci-kafka.ps1`, `AZURE-SETUP.md` update), so the live broker configuration is recorded in the repository
- [ ] Rotate the MySQL admin password to a stronger one (see Credentials)

## Verification checklist

| Check | Result (14 Oct) |
| --- | --- |
| `/health` on all four apps | ✅ All `{"status":"healthy"}` |
| Authenticated notification calls (JWT issuer, audience and key) | ✅ 200 for a regular user and for admin |
| Kafka consumer group `notification-service` | ✅ Assigned all ten topics, lag 0, also re-attached by itself after the broker redeploy |
| Lending publishing to Kafka | ✅ Outbox backlog delivered after the topics were created (offsets on all Lending topics advanced) |
| Notifications created from events | ✅ User reminders; admin new-reservation and cancellation notices with customer names |
| Catalogue for a regular user | ✅ All catalogue endpoints return data; displays correctly |
| Full reserve → accept → return through the live UI | ⏳ Not run as a scripted test after the fixes; admin "new reservation" notifications from real use on 14 Oct show the reserve path working end to end |
| JMeter plan (`tests/performance-tests/lending/lending-load-test.jmx`) against the deployed Lending URL | ⏳ Not yet run (local-vs-deployed comparison required by `CLAUDE.md`) |

## Tooling used

- **Azure CLI** 2.91.0 and **GitHub CLI** 2.102.0, installed on a team machine via `winget`. Most configuration and all read-back checks were done with them. On Windows, a terminal opened before installation won't find `az` on its PATH; call it as `"C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin\az.cmd"` or open a new terminal.
- **Azure Cloud Shell** for the sidecar conversion (step 6).
- **Docker** with the `confluentinc/cp-kafka:8.2.3` image as a throwaway Kafka client: topic listing and creation, consumer-group checks, and local testing of the broker startup script.
- The configuration review, fixes, investigations and this document were produced with AI assistance (Claude Code). Each change to shared or production resources was reviewed and approved by the team before it was applied. Some steps were run by the team themselves: the sidecar conversion, the broker redeploy and the QA data clean-up.

## Credentials

Not recorded in this file. The MySQL admin credentials, `Jwt__Key`, GHCR pull token and publish-profile contents are held by the team outside the repository. Never commit `.env`, publish-profile XML, or connection strings containing real passwords. The MySQL admin password was shared in plain text during the 12 October work, so **rotating it to a stronger one is recommended**, using the password-reset incident procedure above.
