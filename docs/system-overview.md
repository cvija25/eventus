# System overview

This diagram shows the services in Eventus. It shows how each service talks to
the others, and which protocol it uses.

```mermaid
flowchart LR
    FE[Flutter app]

    FE -->|HTTP + JWT| GW[API Gateway]

    GW -->|HTTP| IDN[Identity service]
    GW -->|HTTP| ACC[Account service]
    GW -->|HTTP| CAT[Catalog API]

    IDN -->|HTTP: create wallet| ACC
    FE -->|SSE: price stream| CAT

    IDN --> IDNDB[(Identity DB<br/>MongoDB)]
    ACC --> ACCDB[(Account DB<br/>Postgres)]
    CAT --> CATDB[(Catalog DB<br/>Postgres)]

    ACC -->|publish: BetPlaced, SellShares| MQ[[RabbitMQ]]
    MQ -->|command-request queue| GAME[Game service]
    GAME -->|publish: BetApproved, SellSharesApproved| MQ
    MQ -->|command-response queue| ACC

    CAT -->|publish: EventResolved| MQ
    MQ -->|event-resolved queue| ACC

    GAME -->|gRPC: read and update pools| GRPC[Catalog gRPC]
    GRPC --> CATDB

    GRPC -->|publish: PriceUpdate| KAFKA{{Kafka}}
    KAFKA -->|price-update topic| CAT
```

## Services

- **API Gateway.** The single entry point for the app. It sends each request
  to the right service.
- **Identity service.** Handles user sign-up and login. It stores users in
  MongoDB. When a user registers, it asks the Account service to open a
  wallet for that user.
- **Account service.** Holds user wallets and trade history in Postgres. It
  starts a buy or a sell by sending a message. It finishes the trade when it
  gets an answer back.
- **Game service.** Runs the market math. It reads and updates the price of
  an event through the Catalog gRPC service, then reports the result back to
  the Account service.
- **Catalog API.** Stores events (markets) in Postgres. Event owners create
  and resolve events here. It also streams live prices to the app over
  Server-Sent Events (SSE).
- **Catalog gRPC.** A second entry point to the same Catalog database. Only
  the Game service calls it. It is the only service allowed to change the
  price pools, and it announces every price change.
- **RabbitMQ.** Carries trade commands and their results between the Account
  service and the Game service. It also carries the event-resolved message
  from Catalog to Account.
- **Kafka.** Carries price updates from the Catalog gRPC service to the
  Catalog API, so the app can show live prices.

## Why two ways into Catalog

The Catalog API handles requests from users, through the gateway. The Catalog
gRPC service handles requests from the Game service only. Both use the same
database. This keeps user traffic and trade-engine traffic apart.
