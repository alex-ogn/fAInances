# fAInances

fAInances is a simple personal finance tracker that helps you understand, control, and optimize your spending by tracking income, expenses, and monthly budgets with clarity and ease.

## Tech stack

- ASP.NET Core (.NET 10) + Blazor (Interactive Server)
- MongoDB (official `MongoDB.Driver`)
- Cookie authentication

## Project structure

| Project | Responsibility |
| --- | --- |
| `src/FAInances.Web` | Web layer: Blazor pages/components, MVC/API controllers, authentication, app wiring |
| `src/FAInances.Services` | Business layer: entities (`Transaction`, `Category`, `User`), services with the business rules, options, MongoDB access |
| `tests/FAInances.Services.Tests` | xUnit v3 integration tests for the services, run against a real MongoDB |

## Running locally

1. Start MongoDB (Docker):

   ```bash
   docker compose up -d
   ```

2. Run the web app:

   ```bash
   dotnet run --project src/FAInances.Web --launch-profile http
   ```

3. Open http://localhost:5103, create an account and start adding transactions.

Settings live in `src/FAInances.Web/appsettings.json` (`MongoDb` connection and `Finance:Currency`).

## Running tests

```bash
dotnet test
```

The service tests share one MongoDB through an xUnit collection fixture (`MongoDbFixture`). It starts a throwaway MongoDB 7 server with [EphemeralMongo](https://github.com/asimmon/ephemeral-mongo), so no Docker or local MongoDB install is needed. The first run downloads the MongoDB binaries from `downloads.mongodb.org` / `fastdl.mongodb.org` and caches them in your local application data directory.

## REST API

All endpoints require the auth cookie.

- `GET/POST /api/categories`, `GET/PUT/DELETE /api/categories/{id}`
- `GET /api/transactions?year=&month=`, `GET /api/transactions/summary?year=&month=`
- `POST /api/transactions`, `GET/PUT/DELETE /api/transactions/{id}`
