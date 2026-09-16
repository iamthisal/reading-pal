# Azure Kafka Setup

This document records Reading Pal's Kafka deployment on Azure Container Instances (ACI). For local Docker setup, see the [local Kafka README](./README.md).

## Scope and verification status

Status recorded on 13 September 2026, based on the deployment details and command output reported by the project team. This document is not an independent audit of the live Azure configuration.

| Item | Status |
| --- | --- |
| Kafka container deployed to ACI | Reported running |
| Broker responds through its internal listener | Verified by the reported topic-listing output |
| Three Inventory topics created | Verified by the reported creation and listing output |
| Public endpoint reachable from a laptop | Verified |
| Inventory producer | Implemented (`KafkaBookEventPublisher`) |
| Producer deployed with Azure connection settings | Configured via App Service environments |
| CRUD-to-Kafka event delivery | Active |
| Notification consumer integration | Not yet verified |

## Deployment overview

```text
Frontend -> Inventory Service -> MySQL
                  |
                  | direct publishing, once implemented and configured
                  v
          Kafka container on ACI
                  |
                  v
          Notification consumer (integration pending)
```

This uses actual Apache Kafka packaged in Confluent's `cp-kafka` image. It is not Confluent Cloud or Azure Event Hubs. No virtual machine or VNet integration is used for this public-endpoint design.

| Resource setting | Reported value |
| --- | --- |
| Subscription | Azure for Students |
| Resource group | `reading-pal-v3` |
| Container instance | `readingpal-kafka` |
| Region | UAE North |
| SKU | Standard |
| Public image | `confluentinc/cp-kafka:8.2.3` |
| Operating system | Linux |
| CPU / memory | 1 vCPU / 2 GiB |
| GPU | None |
| Network | Public |
| Exposed port | TCP `9092` |
| DNS label | `readingpal-kafka-grp13` |
| DNS label reuse | Any reuse (unsecure) |
| Restart policy | Always |
| Command override | Empty; image default startup |
| Optional container-instance log integration | Off in the creation summary |
| Persistent storage | None configured in this design |

Expected public bootstrap address:

```text
readingpal-kafka-grp13.uaenorth.azurecontainer.io:9092
```

Confirm the actual FQDN on the ACI Overview page. Use the DNS name rather than a numeric public IP, which can change after restart. The address has no `https://` prefix and no API path. It is a Kafka endpoint, not a website.

## Broker configuration

The creation summary showed 22 environment variables. The following is the intended configuration supplied for this deployment; the complete saved values have not been independently inspected. Compare against Azure before recreating the resource or troubleshooting a mismatch.

| Environment variable | Intended value |
| --- | --- |
| `CLUSTER_ID` | `MkU3OEVBNTcwNTJENDM2Qk` |
| `KAFKA_NODE_ID` | `1` |
| `KAFKA_PROCESS_ROLES` | `broker,controller` |
| `KAFKA_CONTROLLER_QUORUM_VOTERS` | `1@localhost:9093` |
| `KAFKA_CONTROLLER_LISTENER_NAMES` | `CONTROLLER` |
| `KAFKA_LISTENERS` | `INTERNAL://127.0.0.1:19092,EXTERNAL://0.0.0.0:9092,CONTROLLER://127.0.0.1:9093` |
| `KAFKA_ADVERTISED_LISTENERS` | `INTERNAL://localhost:19092,EXTERNAL://readingpal-kafka-grp13.uaenorth.azurecontainer.io:9092` |
| `KAFKA_LISTENER_SECURITY_PROTOCOL_MAP` | `INTERNAL:PLAINTEXT,EXTERNAL:PLAINTEXT,CONTROLLER:PLAINTEXT` |
| `KAFKA_INTER_BROKER_LISTENER_NAME` | `INTERNAL` |
| `KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR` | `1` |
| `KAFKA_TRANSACTION_STATE_LOG_REPLICATION_FACTOR` | `1` |
| `KAFKA_TRANSACTION_STATE_LOG_MIN_ISR` | `1` |
| `KAFKA_DEFAULT_REPLICATION_FACTOR` | `1` |
| `KAFKA_MIN_INSYNC_REPLICAS` | `1` |
| `KAFKA_NUM_PARTITIONS` | `1` |
| `KAFKA_AUTO_CREATE_TOPICS_ENABLE` | `false` |
| `KAFKA_GROUP_INITIAL_REBALANCE_DELAY_MS` | `0` |
| `KAFKA_LOG_DIRS` | `/tmp/kraft-combined-logs` |
| `KAFKA_LOG_RETENTION_HOURS` | `24` |
| `KAFKA_LOG_SEGMENT_BYTES` | `104857600` |
| `KAFKA_LOG_ROLL_HOURS` | `1` |
| `KAFKA_HEAP_OPTS` | `-Xms256m -Xmx512m` |

The combined broker/controller uses KRaft without ZooKeeper. The internal client listener is `localhost:19092`, and the controller is `localhost:9093`; neither should be exposed publicly. Only external Kafka clients use the public FQDN on port 9092.

## Topic creation and completed internal check

In Azure Portal, open **Container instances -> readingpal-kafka -> Containers -> Connect** and launch `/bin/bash`.

The team ran:

```bash
for topic in book-created book-updated book-deleted; do
  kafka-topics \
    --bootstrap-server localhost:19092 \
    --create \
    --if-not-exists \
    --topic "$topic" \
    --partitions 1 \
    --replication-factor 1
done

kafka-topics --bootstrap-server localhost:19092 --list
```

Reported output:

```text
book-created
book-deleted
book-updated
```

Each topic has one partition and replication factor one. Repeat the creation command if topics are missing after a new testing session; `--if-not-exists` avoids errors for existing topics.

## Next check: public endpoint

Run this in local PowerShell with Docker Desktop running. It launches a temporary client, not another broker, and does not publish a local port.

```powershell
docker run --rm `
  confluentinc/cp-kafka:8.2.3 `
  kafka-topics `
  --bootstrap-server readingpal-kafka-grp13.uaenorth.azurecontainer.io:9092 `
  --list
```

Expected output is the same three topics. Record this check as passed only after observing that output from outside ACI.

## Inventory connection contract

The producer should read these application configuration keys. Add the equivalent environment variables to **Inventory App Service -> Settings -> Environment variables -> App settings** once the implementation's configuration contract is confirmed.

| App Service setting | Value |
| --- | --- |
| `Kafka__BootstrapServers` | `readingpal-kafka-grp13.uaenorth.azurecontainer.io:9092` |
| `Kafka__SecurityProtocol` | `Plaintext` |
| `Kafka__Topics__BookCreated` | `book-created` |
| `Kafka__Topics__BookUpdated` | `book-updated` |
| `Kafka__Topics__BookDeleted` | `book-deleted` |

This broker uses no SASL credentials or TLS certificates. Environment variables configure a producer; they do not implement one. These settings belong to the Inventory backend, not the frontend's `VITE_` settings.

### Agreed publishing approach: direct publishing

1. Validate the CRUD request.
2. Save the change in MySQL.
3. Await the Kafka producer's delivery result.
4. Return the API result according to the agreed failure-handling policy.

There is no separate application-level in-memory queue or outbox in this agreed approach. Kafka clients still use internal buffers.

Developer requirements to review:

- Reuse a `Confluent.Kafka` producer through dependency injection.
- Use the book ID as the message key and an agreed JSON event contract.
- Include an event ID, event type and UTC timestamp.
- Await `ProduceAsync`; do not use unobserved fire-and-forget publishing.
- Enable producer idempotence and use `Acks.All` with a bounded delivery timeout.
- Capture needed book details before deleting its database record.
- Agree whether availability toggles also publish `book-updated`.
- Log failed publishing and define what the API returns when MySQL has already succeeded.

MySQL and Kafka writes are not atomic. A failure after the database save can leave a saved book without an event. Producer idempotence does not prevent duplicate database changes caused by repeated HTTP requests, and `Acks.All` does not add another replica to this single-broker setup.

## End-to-end checks still required

After reviewing and deploying the producer, open a local consumer:

```powershell
docker run --rm -it `
  confluentinc/cp-kafka:8.2.3 `
  kafka-console-consumer `
  --bootstrap-server readingpal-kafka-grp13.uaenorth.azurecontainer.io:9092 `
  --topic book-created `
  --from-beginning
```

Create a synthetic test book through the deployed frontend. Verify both the saved database record and the event's book ID, type and payload. Old events can appear with `--from-beginning`; their presence alone does not verify the new request.

Repeat with `book-updated` after editing and `book-deleted` after deleting. Press Ctrl+C to stop each consumer. Once a Notification consumer exists, separately verify its processing; broker delivery alone does not prove notification delivery.

| Test | Recorded result |
| --- | --- |
| Public topic listing | Pending |
| Add book -> matching `book-created` event | Pending |
| Update book -> matching `book-updated` event | Pending |
| Delete book -> matching `book-deleted` event | Pending |
| Publishing-failure behavior | Pending |
| Notification processing | Pending |

## Manual start/stop and cost control

To stop after testing:

1. Finish pending publishing and consumer checks.
2. Open **Container instances -> readingpal-kafka -> Overview -> Stop**.
3. Wait for the whole container group to show **Stopped**.

Closing or disconnecting the container's Bash console does not stop Kafka. ACI compute billing stops when the container group is stopped; App Service, MySQL and other resources have independent charges.

To resume:

1. Select **Start** in ACI Overview.
2. Check **Containers -> Logs** for successful startup and recurring errors.
3. Verify the FQDN and list topics internally.
4. Recreate missing topics, then repeat the public endpoint check.
5. Generate fresh test events.

Stopping ACI does not preserve container state. This setup has no persistent broker volume, so assume events, topics and consumer offsets can be lost. MySQL book records are separate and are not deleted by stopping Kafka. With direct publishing, do not assume events are queued safely while Kafka is stopped.

When permanently finished, delete only the Kafka container instance and remove its old endpoint from application settings. Do not delete the shared resource group containing the working application and database.

## Troubleshooting and limitations

| Symptom | Check |
| --- | --- |
| Provider registration denied | Subscription owner may need to register `Microsoft.ContainerInstance`; resource-group access does not grant provider registration |
| Container repeatedly restarts | Startup logs, environment variable spelling, loopback controller address, memory and CPU pressure |
| Internal topic listing works but public listing fails | Actual FQDN, exposed TCP 9092, advertised external listener, and client network access |
| Topic not found | Auto-creation is disabled; rerun the explicit topic creation command |
| No events after CRUD | Producer deployment/configuration, awaited publishing, topic names and application logs |
| No Portal edit button for ACI environment variables | Changes generally require an updated container-group deployment; inspect/export configuration before changing it and assume temporary data may be lost |

This is a public, unauthenticated, unencrypted demo broker. Anyone who reaches it can access or alter events. Use synthetic data without personal information or secrets, and keep it stopped outside testing. It has no high availability or durable event guarantee. Investigate recurring heartbeat errors instead of assuming they are harmless merely because the container remains Running.

## References

- [Confluent Kafka Docker configuration](https://docs.confluent.io/platform/8.2/installation/docker/config-reference.html)
- [ACI stop/start and state behavior](https://learn.microsoft.com/en-us/azure/container-instances/container-instances-stop-start)
- [ACI console commands](https://learn.microsoft.com/en-us/azure/container-instances/container-instances-exec)
- [ACI DNS label reuse](https://learn.microsoft.com/en-us/azure/container-instances/how-to-reuse-dns-names)
