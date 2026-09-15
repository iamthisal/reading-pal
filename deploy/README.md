# Deployment

This folder contains deployment and infrastructure documentation.

The ReadingPal services are actively deployed across Azure and Docker environments using GitHub Actions pipelines.

## Folders

- `docker/` - Docker-related deployment notes, local infrastructure setup, and `docker-compose.yml` configurations for running locally.
- `azure/` - Azure infrastructure notes, covering deployed App Services, Container Instances, and Static Web Apps.

## Configured Infrastructure

- **Azure App Services**: Hosting `user-service` and `inventory-service`.
- **Azure Static Web Apps**: Hosting the React frontend.
- **Azure Container Instances**: Hosting the KRaft-based Apache Kafka broker.
- **GitHub Container Registry (ghcr.io)**: Storing the built Docker images.
- **Application Insights**: Active monitoring and logging.
