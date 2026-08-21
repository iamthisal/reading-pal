# GitHub Actions Workflow Plan

No workflow YAML files are included yet because the application services have not been implemented.

When the services are added, create workflow files here for CI, image builds, and deployment.

## Suggested Future Workflows

- `ci.yml` - Run backend tests, frontend checks, formatting, and build validation.
- `docker-build.yml` - Build and publish Docker images for changed services.
- `deploy-dev.yml` - Deploy the latest successful builds to the development Azure environment.
- `deploy-prod.yml` - Deploy approved releases to the production Azure environment.

## Suggested GitHub Environments

- `development`
- `production`

Use GitHub Environment protection rules for production deployments.

## Suggested Repository Secrets

- `AZURE_CLIENT_ID`
- `AZURE_TENANT_ID`
- `AZURE_SUBSCRIPTION_ID`
- `AZURE_RESOURCE_GROUP`
- `AZURE_CONTAINER_REGISTRY`
- `MYSQL_CONNECTION_STRING`
- `JWT_SECRET`
- `APPLICATIONINSIGHTS_CONNECTION_STRING`

## Suggested Repository Variables

- `FRONTEND_PORT`
- `USER_SERVICE_PORT`
- `INVENTORY_SERVICE_PORT`
- `LENDING_SERVICE_PORT`
- `NOTIFICATION_SERVICE_PORT`
- `KAFKA_BOOTSTRAP_SERVERS`

## Notes

- Add workflow files only after the corresponding service has real code, tests, and a Dockerfile.
- Prefer one CI workflow for pull requests and separate deployment workflows for environment-specific releases.
- Use path filters so service-specific builds only run when files for that service change.
