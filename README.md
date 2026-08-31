# Eventus

Eventus is a real-time market for event prediction. A user picks an event,
such as "Will it rain tomorrow?", and buys shares in Yes or No. The price of
each share shows the current chance of that outcome. The price moves with
every trade. When the event ends, the owner sets the final outcome and every
winning share pays out.

The system is a set of small services. Each service has one job. The
services talk to each other over HTTP, gRPC, and two message brokers
(RabbitMQ and Kafka). See [docs/system-overview.md](docs/system-overview.md)
for a diagram of how the services connect.

## Prerequisites

Install these tools before you start:

- **Docker** and **Docker Compose**. These run all services and their
  databases. This is the only tool you need to run the full system.
- **.NET SDK 10 (preview)**. Install this only if you want to build the code
  or run tests outside Docker.
- **Flutter SDK 3.22 or later**. Install this only if you want to run the
  mobile or web app in `frontend/`.

## Setup and run

1. Clone this repository.
2. Go to the `Services` folder.
3. Copy `.env.example` to `.env`. Change the values if you need custom
   credentials. The defaults work for local use.
4. Run this command:

   ```
   docker compose up --build
   ```

5. Wait until all services report as started.
6. The app now talks to the system through the API Gateway, at
   `http://localhost:1234`.

### Ports

| Service | Port | Notes |
|---|---|---|
| API Gateway | 1234 | Single entry point for all requests |
| Account service | 8080 | |
| Catalog API | 8081 | |
| Game service | 8082 | |
| Catalog gRPC | 8083 | Used by the Game service only |
| Identity service | 8084 | |
| Catalog Postgres DB | 5432 | |
| Account Postgres DB | 5433 | |
| Identity MongoDB | 27017 | |
| RabbitMQ | 5672 (AMQP), 15672 (management UI) | |
| Kafka | 9092 | |
| pgadmin | 5050 | Optional. Browse the Postgres databases |

### Run the frontend app

1. Go to the `frontend` folder.
2. Run `flutter pub get`.
3. Run `flutter run`.

The app expects the API Gateway at `http://localhost:1234`.

### Run the tests

```
dotnet test Services/Services.sln
```

Docker must be running. The integration and end-to-end tests start their own
Postgres, RabbitMQ, MongoDB, and Kafka containers through Testcontainers.

## How to use it: a happy path

This is the main path through the system, from a new user to a paid-out
trade. It matches the end-to-end test at
[`Services/Tests/Eventus.E2E.Tests/HappyPathTests.cs`](Services/Tests/Eventus.E2E.Tests/HappyPathTests.cs).
All requests below go through the API Gateway at `http://localhost:1234`.

1. **Register.**
   `POST /identity/api/v1/identity/register` with a name, email, and
   password. This also opens a wallet for the new user in the Account
   service.
2. **Log in.**
   `POST /identity/api/v1/identity/login` with the email and password. The
   response holds a JWT token. Send this token in the `Authorization` header
   on every request below.
3. **Create an event.**
   `POST /catalog/api/v1/catalog/events` with a title. A new event opens
   with an equal 50/50 chance for Yes and No.
4. **Add funds.**
   `POST /account/api/v1/account/deposit` with an amount. This adds credit
   to the user's wallet.
5. **Buy shares.**
   `POST /account/api/v1/account/buy` with a stake, the event ID, and an
   outcome (Yes or No). The trade settles a short time after this call
   returns. See [docs/buy-flow.md](docs/buy-flow.md) for the full flow.
6. **Check the result.**
   `GET /account/api/v1/account/balance` shows the funds left.
   `GET /account/api/v1/account/transactions` shows the new trade, once it
   settles.
7. **Sell shares (optional).**
   `POST /account/api/v1/account/sell-shares` with the number of shares, the
   event ID, and the outcome. See
   [docs/sell-flow.md](docs/sell-flow.md) for the full flow.
8. **Resolve the event.**
   The event owner calls `POST /catalog/api/v1/catalog/events/resolve` with
   the final outcome. Every user who holds a winning share gets paid. See
   [docs/resolve-flow.md](docs/resolve-flow.md) for the full flow.

## Diagrams

- [docs/system-overview.md](docs/system-overview.md) — how the services
  connect and what they use to talk to each other.
- [docs/buy-flow.md](docs/buy-flow.md) — the buy flow, step by step.
- [docs/sell-flow.md](docs/sell-flow.md) — the sell flow, step by step.
- [docs/resolve-flow.md](docs/resolve-flow.md) — the resolve flow, step by
  step.
