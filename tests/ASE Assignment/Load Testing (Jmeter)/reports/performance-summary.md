# Performance assessment results — awaiting real local runs

Owner: Wickramasinghe G. K. D. K — IT24101516

## Method
Service-based plans isolate bottlenecks; mixed tests examine representative cross-service use.
59 controller actions are mapped, plus health checks. Writes/negative cases run as bounded workflows;
read and login profiles measure concurrency. See README for exact scenarios and targets.

## Environment (fill from the actual run)
- Running application commit / configuration:
- Windows, Java, JMeter, MySQL versions:
- CPU/RAM and whether generator shares the application host:
- Accounts/books/loans/notification row counts:
- Service URLs, database port configuration, local Kafka status (NO passwords):
- External monitoring evidence and background activity:

## Runs
| Plan/scenario | Profile | Repeat | Users | Samples | p95 by endpoint | Error rate | RPS | Result/evidence |
|---|---|---|---|---|---|---|---|---|
| Not executed against Reading Pal yet | | | | | | | | |

## Findings
Record failures, slow endpoints, recovery, and differences across three repeats. Include graph/report
references. Do not present synthetic tooling validation as real system performance. Do not alter a
threshold after results merely to turn failure into success.

## Limitations
A fixture-sized dataset is not production scale. Closed workloads do not impose a fixed request arrival
rate. API response time is not browser rendering or Kafka delivery time. Forty users is not proof of
maximum capacity. Long-term resilience and full security/functional correctness require other tests.
Azure runs remain deferred until local verification and agreed environment configuration.

## Individual contribution and viva
Explain your scenario choices, per-thread identity, unique fixture IDs, API assertions, 14-day and
stock checks, private configuration, p95/error/throughput metrics, thresholds and pipeline exit behaviour.
Evidence of execution and your own understanding are essential; generated configuration alone is not a
complete demonstrated assignment. No achieved grade is claimed.
