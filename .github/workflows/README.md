# GitHub Actions Workflow Plan

This directory contains the GitHub Actions workflows that handle Continuous Integration (CI) and Continuous Deployment (CD) for the ReadingPal services and frontend.

## Active Workflows

- `ci.yml` - CI Pipeline for the User Service and Frontend. Runs unit tests, formatting checks, and builds Docker images for PRs and pushes to `main` and `develop`.
- `cd.yml` - CD Pipeline for the User Service. Deploys the built Docker image to Azure App Services upon successful CI run on `main`.
- `ci_inventry.yml` - Dedicated CI Pipeline for the Inventory Service backend. Builds the .NET project and runs unit tests.
- `cd_inventry.yml` - Dedicated CD Pipeline for the Inventory Service backend. Deploys the built Docker image to Azure App Services.
- `ci_notification.yml` - Dedicated CI Pipeline for the Notification Service backend. Validates config, builds the .NET project, runs unit tests, and builds the Docker image.
- `cd_notification.yml` - Dedicated CD Pipeline for the Notification Service backend. Deploys the built Docker image to Azure App Services after a successful CI run on `main`, with an optional post-deploy health check.
- `azure-static-web-apps-polite-water-0c0e68a00.yml` - Auto-generated CD workflow for the React Frontend using Azure Static Web Apps.

## Deployment Environments

- `development` (Local testing via Docker Compose)
- `preview` (Azure Static Web Apps creates one per pull request into `main`; it calls the production APIs)
- `production` (Azure App Services, Azure Static Web Apps, and Azure Container Instances)

### Production approval gate

Every backend CD job (`cd.yml`, `cd_inventry.yml`, `cd_lending.yml`, `cd_notification.yml`) runs in the GitHub **`production`** environment. With required reviewers set on that environment, a deployment to Azure pauses after CI passes until a team member approves it. To approve, open the workflow run in the **Actions** tab, then choose **Review deployments → Approve and deploy**. Each deployment is also listed under the repository's **Environments**, linked to the service's live URL.

One-time setup, which needs a repository admin: **Settings → Environments → production → Required reviewers**. Add the team members, and optionally tick **Prevent self-review**. Also set **Deployment branches** to `main` only.

## Configured Repository Secrets

- `AZURE_WEBAPP_PUBLISH_PROFILE` (Used by User Service deployment)
- `AZURE_INVENTORY_WEBAPP_PUBLISH_PROFILE` (Used by Inventory Service deployment)
- `AZURE_NOTIFICATION_WEBAPP_PUBLISH_PROFILE` (Used by Notification Service deployment)
- Optional repository variable `NOTIFICATION_SERVICE_URL` (enables the Notification Service post-deploy health check)
- GitHub also manages an auto-generated token for Azure Static Web Apps deployment.

## Notes

- Backend CI/CD is separated by service (`ci.yml` vs `ci_inventry.yml`) to allow independent builds and reduce CI execution time.
- Docker builds use `ghcr.io` for container image hosting.
