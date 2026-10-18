# ASE group test automation

The group has selected four areas: unit testing/mocking, mutation testing, UI/E2E testing and load/performance testing. The shared workflow is **.github/workflows/ase-testing.yml**, named **ASE Assignment Tests**.

## Contribution branches and pull requests

Each contributor opens their PR **directly into develop**. The unit-testing-ASE PR includes the unit tests, shared workflow and group runner, plus the already-merged load-testing contribution. No separate integration branch is required.

The unit tests and workflow foundation can be reviewed and merged first. The other suites remain visibly pending. Teammates then add their tests and CI entry points through their own PRs into develop. Before final submission, all four suites must execute and pass with require_all=true.

After the foundation PR is merged into develop, bring the shared setup into an existing contribution branch. First commit or stash your unfinished work, then run:

~~~bash
git fetch origin
git switch your-contribution-branch
git merge origin/develop
~~~

Replace your-contribution-branch with your actual branch name. Resolve any conflicts before committing and pushing. To start a new contribution branch instead:

~~~bash
git fetch origin
git switch --no-track -c ase-my-contribution origin/develop
~~~

Use your own unused branch name in place of ase-my-contribution. Add your suite's folder and run-ci.ps1, push your contribution branch, and choose **develop** as the PR base.

## Current readiness

| Contribution | Folder | Workflow connection |
| --- | --- | --- |
| Unit testing and mocking | Unit Testing (xUnit-Moq) | Connected: all four projects run and produce TRX results. |
| Mutation testing | Mutation Testing (Stryker) | Pending: teammate adds the suite and run-ci.ps1. |
| UI/E2E testing | E2E Testing (Selenium) | Connected: Linux CI runner provisions the local stack and executes four Selenium projects. |
| Load/performance testing | Load Testing (Jmeter) | Local JMeter suite exists; CI environment/fixture adapter run-ci.ps1 is pending. |

The Mutation-Testing-ASE branch currently contains the same load-testing commit as Load-Testing-ASE; its name alone does not establish a mutation implementation. PR #106 supplies the assignment Selenium suite; existing Selenium projects elsewhere in tests remain separate.

Add each student's name and student ID to their suite README before submission. The [unit README](Unit%20Testing%20%28xUnit-Moq%29/README.md) documents the 120 locally passing cases and demo examples. This group workflow is new and still needs a successful GitHub Actions run; local execution is not evidence of a remote workflow pass.

## How one workflow coordinates the group

1. Discovery reports which suite entry points exist.
2. Unit tests run on every matching trigger. Each project is run even if an earlier one fails; any failing project fails the job.
3. Mutation runs after unit tests pass. UI and load use separate runners, so their traffic does not interfere.
4. Each connected suite uploads reports even when its execution fails.
5. The final summary distinguishes success, failure and missing contributions. Missing suites are **PENDING**, not passing tests.

The workflow runs on relevant pushes to main, develop and the four current ASE contribution branches; on pull requests targeting develop with relevant changes; and manually through Actions. Other contributor branches can use a pull request or be added to the push branch list.

The manual Run workflow screen supports **require_all**:

- false: development mode; missing contributions are clearly pending, while any connected suite failure fails the workflow.
- true: final submission mode; all four suites must be connected and pass.

GitHub normally exposes a newly added workflow's manual Run workflow control after the workflow file is present on the default branch. Until then, a qualifying push or pull request can trigger it.

The ASE workflow coordinates the assignment. The application ci.yml also discovers unit projects; its loops now use null-delimited paths so assignment folders containing spaces are passed intact to dotnet.

## Local commands

Run these from the repository root using PowerShell 7:

~~~powershell
pwsh -NoProfile -File "tests/ASE Assignment/scripts/Invoke-AseTests.ps1" -Suite Discover
pwsh -NoProfile -File "tests/ASE Assignment/scripts/Invoke-AseTests.ps1" -Suite Unit
~~~

Once the matching teammate entry point is ready:

~~~powershell
pwsh -NoProfile -File "tests/ASE Assignment/scripts/Invoke-AseTests.ps1" -Suite Mutation
pwsh -NoProfile -File "tests/ASE Assignment/scripts/Invoke-AseTests.ps1" -Suite Ui
pwsh -NoProfile -File "tests/ASE Assignment/scripts/Invoke-AseTests.ps1" -Suite Load
~~~

Missing entry points fail when requested directly. They are not replaced by empty passing scripts. Results default to **.ase-results/** at the repository root; each suite has a separate directory. CI uses the same commands, paths and entry points.

## Teammate handover: run-ci.ps1

Add the entry point at the exact path in the readiness table. Its parameters must be:

~~~powershell
param(
    [Parameter(Mandatory)][string]$ResultsDirectory,
    [Parameter(Mandatory)][string]$ApplicationCommit
)
~~~

This is a parameter contract, not a complete runner. The script must actually run the tests, enforce the suite's pass/fail criteria, and write reports to ResultsDirectory. It must throw or exit nonzero on setup/test/threshold failures. A zero exit with no report files is rejected by the shared runner.

Each job uses PowerShell 7. Unit, mutation and load jobs use fresh Windows runners. The connected UI job uses **ubuntu-24.04** with Linux Docker, Chrome and .NET 10; Node 22 is installed by its setup step. Its entry point builds the frontend, starts the repository's MySQL/Kafka/API stack, waits for readiness, runs the four projects sequentially, saves TRX/log artifacts, and cleans up its disposable environment. A completed runner is not proof that every E2E assertion passes; consult its results.

- **Mutation owner:** pin Stryker through a committed tool manifest, restore it, target the relevant unit projects, configure a meaningful mutation threshold, propagate its exit code and save the mutation report.
- **UI owner:** the connected run-ci.ps1 handles CI infrastructure and test execution. Maintain the Selenium cases and inspect reports for genuine failures. Local runs can explicitly use -UseExistingStack with a prepared local environment; they do not reset local containers or volumes.
- **Load owner:** install/pin JMeter, provision local MySQL/Kafka/services, create fresh private CI credentials and fixtures, wait for notification delivery, then invoke the existing prepare/run/report scripts. Pass ApplicationCommit to the load runner. The current runner deliberately permits local hosts only. Copy public reports to ResultsDirectory; never upload private account manifests, credentials, tokens or HTTP headers.

The load adapter is still pending. Its Windows job must either provision compatible infrastructure or switch to a suitable Linux runner and adapt its Windows-specific JMeter lookup. Merely adding test files or a run-ci.ps1 placeholder does not complete infrastructure integration.

Use disposable CI data rather than production or a developer's persistent database. The workflow has read-only repository permissions and does not deploy or push code. No shared secrets are wired in yet; arrange any required CI secrets explicitly when the owning suite is connected.

## Before the presentation

Complete the pending mutation/load adapters and team ownership details. Run the workflow with require_all=true, confirm all four jobs execute and pass, download the artifacts, and rehearse the two-minute group introduction and four individual demonstrations. Until then this is not a finished four-suite pipeline.
