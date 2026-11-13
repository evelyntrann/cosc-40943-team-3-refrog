# Microsoft 365 email adapter candidate

This document describes the repository's candidate Microsoft Graph implementation of the shared `IEmailSender` adapter. It does not select or approve Microsoft 365 for production. Production use remains blocked until the team and EAS resolve `OI-25`. Without an approved provider setting, a non-development deployment reports email delivery unavailable.

The Microsoft 365 administrator must register an application for unattended mail sending, authorize Microsoft Graph application `Mail.Send` access for the sender mailbox, and provide a dedicated mailbox address. [Microsoft Graph sendMail](https://learn.microsoft.com/en-us/graph/api/user-sendmail?view=graph-rest-1.0) accepts app-only calls to `/users/{sender}/sendMail`; [Exchange Online application RBAC](https://learn.microsoft.com/en-us/graph/auth-limit-mailbox-access) describes mailbox-scoped access. An unscoped Microsoft Entra `Mail.Send` grant can override that mailbox scope, so the administrator must review the effective permissions.

If EAS approves this adapter under `OI-25`, supply these settings through deployment configuration or a secret store:

| Setting | Purpose |
|---|---|
| `Email:Provider` | `MicrosoftGraph` |
| `Email:MicrosoftGraph:TenantId` | Microsoft Entra tenant ID |
| `Email:MicrosoftGraph:ClientId` | Application ID |
| `Email:MicrosoftGraph:ClientSecret` | Application credential; store as a secret |
| `Email:MicrosoftGraph:SenderAddress` | Authorized Microsoft 365 mailbox address |

The adapter requests a client-credentials token and submits one message. It treats Graph `202 Accepted` as accepted for processing; this is not a delivery receipt. A failed or uncertain submission does not trigger an automatic resend, preserving the registration request's idempotency. No tenant credential or mailbox is committed to the repository, and live delivery requires administrator setup and a controlled end-to-end check.

For local SQL Server migration, set the `ConnectionStrings__ReFrog` environment variable to the same connection string used by the running application. The EF Core design-time factory requires this setting and does not contain a database credential. Run the checked-in migrations with `dotnet-ef database update` before submitting registration requests.
