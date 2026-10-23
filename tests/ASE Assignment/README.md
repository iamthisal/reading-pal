# ReadingPal — ASE Testing Assignment

This README outlines the tools, setup, execution commands, shared pipeline and individual contributions for testing ReadingPal's User, Inventory, Lending and Notification services.

## Team contributions

| Student | Student ID | Feature and responsibility |
| --- | --- | --- |
| Wickramathanthri T. D | IT24101868 | Unit testing and mocking with xUnit, Moq and EF Core InMemory; business rules, boundaries, errors and dependency verification across all four services. |
| Samaraweera S K U S S | IT24101841 | Mutation testing with Stryker.NET; service configurations, mutation execution, scores and HTML/JSON reports. |
| Peiris M D D P | IT24100532 | Selenium E2E testing; customer/admin browser journeys, access control, outage scenarios, test catalogue and results. |
| Wickramasinghe G. K. D. K | IT24101516 | JMeter load and performance testing; concurrent API journeys, fixture preparation, performance thresholds and reports. |

## Contribution locations and readiness

Status checked against fetched branches on **23 October 2026**. The working branch is `unit-testing-ASE`. Fetching a contribution does not add its files to the current checkout; commands for another contribution require a checkout containing its files.

| Suite | Branch | Folder under `tests/ASE Assignment/` | Integration status |
| --- | --- | --- | --- |
| Unit | `unit-testing-ASE` | `Unit Testing (xUnit-Moq)/` | Present here and connected to the shared workflow. |
| Mutation | `Mutation-Testing-ASE` | `Mutation_Testing/` | Configurations, runner and reports available on the contribution branch; shared CI adapter missing. |
| Selenium | `E2E-Testing-ASE` | `E2E Testing (Selenium)/` | Four projects, support library, documentation, recorded results and CI entry point available on the contribution branch. |
| JMeter | `Load-Testing-ASE` | `Load Testing (Jmeter)/` | Local suite present here; the E2E branch also contains the load CI adapter and manual Azure runner. |

These suites are not yet all integrated into this checkout or `origin/develop`. The current shared runner expects `Mutation Testing (Stryker)/run-ci.ps1` and `UI Testing (Selenium)/run-ci.ps1`. The E2E branch corrects the Selenium discovery path to `E2E Testing (Selenium)/run-ci.ps1` and adapts Docker jobs to Linux. Mutation still needs an adapter and discovery path aligned with its actual `Mutation_Testing` folder.

## Tool setup

| Tool | Required for | Setup |
| --- | --- | --- |
| Git | Source and contribution branches | Clone the repository and fetch contribution branches. |
| .NET SDK 10 | Unit, mutation and E2E | Install SDK 10; project packages restore through `dotnet restore` or `dotnet test`. |
| PowerShell 7 (`pwsh`) | Test runners | Install PowerShell 7; run commands from the repository root unless stated otherwise. |
| xUnit 2.9.3, Moq 4.20.72, EF Core InMemory | Unit tests | Restored from test project NuGet references. |
| Stryker.NET | Mutation testing | Install `dotnet-stryker`; see the mutation setup below. |
| Chrome, Selenium WebDriver 4.48 | E2E tests | Install Chrome; restore projects. Selenium Manager resolves ChromeDriver. |
| Docker with Linux containers and Compose | Local E2E and Docker-based CI | Start Docker and prepare the disposable application stack. |
| Node.js 22 and npm | React frontend | Run `npm ci` in `frontend`. |
| Java 17 and Apache JMeter 5.6.3 | Load testing | Install Java, extract JMeter and set `JMETER_HOME` or supply `-JMeterPath`. |

Verify tools:

```powershell
dotnet --version
pwsh --version
git --version
node --version
npm --version
docker compose version
java -version
```

Unit tests call C# classes directly using mocked dependencies and fresh EF Core InMemory databases. Running APIs, MySQL, Kafka and Docker are unnecessary for the unit suite. Selenium and JMeter exercise the running application.

For application-backed tests, configure environment files using `.env.example` and `frontend/.env.example` without overwriting existing settings. Follow each suite's README for stack configuration. Selenium defaults to User Service port 5050; the local JMeter configuration uses 5000. Keep passwords, tokens and private fixture files out of Git and uploaded artifacts.

## Unit testing commands

From the repository root:

```powershell
pwsh -NoProfile -File "tests/ASE Assignment/scripts/Invoke-AseTests.ps1" -Suite Discover
pwsh -NoProfile -File "tests/ASE Assignment/scripts/Invoke-AseTests.ps1" -Suite Unit
```

The runner restores/builds in Release, executes all four projects even when an earlier project fails, saves TRX files to `.ase-results/unit/<Service>/`, and fails if any project fails.

Run one project or a focused demonstration:

```powershell
dotnet test "tests/ASE Assignment/Unit Testing (xUnit-Moq)/UserService.UnitTests/UserService.UnitTests.csproj"
dotnet test "tests/ASE Assignment/Unit Testing (xUnit-Moq)/UserService.UnitTests/UserService.UnitTests.csproj" --filter "FullyQualifiedName~Register_NewEmail_SavesUnapprovedUser"
```

See the [unit README](Unit%20Testing%20%28xUnit-Moq%29/README.md) for coverage, test explanations and demo examples.

## Mutation testing commands

Use a checkout containing the `Mutation-Testing-ASE` contribution. Install Stryker once:

```powershell
dotnet tool install --global dotnet-stryker
dotnet stryker --version
```

The inspected branch has no committed Stryker tool manifest/version pin. Agree and pin a version in a tool manifest for repeatable execution; the command above installs the currently available version.

**Configuration prerequisite:** check production and test project paths in every `stryker-config.json` before running. The inspected User configuration names `UserService.csproj` without its backend directory and references `../../../unit-tests/UserService.Tests/UserService.Tests.csproj`, which does not exist on that branch. Correct paths against the actual checkout first. Retained reports do not prove that the current configuration can reproduce the run.

After correcting configuration paths, run all services:

```powershell
pwsh -NoProfile -File "tests/ASE Assignment/Mutation_Testing/run-mutation.ps1"
```

Or select services from a PowerShell session:

```powershell
& "tests/ASE Assignment/Mutation_Testing/run-mutation.ps1" -Services UserService,LendingService
```

Output: `Mutation_Testing/reports/<Service>.html`, `<Service>.summary.json` and `summary.md`. Raw runs are under each service's ignored `StrykerOutput/` directory. The inspected User configuration uses a break threshold of 0, so execution success does not establish a minimum mutation score.

## Selenium E2E commands

Use a checkout containing `E2E-Testing-ASE`. Follow its E2E README and `TEST-PLAN.md` to prepare Chrome, MySQL, Kafka and the four APIs in Docker. Use a disposable local test stack: the suite changes test data and stops/pauses containers for outage scenarios.

Prepare the frontend in a separate terminal after configuring its API addresses:

```powershell
Set-Location frontend
npm ci
npm run dev
```

From the repository root, run against the prepared stack:

```powershell
pwsh -NoProfile -File "tests/ASE Assignment/E2E Testing (Selenium)/run-ci.ps1" -ResultsDirectory ".ase-results/ui" -ApplicationCommit "$(git rev-parse HEAD)" -UseExistingStack
```

Run one project or case directly:

```powershell
dotnet test "tests/ASE Assignment/E2E Testing (Selenium)/UserService.E2ETests/UserService.E2ETests.csproj" --logger "trx;LogFileName=UserService.E2ETests.trx"
dotnet test "tests/ASE Assignment/E2E Testing (Selenium)/UserService.E2ETests/UserService.E2ETests.csproj" --filter "Id=ACC-04"
```

On its contribution branch, CI provisions a disposable stack and frontend, runs four projects sequentially, retains TRX files and infrastructure logs, then cleans up its infrastructure. Local `-UseExistingStack` mode requires infrastructure prepared by the developer.

## JMeter commands

Start the four APIs, MySQL and Kafka. Install Java and JMeter 5.6.3. From the repository root:

```powershell
Set-Location "tests/ASE Assignment/Load Testing (Jmeter)"
```

Create `config/local.private.json` from `config/local.private.example.json` only if it does not already exist, then supply local admin credentials. Set `JMETER_HOME` to your extracted JMeter directory, or supply the full `bin/jmeter.bat` path with `-JMeterPath`.

Prepare fixtures only if they do not already exist, then wait for seeded notifications:

```powershell
.\scripts\prepare-data.ps1 -Users 40 -SeedNotifications
```

Preview and execute:

```powershell
.\scripts\run-parallel-local.ps1 -Preview
.\scripts\run-parallel-local.ps1 -AllowWrites -ApplicationCommit "$(git rev-parse HEAD)"
```

The default plan uses 20 customer threads, 2 admin threads, additional bounded workflow checks and 60 seconds of repeated traffic. HTML reports and metrics appear under `results/local/parallel-<timestamp>/`. Response assertions and configured error-rate, latency and sample-count checks determine success. Thresholds are assessment targets, not production SLAs.

The E2E branch also includes a load `run-ci.ps1` and `scripts/run-azure.ps1`; use that branch's README for parameters. The local runner in this checkout only permits local hosts.

See the [JMeter README](Load%20Testing%20%28Jmeter%29/README.md) for fixture ownership, cleanup and result interpretation.

## Shared GitHub Actions pipeline

Workflow: [ASE Assignment Tests](../../.github/workflows/ase-testing.yml).

```text
Discover suites ──> Unit tests ──> Mutation (when connected)
                ├─> Selenium (when connected)
                └─> JMeter   (when connected)
                         ↓
                Artifacts + final summary
```

Relevant pushes to `main`, `develop` and the ASE contribution branches, pull requests targeting `develop`, and manual dispatch can trigger execution. Unit runs after discovery; mutation additionally depends on unit success. Selenium and load use separate jobs. Report upload steps use `if: always()` to retain available results after failures.

In **Actions → ASE Assignment Tests → Run workflow**, select the branch and set:

- `require_all=false`: missing adapters remain visibly pending; connected suite failures fail the workflow.
- `require_all=true`: every suite must be connected and pass for final submission.

Once manual dispatch is available for this workflow, an authenticated GitHub CLI can run it:

```powershell
gh workflow run ase-testing.yml --ref unit-testing-ASE -f require_all=false
gh run list --workflow ase-testing.yml --limit 5
gh run view <run-id> --log-failed
gh run download <run-id> --dir .ase-results/downloaded
```

Replace `<run-id>` with an actual run ID. For final submission, use the integrated branch and `require_all=true`. The present checkout cannot pass final mode while suite adapters remain missing.

Shared commands for integrated suite adapters, run from the repository root:

```powershell
pwsh -NoProfile -File "tests/ASE Assignment/scripts/Invoke-AseTests.ps1" -Suite Mutation
pwsh -NoProfile -File "tests/ASE Assignment/scripts/Invoke-AseTests.ps1" -Suite Ui
pwsh -NoProfile -File "tests/ASE Assignment/scripts/Invoke-AseTests.ps1" -Suite Load
```

These commands fail if their expected entry point is missing. They require the contribution files, correct discovery paths and suitable runner environments. The current workflow uses Windows runners; Docker-based E2E/load jobs need the Linux adaptations present on the E2E branch.

### CI entry-point contract

Each adapter must accept:

```powershell
param(
    [Parameter(Mandatory)][string]$ResultsDirectory,
    [Parameter(Mandatory)][string]$ApplicationCommit
)
```

Adapters must provision tools and a suitable environment, execute the real suite, write public reports to `ResultsDirectory`, return nonzero on setup/test/threshold failures, and clean up their infrastructure in a `finally` block. The shared runner rejects success without report files. Align the mutation adapter and discovery path with its actual contribution folder before enabling it.

Expected artifacts are `ase-unit-results`, `ase-mutation-results`, `ase-ui-results` and `ase-load-results`; files are available only when the respective job produces them. Local success and configured workflow files are not evidence of a successful remote CI run.

## Recorded results

These are retained results from **18 October 2026**, not new executions performed while writing this README.

| Suite | Evidence |
| --- | --- |
| Unit | 71 methods, 120 executed cases; 120 passed, 0 failures, 0 skipped. User 20, Inventory 23, Lending 38, Notification 39. |
| Mutation | User 49.06%, Inventory 62.34%, Lending 51.82%, Notification 57.25%. Mutation scores measure detected mutations, not code coverage. |
| Selenium | 187 defined cases, 198 executions. Initial run: 197 passed, 1 failed (member revocation). Rerun with `fix/revoke-member-url` at `f6e9b26`: 198 passed. |

For load results, retain the generated report with its actual environment and application commit. Before submission, merge contributions through PRs into `develop`, resolve adapter/configuration gaps, execute with `require_all=true`, and retain the successful integrated run and artifacts.
