# GitHub Actions Workflow Plan

This directory contains the GitHub Actions workflows that handle Continuous Integration (CI) and Continuous Deployment (CD) for the ReadingPal services and frontend.

## Active Workflows

- `ci.yml` - CI Pipeline for the User Service and Frontend. Runs unit tests, formatting checks, and builds Docker images for PRs and pushes to `main` and `develop`.
- `cd.yml` - CD Pipeline for the User Service. Deploys the built Docker image to Azure App Services upon successful CI run on `main`.
- `ci_inventry.yml` - Dedicated CI Pipeline for the Inventory Service backend. Builds the .NET project and runs unit tests.
- `cd_inventry.yml` - Dedicated CD Pipeline for the Inventory Service backend. Deploys the built Docker image to Azure App Services.
- `azure-static-web-apps-polite-water-0c0e68a00.yml` - Auto-generated CD workflow for the React Frontend using Azure Static Web Apps.

## Deployment Environments

- `development` (Local testing via Docker Compose)
- `production` (Azure App Services, Azure Static Web Apps, and Azure Container Instances)

## Configured Repository Secrets

- `AZURE_WEBAPP_PUBLISH_PROFILE` (Used by User Service deployment)
- `AZURE_INVENTORY_WEBAPP_PUBLISH_PROFILE` (Used by Inventory Service deployment)
- GitHub also manages an auto-generated token for Azure Static Web Apps deployment.

## Notes

- Backend CI/CD is separated by service (`ci.yml` vs `ci_inventry.yml`) to allow independent builds and reduce CI execution time.
- Docker builds use `ghcr.io` for container image hosting.
