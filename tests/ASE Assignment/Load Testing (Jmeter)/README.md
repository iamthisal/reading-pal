# Reading Pal - Load and Performance Testing

Owner: Wickramasinghe G. K. D. K - IT24101516

## Run

Start the four local services, MySQL and Kafka. The runner does not start infrastructure.
User: 5000; Inventory: 5001; Lending: 5148; Notification: 5003.

```powershell
.\scripts\run-parallel-local.ps1 -Preview
.\scripts\run-parallel-local.ps1 -AllowWrites
```

One JMX execution, 60 seconds of repeated traffic by default, with shared pools of
20 customer threads and 2 admin threads. Nine one-off workflow threads preserve endpoint
coverage; two notification read-action setup threads run first. Workflow admin actions
can overlap the admin pool. The 21 scenario sections share pools: do not add their user
counts. Setup, in-flight requests, workflow completion and reporting add to elapsed time.

## Files retained and their purpose

- test-plans/all-services.jmx: the only plan; service/scenario sections are inside it.
- config/local.properties: local API hosts, ports and HTTP timeouts.
- config/load.properties: duration, pacing and console progress settings.
- config/thresholds.properties: error-rate, p95 and sample-count targets.
- config/scenarios.json: scenario labels, shared pools and request-coverage expectations.
- config/local.private.example.json: safe template for local admin credentials.
- config/local.private.json: ignored actual admin credentials; never commit it.
- data/local/: saved fixture accounts/IDs, example format and any recovery manifests.
- scripts/run-parallel-local.ps1: readiness checks, one JMeter execution and final evaluation.
- scripts/check-results.ps1: response-time/error/sample-count acceptance checks.
- scripts/build-suite-report.ps1: grouped terminal metrics and combined HTML report.
- scripts/common.ps1: shared local configuration, JMeter and API helpers.
- scripts/prepare-data.ps1: creates fixtures separately, not during each load run.
- scripts/cleanup-data.ps1: removes owned fixtures when explicitly requested.
- coverage-matrix.md and reports/endpoint-audit.json: endpoint-definition coverage evidence.
- reports/performance-summary.md: template for documenting actual measurements.
- results/local/: generated results and reports; ignored by Git.

## Fixture preparation (once)

Java and JMeter 5.6.3 are required. Configure local.private.json using the example without
overwriting a working file. The default run requires 33 prepared fixture rows. Your existing
40-account dataset is sufficient. Only if a fixture does not already exist:

```powershell
.\scripts\prepare-data.ps1 -Users 40 -SeedNotifications
```

Wait for seeded notifications to arrive. Preparation refuses to overwrite existing manifests.
Reuse saved accounts between runs. Failed-preparation manifests must be retained for reconciliation.
One-off workflows create temporary records as part of endpoint verification; this is not bulk seeding.

## Optional higher-load comparison

```powershell
.\scripts\run-parallel-local.ps1 -AllowWrites -Customers 40
```

Requires 53 prepared accounts (customers + 13). Prepare a replacement only after reconciling
and cleaning the existing fixture. No automatic database reset or reseeding is performed.
Duration can be controlled with -DurationSeconds (default 60). Keep duration, pacing,
data approach and thresholds comparable between runs. -ApplicationCommit records the
actual running application revision; it defaults to UNVERIFIED.

## Results and interpretation

Final results are in results/local/parallel-<timestamp>/index.html and html-report/index.html.
run-overview.json counts actual HTTP requests separately from internal JMeter samples.
endpoint-performance.csv ranks repeated endpoints by p95 and p99. Scenario JSON/CSV files
are diagnostic inputs, not additional JMeter runs. All reporting happens after traffic ends.

Response assertions stay active during traffic. Repeated endpoint checks require at least
20 samples; bounded workflows require one and are functional coverage, not sustained load.
Thresholds are proposed assessment targets, not verified production SLAs. Missing coverage,
assertion failures, excessive errors or slow p95 fail the command. Reports include ramp-up;
they do not claim steady-state-only measurements or maximum system capacity.

Readiness checks verify service health once; there is no timed smoke stage. Authentication
and database use are exercised by real requests. A high user count alone does not establish
stress tolerance. CPU/RAM, database and Kafka monitoring remain external; stop if the local
machine becomes unresponsive. Local results do not establish Azure performance. Fine-date
boundaries, Kafka delivery latency and browser rendering are outside this plan's coverage.

## Cleanup and submission

```powershell
.\scripts\cleanup-data.ps1 -ConfirmFixtureCleanup
```

Cleanup targets only recorded owned resources; history and failed-workflow leftovers may
need manual reconciliation. Keep private files out of Git. Commit the plan, scripts,
non-secret configuration, examples and documentation. The group can call the runner from
its unified automation; nonzero exit means failure. No CI workflow is changed by this suite.
