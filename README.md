# Online Store Microservices (C#)

## Overview
This project is a microservices-based online store demo, built with .NET 8, MassTransit, RabbitMQ, and Docker. It demonstrates asynchronous interservice communication, transactional outbox/inbox patterns, and exactly-once semantics for debiting accounts.

**Services:**
- **ApiGateway**: Routes requests to OrdersService and PaymentsService.
- **OrdersService**: Handles order creation, listing, and status. Publishes payment tasks to a message queue (RabbitMQ) using transactional outbox.
- **PaymentsService**: Manages user accounts, balance, and payment processing. Uses transactional inbox/outbox and ensures at-most-once debiting.
- **RabbitMQ**: Message broker for asynchronous communication.
- **Shared**: DTOs and event contracts shared between services.

## Features
- Transactional Outbox/Inbox for reliable messaging
- Asynchronous order payment workflow
- At-most-once semantics for debiting
- API Gateway for unified entry point
- OpenAPI/Swagger for all services
- Dockerized, ready for `docker compose up`
- >65% code coverage with xUnit tests


## API Endpoints
### OrdersService
- `POST /orders` — Create order (async payment)
- `GET /orders` — List orders
- `GET /orders/{id}` — Get order status

### PaymentsService
- `POST /accounts` — Create account
- `POST /accounts/topup` — Top up account
- `GET /accounts/{userId}` — Get account balance

### ApiGateway
- Proxies `/orders/*` to OrdersService
- Proxies `/accounts/*` to PaymentsService

### Swagger/OpenAPI
- Each service exposes Swagger UI at `/swagger`


## Architecture
- **Transactional Outbox**: OrdersService writes payment tasks to outbox table in the same transaction as order creation. Background service publishes to RabbitMQ.
- **Transactional Inbox/Outbox**: PaymentsService reads payment tasks from RabbitMQ, processes them, and writes results to its outbox for reliable delivery.
- **Exactly-once/At-most-once**: PaymentsService ensures no double debiting via idempotency checks.
- **API Gateway**: Minimal proxy, can be extended for auth/rate limiting.

## Development Notes
- All DTOs/events in `Shared/`
- In-memory EF Core DBs for demo/testing
- Ports and service URLs are hardcoded for demo; use env/config for production