# Docker

This folder is reserved for Docker deployment configuration.

The root `docker-compose.yml` currently runs shared local infrastructure only:

- MySQL
- ZooKeeper
- Kafka

Service Dockerfiles and compose service entries should be added only after each service has real application code.
