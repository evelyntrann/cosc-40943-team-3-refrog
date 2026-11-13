# Local email testing with Mailpit

Mailpit is the development-only email inbox for ReFrog. It accepts messages from the shared `IEmailSender` SMTP adapter and displays them without delivering mail to real recipients. This does not resolve or select the production provider tracked by `OI-25`.

## Start Mailpit

From the repository root:

```powershell
docker compose up -d mailpit
```

Mailpit uses its standard local ports:

- SMTP: `localhost:1025`
- Web inbox: `http://localhost:8025`

The compose file binds both ports to `127.0.0.1`, so they are not exposed to other computers.

## Test ReFrog email

Run the application in the Development environment and complete an email flow such as account registration. The application reads the SMTP settings from `src/backend/ReFrog.Server/appsettings.Development.json`. Open `http://localhost:8025` to view the captured verification message and code.

Stop Mailpit when finished:

```powershell
docker compose stop mailpit
```

Do not use Mailpit as a production provider or place production credentials in its configuration.
