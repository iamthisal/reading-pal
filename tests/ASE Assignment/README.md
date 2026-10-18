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
| Load/performance testing | Load Testing (Jmeter) | Connected: Linux CI adapter provisions an isolated stack, prepares fixtures and runs the existing JMeter gates. |

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

Each job uses PowerShell 7. Unit and mutation jobs use fresh Windows runners. The connected UI and load jobs use separate **ubuntu-24.04** runners with Linux Docker and .NET 10. The UI job installs Node 22 and uses Chrome; its entry point builds the frontend, starts MySQL/Kafka/APIs, waits for readiness, runs four projects sequentially, saves TRX/log artifacts and cleans up. The load job installs Java 17 and verifies/downloads JMeter 5.6.3; it prepares 33 fixture accounts on its own stack and runs 20 customers for 60 seconds. Consult actual reports rather than assuming configured suites pass.

- **Mutation owner:** pin Stryker through a committed tool manifest, restore it, target the relevant unit projects, configure a meaningful mutation threshold, propagate its exit code and save the mutation report.
- **UI owner:** the connected run-ci.ps1 handles CI infrastructure and test execution. Maintain the Selenium cases and inspect reports for genuine failures. Local runs can explicitly use -UseExistingStack with a prepared local environment; they do not reset local containers or volumes.
- **Load owner:** the connected run-ci.ps1 handles JMeter installation, infrastructure, fixtures and notification readiness, then invokes the existing prepare/run/report scripts with ApplicationCommit. Maintain the scenarios and interpret actual performance results. Existing thresholds remain unchanged. Private account manifests and credentials remain outside uploaded reports.

Mutation integration is still pending. The UI and load adapters enforce their own results and run on separate machines, so load traffic cannot interfere with Selenium's outage cases. A failed performance gate is a failed job, not a skip or a fabricated pass.

Use disposable CI data rather than production or a developer's persistent database. The workflow has read-only repository permissions and does not deploy or push code. No shared secrets are wired in yet; arrange any required CI secrets explicitly when the owning suite is connected.

## Before the presentation

Complete the pending mutation adapter and team ownership details. Run the workflow with require_all=true, confirm all four jobs execute and pass, download the artifacts, and rehearse the two-minute group introduction and four individual demonstrations. Until then this is not a finished four-suite pipeline.
