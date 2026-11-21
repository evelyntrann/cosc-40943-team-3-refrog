# ReFrog

Phone-first progressive web app for TCU's finals-week move-out donation event. See [`AGENTS.md`](AGENTS.md) for the charter and [`docs/`](docs/) for requirements and design.

## Running locally

You need the .NET 10 SDK and a SQL Server you can throw away (Docker works).

```bash
# 1. A local SQL Server (Apple silicon: turn on Rosetta in Docker Desktop)
docker run -d --name refrog-sql -p 1433:1433 -e ACCEPT_EULA=Y \
  -e MSSQL_SA_PASSWORD='<a strong local-only password>' mcr.microsoft.com/mssql/server:2022-latest

# 2. Local settings. These go in .NET user-secrets, never in source.
cd src/Backend/ReFrog.Server
dotnet user-secrets set "ConnectionStrings:ReFrog" \
  "Server=localhost,1433;Database=ReFrogDev;User Id=sa;Password=<same>;TrustServerCertificate=True"
dotnet user-secrets set "Authentication:VerificationKeys:1" "$(openssl rand -base64 32)"
dotnet user-secrets set "Authentication:ActiveVerificationKeyVersion" "1"
# Optional: emails that become administrators when they register
dotnet user-secrets set "Authentication:AdministratorEmails:0" "<your email>"

# 3. Create or update the database, then run
dotnet run -- --apply-migrations
dotnet run --launch-profile https      # then open https://localhost:7257/register
```

Email goes through the shared `IEmailSender` adapter. Local development uses Mailpit over SMTP: run `docker compose up -d mailpit`, start ReFrog in the Development environment, then inspect captured messages at `http://localhost:8025`. Mailpit listens only on the local machine through the compose port bindings and does not deliver real email. The repository also contains a candidate Microsoft Graph adapter for evaluation ([setup](docs/operations/microsoft-365-email.md)), but no production provider is approved or selected (`OI-25`). A non-development deployment without an approved provider reports email delivery unavailable.

For `UC-AUTH-recover-account`, Development settings supply `Authentication:PublicBaseUrl` as `https://localhost:7257`, matching the HTTPS launch profile. To check delivery, request recovery for a registered account at `/recover-account` and open the reset link in Mailpit. If you change the HTTPS address, override `Authentication:PublicBaseUrl` with that address (environment variable: `Authentication__PublicBaseUrl`). Non-development deployments must supply their own trusted HTTPS application URL through deployment configuration. As specified in [AUTH.md](docs/design/AUTH.md), a missing or invalid URL prevents reset-email dispatch while preserving the generic public acknowledgement.

## Tests

```bash
export REFROG_TEST_SQL='Server=localhost,1433;User Id=sa;Password=<same>;TrustServerCertificate=True'
dotnet build ReFrog.slnx
dotnet test ReFrog.slnx --no-build
```

Each integration test creates and drops its own database. If `REFROG_TEST_SQL` is not set, those tests are reported as **skipped**, not passed. Check the skipped count before you trust a green run.

## Observability

The [proposed observability design](docs/design/observability.md) and [verification runbook](docs/operations/observability.md) track issue #117: health/metrics contracts, useful privacy-safe diagnostics, a [local demo path](docs/operations/observability.md#local-demo-first), and the later platform-stream failure exercise. These changes are design only; endpoints are pending implementation and deployment is deferred. The existing [availability measurement](docs/operations/availability-slo.md) is a local sample, not production availability.
