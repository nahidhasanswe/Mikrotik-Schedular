# Mikrotik PPPoE Scheduler

Clean-architecture .NET 10 solution: **Web API + MVC UI + background service** that pushes *active* PPPoE users to a
MikroTik router through the RouterOS API. SQL Server + EF Core, CQRS via
[`AspNet.Core.Dispatcher`](https://www.nuget.org/packages/AspNet.Core.Dispatcher) (1.0.0-preview.1).

**Sync rule:** a PPPoE user that is *active* in this system is created (or enabled, if it exists but is disabled) in the
router's `/ppp/secret`. Inactive users are ignored — nothing is ever disabled or deleted on the router.

## Layout
| Project | Role |
|---|---|
| `Domain` | Entities: `User`, `MikrotikRouter`, `PppoeUser`, `RefreshToken`, `SyncLog` |
| `Application` | CQRS commands/queries/handlers, validators (FluentValidation), pipeline behaviors, abstractions |
| `Infrastructure` | EF Core + migrations, RouterOS API client, JWT, PBKDF2, Data Protection, `SyncWorker` |
| `Api` | REST controllers (JWT). Hosts the background `SyncWorker` |
| `Web` | MVC UI (cookie auth), talks to the API |

Tables: `Users` 1─* `MikrotikRouters` 1─* `PppoeUsers`; plus `RefreshTokens`, `SyncLogs`.

## Run with Docker
```bash
cp .env.example .env        # set CONNECTION_STRING, JWT_KEY
docker compose up -d --build                      # uses the SQL Server from CONNECTION_STRING
docker compose --profile localdb up -d --build    # ...or also start a local SQL Server container
```
API: http://localhost:5133/health · UI: http://localhost:5200 (first registered user becomes Admin).
The database is created/migrated automatically on API start. Router/PPPoE passwords are encrypted with Data Protection;
the keys live in the `dpkeys` volume — **keep it**, or stored passwords become unreadable.
The router must be reachable from the API container (use its LAN IP; `host.docker.internal` for a router simulated on your machine).

## Run locally
```bash
cd src/MikrotikScheduler.Api
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:Default" "<your connection string>"
dotnet run                                   # http://localhost:5133
cd ../MikrotikScheduler.Web && dotnet run    # Api:BaseUrl in appsettings.json
```

## Router setup
Enable the API service (`/ip service enable api` — port 8728, or `api-ssl` 8729) and create a user with `api,write,policy` rights.

## Testing without a router
`python3 tools/mock_routeros.py 8728 admin secret` starts a fake RouterOS API server.

## Deploy behind jwilder/nginx-proxy + letsencrypt companion (Ubuntu, host SQL Server)
```bash
rsync -avz --exclude bin --exclude obj --exclude .env ./ user@SERVER:/opt/mikrotik-scheduler/
ssh user@SERVER && cd /opt/mikrotik-scheduler
cp .env.prod.example .env && chmod 600 .env     # set DOMAIN, LETSENCRYPT_EMAIL, PROXY_NETWORK, CONNECTION_STRING, JWT_KEY
docker compose -f docker-compose.prod.yml up -d --build
docker compose -f docker-compose.prod.yml logs -f api
```
Requirements: DNS for `DOMAIN` points at the server; SQL Server listens on 0.0.0.0:1433 and the firewall allows Docker's
subnet (`ufw allow from 172.16.0.0/12 to any port 1433`); the server can reach the router's API port.
SQL login needs `db_owner` on the database (the API applies EF migrations on startup).
