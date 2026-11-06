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

Email goes through Microsoft 365 when `Email:Provider` is `MicrosoftGraph` (setup: [docs/operations/microsoft-365-email.md](docs/operations/microsoft-365-email.md)). In Development with no provider configured, each email (including the six-digit code) is written to a file in `.email-pickup/` at the repository root instead of being sent. The folder is git-ignored. In any other environment with no provider configured, sending reports "not sent".

## Tests

```bash
export REFROG_TEST_SQL='Server=localhost,1433;User Id=sa;Password=<same>;TrustServerCertificate=True'
dotnet build ReFrog.slnx
dotnet test ReFrog.slnx --no-build
```

Each integration test creates and drops its own database. If `REFROG_TEST_SQL` is not set, those tests are reported as **skipped**, not passed. Check the skipped count before you trust a green run.
