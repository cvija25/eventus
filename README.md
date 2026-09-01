# Eventus

Eventus is a real-time market for event prediction. A user picks an event,
such as "Will it rain tomorrow?", and buys shares in Yes or No. The price of
each share shows the current chance of that outcome. The price moves with
every trade. When the event ends, the owner sets the final outcome and every
winning share pays out.

The system is a set of small services. Each service has one job. The
services talk to each other over HTTP, gRPC, and two message brokers
(RabbitMQ and Kafka). See [docs](docs) for a detailed view of the services and use cases.

## Prerequisites

Install these tools before you start:

- **Docker** and **Docker Compose**. These run all services and their
  databases. This is the only tool you need to run the full system. See the
  [Docker Desktop installation guide](https://docs.docker.com/get-started/get-docker/),
  which includes Docker Compose.
- **.NET SDK 10 (preview)**. Install this only if you want to build the code
  or run tests outside Docker. See the
  [.NET installation guide](https://learn.microsoft.com/dotnet/core/install/).
- **Flutter SDK 3.22 or later**. Install this only if you want to run the
  mobile or web app in `frontend/`. See the
  [Flutter installation guide](https://docs.flutter.dev/get-started/install).

## Setup and run

1. Clone this repository.
2. Go to the `Services` folder.
3. Copy `.env.example` to `.env`. Change the values if you need custom
   credentials. The defaults work for local use.
4. Start the services:

   ```
   docker compose up --build
   ```

5. Wait until all services report as started.
6. The app now talks to the system through the API Gateway, at
   `http://localhost:1234`.

### Run the frontend app

Start the backend first, then:

1. Go to the `frontend` folder.
2. Run `flutter pub get`.
3. Pick where to run it. `flutter devices` lists what Flutter can see on your machine.
4. Run the app on one of them:

   ```
   flutter run --release -d chrome     # web
   flutter run --release -d linux      # desktop, on Linux
   ```
5. You're all set! 

### Run the tests

```
dotnet test Services/Services.sln
```

Docker must be running. The integration and end-to-end tests start their own
Postgres, RabbitMQ, MongoDB, and Kafka containers through Testcontainers.

## How to use it: a happy path

This is the main path through the system, from a new user to a paid-out
trade. Walk it in the app, with the backend running. See
[Setup and run](#setup-and-run) and
[Run the frontend app](#run-the-frontend-app) to start both.

The app opens on the **home screen**, a list of all open events. Prices on
the cards update by themselves as other people trade.

1. **Register.**
   Tap the login icon in the top right, then **Register** at the bottom of
   the form. Fill in name, email, password, and confirm password, then tap
   **Register**. This also opens a wallet for the new user in the Account
   service. The form switches back to login mode.
2. **Log in.**
   Enter the same email and password and tap **Login**. The app returns to
   the home screen and keeps the token, so you stay logged in across
   restarts. Two more icons appear in the top bar: **+** to create an event,
   and the logout icon.
3. **Create an event.**
   Tap **+**, type a title, and tap **Create**. The app opens the new
   event's detail screen. Both outcomes start at £0.50, an equal 50/50
   chance.
4. **Add funds.**
   Tap the person icon in the top bar to open **Profile**. Type an amount in
   the field at the bottom and tap **Deposit now**. The balance at the top
   updates.
5. **Buy shares.**
   Open an event from the home screen. Under **Outcome prices**, tap the
   **Yes** or **No** row to pick a side, then enter a whole number of shares
   under **Place order** and tap **Buy shares**. This places the order.
6. **Check the result.**
   The **Your shares on this event** panel at the bottom of the event screen
   shows what you hold here. **Profile** shows the funds left and every
   holding, grouped by event; tap a group to expand it, or tap its title to
   jump to that event.
7. **Sell shares.**
   On the event screen, in **Your shares on this event**, enter how many
   shares to sell and tap **Sell**.
8. **Resolve the event.**
   Only the event's owner sees the **Owner Tools: Resolve Market** panel,
   and only while the event is open. Tap **Resolve YES** or **Resolve NO**.
   The panel is replaced by **Event Market Resolved** with the winning
   outcome, trading closes, and every user who holds a winning share gets
   paid. Check the payout under **Profile**. See
   [docs/resolve-flow.md](docs/resolve-flow.md) for the full flow.

To see the price move from a second account, log out, register a second
user, and buy the opposite outcome. The first user's event screen updates
live.

The same path in raw HTTP calls, all through the API Gateway at
`http://localhost:1234`, is the end-to-end test at
[`Services/Tests/Eventus.E2E.Tests/HappyPathTests.cs`](Services/Tests/Eventus.E2E.Tests/HappyPathTests.cs).

## Diagrams

- [docs/mainDiagram.png](docs/mainDiagram.png) — how the services connect
  and what they use to talk to each other.
- [docs/buy-flow.md](docs/buy-flow.md) — the buy flow, step by step.
- [docs/sell-flow.md](docs/sell-flow.md) — the sell flow, step by step.
- [docs/resolve-flow.md](docs/resolve-flow.md) — the resolve flow, step by
  step.

## Contributors

Lazar Cvijić
Đorđe Marić
Boško Andrić
