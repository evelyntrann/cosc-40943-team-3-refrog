# Registration email degradation evidence

**Issue:** [#116](https://github.com/evelyntrann/cosc-40943-team-3-refrog/issues/116)  
**Requirement and design:** `UC-AUTH-register-account` extension 4b, `ROB-external-service-failure`, [AUTH test list](../design/AUTH.md#tests).  
**Test:** `RegistrationFlowTests.ThrowingEmailProviderReportsUnavailableWithoutLeakingOrSendingTwice` in `tests/ReFrog.Server.Tests/Authentication/RegistrationFlowTests.cs`.

The integration test uses a disposable SQL Server 2022 database and an `IEmailSender` test double that throws after capturing the attempted message. It asserts the unavailable response and safe retry message, absence of a session and account, retention of pending state, no duplicate dispatch on request replay, and absence of test email, password, code, and provider detail from captured logs.

| Run | Temporary source state | Focused test result |
|---|---|---|
| Red | Removed only the `try`/`catch` around `emailSender.SendAsync` in `RegistrationService.CompleteIssueAsync` | 1 failed, 0 passed; command exited 1 |
| Green | Restored the original `try`/`catch` | 1 passed, 0 failed; command exited 0 |
| Regression | Restored source, all `RegistrationFlowTests` | 30 passed, 0 failed, 0 skipped |

All runs used .NET SDK 10 in a Linux Docker container and a disposable SQL Server 2022 container on the same Docker network. The red change was temporary and is absent from the final diff. Reproduce with `REFROG_TEST_SQL` set to a disposable SQL Server connection string and:

```text
dotnet test tests/ReFrog.Server.Tests/ReFrog.Server.Tests.csproj --filter FullyQualifiedName~ThrowingEmailProviderReportsUnavailableWithoutLeakingOrSendingTwice
```

This test covers a thrown email-provider exception. It does not prove a real SMTP timeout, a production provider outage, or any other registration extension.
