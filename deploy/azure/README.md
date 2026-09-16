# Azure

This folder contains Azure infrastructure and deployment documentation.

## Configured Resources

The ReadingPal architecture relies on the following active Azure services:

- **Azure App Services**: Hosts the `.NET` backend microservices (`user-service`, `inventory-service`). Deployments are managed via GitHub Actions Publish Profiles.
- **Azure Static Web Apps**: Hosts the React frontend application.
- **Azure Container Instances (ACI)**: Hosts the KRaft-based Apache Kafka broker. For detailed setup and status, see the [Kafka Azure Setup](../../infrastructure/kafka/AZURE-SETUP.md).
- **Azure Application Insights**: Integrated into the backend services for telemetry, distributed tracing, and logging.
- **Azure Database for MySQL**: The primary relational data store for the backend services.
