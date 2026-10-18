# Reading Pal - Load and Performance Testing

Owner: Wickramasinghe G. K. D. K - IT24101516

## GitHub Actions

The shared **ASE Assignment Tests** workflow detects this folder's run-ci.ps1. Its load job uses a
separate Ubuntu 24.04 runner with Java 17, downloads JMeter 5.6.3 and verifies its official SHA-512
checksum, starts a disposable MySQL/Kafka/four-service stack, prepares 33 fixture accounts and waits
for notification delivery. It then runs the existing suite with 20 customers and 60 seconds of traffic.
The original assertions, minimum sample counts, p95 limits and error-rate gates are unchanged.

Generated HTML/JMeter/JSON reports and infrastructure logs are uploaded as **ase-load-results**,
including when a gate fails. Private admin credentials and account manifests are not uploaded.
CI cleans up its own containers/volumes; this adapter refuses to provision or reseed a developer's
existing local stack. Use the local commands below for local runs.

The generator and APIs share the CI runner. Results measure that environment; they do not establish
production or Azure capacity. A non-skipped job is not a passing performance result: consult its gates
and downloadable reports.

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

its unified automation; nonzero exit means failure. The shared workflow calls the CI adapter
to run against disposable services on the GitHub runner. Azure testing is run manually
and is not included in the GitHub Actions workflow.

## Azure runs (separate from local)

Backend URLs are configured in config/azure.properties. No frontend server is required
for JMeter API tests.

Create config/azure.private.json using config/azure.private.example.json and enter the
deployed admin credentials. The private file is ignored by Git and must never be committed.

### Prepare fixtures once

For 20 customer threads, the suite requires 33 fixture accounts: 20 customer accounts
plus 13 supporting accounts. Reuse existing complete fixtures rather than preparing
new accounts before every run.

Only run preparation when an Azure fixture manifest does not already exist:

```powershell
.\scripts\prepare-data.ps1 -Environment azure -Users 33 -SeedNotifications
```

Wait for seeded notifications to arrive before running the test.

### Run the test manually

```powershell
.\scripts\run-azure.ps1 -Preview -Customers 20
.\scripts\run-azure.ps1 -AllowWrites -Customers 20 -DurationSeconds 60
```

Azure runs use 20 customer threads, plus two admin threads and separate workflow checks.
The traffic duration is 60 seconds; setup, in-flight requests and reporting add to total
execution time. Local and Azure tests run separately.

### Data and results

Azure fixtures are stored in data/azure/ and generated reports in results/azure/.
Bulk fixture preparation happens outside the timed workload. Individual workflows
still create and modify records to exercise endpoints.

Tests modify deployed data. Most write operations target owned test records, but the
admin mark-all-as-read operation affects the shared admin notification inbox, including
existing notifications.

A local pass does not guarantee an Azure pass. Record timeouts, response-time failures
and incomplete coverage accurately. An endpoint with no samples was not exercised;
it must not be reported as passing.

### Optional cleanup

```powershell
.\scripts\cleanup-data.ps1 -Environment azure -ConfirmFixtureCleanup
```

Cleanup targets recorded test resources. History and failed-workflow leftovers may
require manual reconciliation.

### GitHub Actions scope

GitHub Actions runs the local-environment suite against services started on its own
runner. It does not run the Azure suite. Reports are uploaded even when performance
checks fail, while failed checks remain visible in the test results.

Never upload private credentials or account manifests as artifacts.