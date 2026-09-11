# Sprint 02 - Inventory Service Kafka Setup

This document records the local Kafka infrastructure configured for Reading Pal and explains how teammates can start it and test Inventory events.

## Current status

The local setup has been completed and reported working:

- Apache Kafka runs in Docker using KRaft, without ZooKeeper.
- Kafbat Kafka UI is configured for inspecting topics, messages and consumer groups.
- The `book-created`, `book-updated` and `book-deleted` topics have been created locally.
- Inventory Service can run directly on the development machine or in Docker.
- The Inventory container is configured to connect to `kafka:19092` through the shared Docker network.

Still pending:

- Implementing the Kafka producer in the .NET Inventory Service.
- Verifying that successful Inventory CRUD operations publish the expected events.
- Adding automated Inventory/Kafka integration tests to CI.
- Deploying Kafka to Azure. This document covers local infrastructure only.

The infrastructure configuration does not itself implement event publishing. The CRUD-to-Kafka integration must not be marked complete until the tests below pass.

## Configuration files

| File | Purpose |
| --- | --- |
| [docker-compose.yml](./docker-compose.yml) | Standalone KRaft broker, optional Kafka UI, persistent volume and shared network |
| [Root docker-compose.yml](../../docker-compose.yml) | Inventory/MySQL containers and Inventory's Kafka connection settings |
| [Root .env.example](../../.env.example) | Template for local application and database variables |

The root Compose file still contains the older Kafka/ZooKeeper services. They are separate from this KRaft deployment. Do not start both Kafka deployments on the same host port.

## Local configuration

| Component | Configuration |
| --- | --- |
| Kafka image | `apache/kafka:4.1.2` |
| Kafka mode | Single combined broker/controller using KRaft |
| Kafka UI image | `ghcr.io/kafbat/kafka-ui:v1.5.0` |
| Compose project | `readingpal-events` |
| Shared bridge network | `readingpal-events` |
| Kafka data | Named volume `kafka-data`, mounted at `/var/lib/kafka/data` |
| Host bootstrap address | `localhost:9092` |
| Docker-network bootstrap address | `kafka:19092` |
| Kafka UI | <http://localhost:8085> |
| Topic defaults | One partition, replication factor one |
| Automatic topic creation | Disabled; create topics explicitly |
| Message retention | 24 hours; old log segments become eligible for deletion |
| Broker Java heap | 256 MB initial / 512 MB maximum; total process memory can be higher |

The broker health check lists topics every 30 seconds, with a 15-second timeout, five retries and a 60-second startup allowance. Kafka UI waits for the broker to become healthy.

This is a development configuration: Kafka uses plaintext connections, host ports are bound to loopback, and the broker has no redundant copy. It is not a production security or availability configuration. Clients on the Docker network can access the broker without authentication.

## Bootstrap servers explained

The bootstrap address is the first address a Kafka client contacts to discover the broker. It is a connection setting, not an additional component to install.

- An application started directly on the laptop with `dotnet run` uses `localhost:9092`.
- Kafka UI and containers attached to `readingpal-events` use `kafka:19092`.
- Commands below executed using `docker compose exec kafka` run inside the broker container, so they use `kafka:19092`.

Inside a container, `localhost` refers to that container, not the development laptop or another container.

## Start the local environment

Prerequisites: Docker Desktop running with Linux containers, Docker Compose, and PowerShell. Running Inventory directly also requires the project's .NET SDK and a reachable MySQL database.

Run the following commands from the **repository root**. Explicit Compose file arguments keep the old and new Kafka deployments separate.

### 1. Stop the older Kafka deployment

```powershell
docker compose -f docker-compose.yml stop kafka zookeeper
```

This stops the older services without deleting their data.

### 2. Start KRaft Kafka and Kafka UI

```powershell
docker compose -f infrastructure/kafka/docker-compose.yml config --quiet
docker compose -f infrastructure/kafka/docker-compose.yml --profile tools up -d --wait --wait-timeout 180
docker compose -f infrastructure/kafka/docker-compose.yml ps
```

Open <http://localhost:8085>, choose the `readingpal` cluster and inspect **Topics**.

To start only the broker, omit the UI profile:

```powershell
docker compose -f infrastructure/kafka/docker-compose.yml up -d --wait --wait-timeout 180
```

### 3. Create or verify Inventory topics

Topic creation must be repeated on a fresh broker or after deleting its data volume. Topic data is not stored in Git.

```powershell
$inventoryTopics = @("book-created", "book-updated", "book-deleted")

foreach ($topic in $inventoryTopics) {
    docker compose -f infrastructure/kafka/docker-compose.yml exec -T kafka /opt/kafka/bin/kafka-topics.sh --bootstrap-server kafka:19092 --create --if-not-exists --topic $topic --partitions 1 --replication-factor 1
    if ($LASTEXITCODE -ne 0) {
        throw "Could not create topic: $topic"
    }
}

docker compose -f infrastructure/kafka/docker-compose.yml exec kafka /opt/kafka/bin/kafka-topics.sh --bootstrap-server kafka:19092 --list
```

| Inventory operation | Expected topic |
| --- | --- |
| Add a book successfully | `book-created` |
| Update a book successfully | `book-updated` |
| Delete a book successfully | `book-deleted` |

These are Inventory events. The `book-issued`, `book-returned` and `book-overdue` names in the existing root environment template concern Lending and do not replace these topics.

### 4. Start Inventory in Docker

Prepare the root `.env` using `.env.example` if it does not already exist. Supply your team's local database/JWT settings; do not overwrite an existing `.env`.

```powershell
if (!(Test-Path .env)) {
    Copy-Item .env.example .env
}

docker compose -f docker-compose.yml up -d mysql inventory-service
```

Use explicit service names here. A bare `docker compose up -d` at the repository root also starts the older Kafka/ZooKeeper services.

The root Compose file already supplies:

```yaml
Kafka__BootstrapServers: kafka:19092
Kafka__Topics__BookCreated: book-created
Kafka__Topics__BookUpdated: book-updated
Kafka__Topics__BookDeleted: book-deleted
```

Inventory joins both `default` (for MySQL) and `events` (the external `readingpal-events` network). Start the Kafka Compose project first so that external network exists.

### Alternative: run Inventory directly on the laptop

Do not run the Inventory container and the host application on the same port simultaneously. If necessary, stop the Inventory container first:

```powershell
docker compose -f docker-compose.yml stop inventory-service
docker compose -f docker-compose.yml up -d mysql

$env:Kafka__BootstrapServers = "localhost:9092"
$env:Kafka__Topics__BookCreated = "book-created"
$env:Kafka__Topics__BookUpdated = "book-updated"
$env:Kafka__Topics__BookDeleted = "book-deleted"

dotnet run --project services/inventory-service/InventoryService.csproj
```

The application must also have a valid `ConnectionStrings__DefaultConnection` or development connection string. Use the actual MySQL host port and your local credentials. Environment variables set this way affect only processes started from that terminal; .NET does not automatically load the root Compose `.env` file.

During local setup, a MySQL connection failure was resolved by matching Inventory's development connection port to Docker's published port: MySQL was exposed on `3306`, while Inventory had been configured for `33066`. Database migration checks then succeeded and Inventory listened on port `5001`. Keep credentials in local configuration, not in this document.

## Broker-only smoke test

This verifies Kafka messaging without requiring the Inventory producer. It does not prove application integration.

In terminal A, from the repository root:

```powershell
docker compose -f infrastructure/kafka/docker-compose.yml exec kafka /opt/kafka/bin/kafka-console-consumer.sh --bootstrap-server kafka:19092 --topic book-created --from-beginning
```

In terminal B, also from the repository root:

```powershell
docker compose -f infrastructure/kafka/docker-compose.yml exec kafka /opt/kafka/bin/kafka-console-producer.sh --bootstrap-server kafka:19092 --topic book-created
```

Enter the following single-line sample in terminal B and press Enter:

```json
{"eventId":"inventory-demo-001","schemaVersion":1,"eventType":"book-created","bookId":67,"title":"Sample Book","availableCopies":3}
```

Confirm the message appears in terminal A. This payload is illustrative; the final event contract must be agreed with the developer and consumers. Press Ctrl+C to stop the command-line clients; the broker remains running.

## Inventory integration test — pending producer implementation

The Inventory developer must implement a producer that reads the configured bootstrap address and topic names. Publish events only for successful operations. Reliable database-to-Kafka publishing should use an agreed failure/retry strategy, such as a transactional outbox.

After the producer is implemented:

1. Start Kafka, MySQL and Inventory using one of the application modes above.
2. Start the `book-created` consumer shown in the smoke test.
3. Add a book through the application/API using the required authentication.
4. Confirm that the book is saved in MySQL.
5. Confirm a new `book-created` event refers to that book.
6. Repeat with `book-updated`: update the book and confirm the database and event agree.
7. Repeat with `book-deleted`: delete the book and verify the expected database deletion/soft-deletion behavior and event.
8. Check that rejected operations do not publish successful CRUD events.

`--from-beginning` may display old smoke-test messages. Correlate each operation with its book ID and event ID; an old message is not evidence that a new operation published successfully.

Record test outcomes only after execution. Automated integration tests are still pending and should use a temporary Kafka broker on the CI runner rather than depend on Azure.

## Troubleshooting

| Problem | Check or action |
| --- | --- |
| Port 9092 already in use | Stop the older root Kafka service or a conflicting SSH tunnel |
| External network not found | Start the Kafka Compose project before Inventory |
| Inventory container cannot connect | Check network membership and use `kafka:19092`, not `localhost:9092` |
| Host application cannot connect | Use `localhost:9092` and verify the broker is healthy |
| Kafka UI does not start | Include `--profile tools`; inspect broker health |
| No events after CRUD | Producer code is still required; configuration alone does not publish |
| MySQL connection error | Check database container health, published port, credentials and database access separately from Kafka |
| Old messages disappear | Demo retention is 24 hours; Kafka is not permanent business-data storage |

Useful commands, from the repository root:

```powershell
docker compose -f infrastructure/kafka/docker-compose.yml logs kafka --tail 100
docker compose -f infrastructure/kafka/docker-compose.yml logs kafka-ui --tail 100
docker compose -f docker-compose.yml logs inventory-service --tail 100
docker compose -f docker-compose.yml port mysql 3306
```

## Stop and restart

Stop the local Kafka services while preserving containers and stored data:

```powershell
docker compose -f infrastructure/kafka/docker-compose.yml --profile tools stop
```

Restart:

```powershell
docker compose -f infrastructure/kafka/docker-compose.yml --profile tools up -d --wait --wait-timeout 180
```

Do not remove the Kafka data volume unless you intentionally want to discard the broker's topics, messages and consumer offsets. In particular, `docker compose down -v` deletes the associated named volumes.

## Git and handoff

Commit this README and the relevant Compose configuration. Do not commit `.env`, passwords, connection strings, SSH private keys or runtime data. No Azure resource is required for the local setup described here.

The next handoff is to the Inventory developer for producer implementation, followed by the manual and automated integration tests above.
