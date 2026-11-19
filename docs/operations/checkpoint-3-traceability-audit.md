# Checkpoint 3 traceability audit

This audit covers application source and tests at [ede1859](https://github.com/evelyntrann/cosc-40943-team-3-refrog/tree/ede18599eafcf4bd913cbe3c1aaa1bda0ade906e) on November 19, 2026. It supports the separate Build and Verify claims in [the matrix](../traceability.md#4-use-case-to-design-code-and-tests). Every defined use case has one matrix row. Six use cases have application implementation evidence at this revision, including the partially built shift-management use case.

Static checks confirmed one row for each of the 23 defined use cases, no duplicate UC/FR rows, and 222 resolving relative Markdown targets and anchors across the matrix and this audit. All 84 linked method references, representing 80 distinct test methods, matched the passing TRX test definitions. The linked GitHub issues, pull requests, release, and source revision were checked through GitHub CLI and Git.

## Execution evidence

The full Release run passed 142 server tests and 46 client tests, with zero failures and zero skips. It used the exact source snapshot in a Linux .NET 10 SDK container, disposable SQL Server data, fake email adapters, local Mailpit where configured, fixed clocks for deadline tests, and two test workers. These results do not establish coverage of absent tests.

| Test group | Server passes | Client passes |
|---|---|---|
| Registration flow, code, schema, and form | 33 | 7 |
| Session flow and sign-in form | 8 | 3 |
| Recovery flow, token, schema, and form | 32 | 12 |
| Volunteer sign-up endpoint, schema, and form | 11 | 6 |
| Administration shift endpoint, schema, store, shift contract, unit of work, and form | 29 | 6 |
| INF event endpoint, event service, configuration service, and view | 15 | 6 |
| Shared configuration, API, email, and time tests | 14 | 6 |
| Total | 142 | 46 |

Reproduce at the audited revision with `REFROG_TEST_SQL` pointing to a disposable SQL Server instance and `MAILPIT_SMTP_HOST` / `MAILPIT_SMTP_PORT` pointing to local Mailpit.

```sh
dotnet test tests/ReFrog.Server.Tests/ReFrog.Server.Tests.csproj -c Release --logger 'trx;LogFileName=fanout-server.trx' -- MSTest.Parallelize.Workers=2
dotnet test tests/ReFrog.Client.Tests/ReFrog.Client.Tests.csproj -c Release --logger 'trx;LogFileName=fanout-client.trx' -- MSTest.Parallelize.Workers=2
```

## Registration

`UC-AUTH-register-account` has a [main-flow and seven-extension map](../traceability.md#registration-audit-2026-11-19). Its targeted 33-server/7-client run and the full run both passed. Verify remains Partial for the named HTTP, rendered-component, cross-purpose, and assertion gaps. Build is Built because the registration implementation exists.

## Sign-in

`UC-AUTH-sign-in` implements session creation and sign-out. Verify remains Partial. The design test list is in [AUTH.md](../design/AUTH.md#tests).

| Flow | Passing test evidence | Coverage limit |
|---|---|---|
| Main | [OrdinaryAccountSignsInForThirtyDaysAndUsesSafeDestination](../../tests/ReFrog.Server.Tests/Authentication/SessionFlowTests.cs), [AcceptedCredentialsClearPasswordAndReturnServerDestination](../../tests/ReFrog.Client.Tests/Authentication/SignInFormTests.cs) | Checks cookie properties and safe destination response. Browser navigation is not asserted. |
| 3a | [UnknownWrongAndLockedCredentialsAreIndistinguishableUntilLockoutEnds](../../tests/ReFrog.Server.Tests/Authentication/SessionFlowTests.cs), [RejectedCredentialsKeepEmailAndPasswordForCorrection](../../tests/ReFrog.Client.Tests/Authentication/SignInFormTests.cs) | Covers credential responses and the lockout boundary. |
| 3b | [ValidationFailureNamesFieldAndUnavailableResponseIsSafe](../../tests/ReFrog.Client.Tests/Authentication/SignInFormTests.cs) | This is a client response test. No test injects a server-side sign-in store failure and checks no session plus redaction. [UnavailableCookieStateReturnsRetryableFailureAndPreservesTheCookie](../../tests/ReFrog.Server.Tests/Authentication/SessionFlowTests.cs) covers an existing session. |
| 4a | [AdministratorAttemptsAreAuditedAndSuccessfulCookieUsesFourHours](../../tests/ReFrog.Server.Tests/Authentication/SessionFlowTests.cs) | Initial expiry and audit are asserted. Sliding renewal and the inactivity boundary are not exercised. |
| 5a | [SignedOutAndNonAdministratorCallersAreRefusedAndNothingIsStored](../../tests/ReFrog.Server.Tests/Administration/AdminShiftEndpointTests.cs) | Protected-operation refusal is asserted. The test does not subsequently assert the caller still has its authenticated session. |
| 6a | [CurrentSessionAndAntiforgeryProtectedSignOutUseSharedEnvelope](../../tests/ReFrog.Server.Tests/Authentication/SessionFlowTests.cs) | Sign-out proof, confirmation, and later unauthorized session request are asserted. |

## Recovery

`UC-AUTH-recover-account` has tests for the main flow and all stated extensions. Verify remains Partial because its accepted-send accounting has a confirmed requirement mismatch, tracked in [#143](https://github.com/evelyntrann/cosc-40943-team-3-refrog/issues/143). The requirement remains authoritative and the correction belongs in code. The full passing suite contains no regression for the history-write failure.

| Flow | Passing test evidence |
|---|---|
| Main | [RecoveryResetsPasswordAndInvalidatesEveryEarlierSession](../../tests/ReFrog.Server.Tests/Authentication/RecoveryFlowTests.cs), [SuccessfulResetClearsCredentialsAndRendersReturnToSignIn](../../tests/ReFrog.Client.Tests/Authentication/RecoveryFormTests.cs) |
| 4a | [KnownUnknownAndLimitedRequestsHaveIdenticalAcknowledgements](../../tests/ReFrog.Server.Tests/Authentication/RecoveryFlowTests.cs) |
| 4b | [DeliveryFailurePreservesCredentialsSessionsAndAllowsImmediateRetry](../../tests/ReFrog.Server.Tests/Authentication/RecoveryFlowTests.cs) with failed, unknown, exception, and timeout data rows |
| 5a | [InvalidPasswordNamesFieldAndDoesNotConsumeToken](../../tests/ReFrog.Server.Tests/Authentication/RecoveryFlowTests.cs), [ServerRejectsMismatchedConfirmationWithoutChangingCredentialsOrConsumingToken](../../tests/ReFrog.Server.Tests/Authentication/RecoveryFlowTests.cs), [ServerRequiresConfirmationWhenTheClientOmitsTheField](../../tests/ReFrog.Server.Tests/Authentication/RecoveryFlowTests.cs), [ResetPageExplainsPasswordRequirementsBeforeSubmission](../../tests/ReFrog.Client.Tests/Authentication/RecoveryFormTests.cs) |
| 6a | [TokenWorksImmediatelyBeforeExpiryAndCannotBeReused](../../tests/ReFrog.Server.Tests/Authentication/RecoveryFlowTests.cs), [ExpiredInvalidAndWrongAccountTokensPreservePasswordAndSessions](../../tests/ReFrog.Server.Tests/Authentication/RecoveryFlowTests.cs), [InvalidOrMissingTokenRendersRequestNewLink](../../tests/ReFrog.Client.Tests/Authentication/RecoveryFormTests.cs) |
| 6b | [FailedPasswordWritePreservesStampPasswordAndSessionsAndTokenCanBeRetried](../../tests/ReFrog.Server.Tests/Authentication/RecoveryFlowTests.cs), [FailedSaveKeepsResetInputAndAllowsRetryWithTheSameToken](../../tests/ReFrog.Client.Tests/Authentication/RecoveryFormTests.cs) |
| Delivery limits | [LimitsUseSixtySecondAndRollingHourBoundariesAcrossNormalizedEmails](../../tests/ReFrog.Server.Tests/Authentication/RecoveryFlowTests.cs), [ConcurrentRecoveryRequestsDispatchOnlyOneMessage](../../tests/ReFrog.Server.Tests/Authentication/RecoveryFlowTests.cs) cover normal persistence. Post-send history-write failure remains uncovered. |

The #143 reproduction came from the fresh-context review at `e2d06ce`. The dispatch-before-history-commit sequence is unchanged in the audited revision. When a synthetic accepting sender succeeds and a trigger rejects `RecoveryRequests` inserts, two immediate requests can dispatch two messages and leave zero history rows. The temporary reproducer was removed after that review, so it is excluded from the 142-test total.

## Volunteer sign-up

`UC-VOL-signup` is implemented but Verify is Partial. The [VOL design test list](../design/VOL.md#tests) includes more cases than the existing endpoint and client tests assert.

| Flow | Passing test evidence | Coverage limit |
|---|---|---|
| Main | [ApprovedVolunteerClaimsFreeSlotAndCapacityDecrements](../../tests/ReFrog.Server.Tests/Volunteer/VolunteerShiftEndpointTests.cs), [LoadPopulatesOpenShiftsList](../../tests/ReFrog.Client.Tests/Volunteer/OpenShiftsFormTests.cs), [SignUpConfirmedSetsConfirmedFeedbackMessage](../../tests/ReFrog.Client.Tests/Volunteer/OpenShiftsFormTests.cs) | Form-state confirmation is checked. Rendered output is not checked. |
| 1a | [RestrictedAccountIsDeniedOnListAndSignUp](../../tests/ReFrog.Server.Tests/Volunteer/VolunteerShiftEndpointTests.cs), [LoadSetsLoadErrorOnServerRefusal](../../tests/ReFrog.Client.Tests/Volunteer/OpenShiftsFormTests.cs) | No restriction-after-listing race or access-store-outage test. |
| 2a | No explicit empty-open-list test found in the cited endpoint or client test files. | The extension remains unverified. |
| 4a | [UnapprovedVolunteerClaimsSlotAsPendingAndHoldsCapacity](../../tests/ReFrog.Server.Tests/Volunteer/VolunteerShiftEndpointTests.cs) checks a later claim against a full shift. | The test-file comment names a last-slot race, but no concurrent last-slot test exists. Exact-start and closed-event direct-POST cases also lack assertions. |
| 4b | [SigningUpForAlreadyAssignedShiftReturnsOkWithAlreadyAssigned](../../tests/ReFrog.Server.Tests/Volunteer/VolunteerShiftEndpointTests.cs), [SignUpAlreadyAssignedSetsAlreadyAssignedFeedbackMessage](../../tests/ReFrog.Client.Tests/Volunteer/OpenShiftsFormTests.cs) | Duplicate-row count is checked. |
| 4c | [SigningUpForDeniedShiftReturnsConflictSignupDenied](../../tests/ReFrog.Server.Tests/Volunteer/VolunteerShiftEndpointTests.cs), [OpenShiftsListAnnotatesAssignedToMeAndExcludesFullAndDeniedShifts](../../tests/ReFrog.Server.Tests/Volunteer/VolunteerShiftEndpointTests.cs) | Denial and own-list exclusion are checked. The different-shift acceptance case is not asserted. |
| 5a | No injected assignment-save failure or lock-timeout test found. | No proof of rollback, no false success, and recovery after the failure. |
| 5b | [SigningUpForAlreadyAssignedShiftReturnsOkWithAlreadyAssigned](../../tests/ReFrog.Server.Tests/Volunteer/VolunteerShiftEndpointTests.cs) | Sequential retry is covered. Concurrent same-account requests, unique-violation recovery, and retry after shift start are untested. |
| 5c | [UnapprovedVolunteerClaimsSlotAsPendingAndHoldsCapacity](../../tests/ReFrog.Server.Tests/Volunteer/VolunteerShiftEndpointTests.cs), [SignUpPendingSetsAwaitingApprovalFeedbackMessage](../../tests/ReFrog.Client.Tests/Volunteer/OpenShiftsFormTests.cs) | Pending status and capacity are checked. Event-scoped role changes and grant/removal races are untested. |

The cookie-authenticated sign-up POST currently accepts requests without antiforgery proof. This conflicts with `FR-AUTH-session-policy` and architecture §8.1 and is tracked in [#144](https://github.com/evelyntrann/cosc-40943-team-3-refrog/issues/144). The requirement remains authoritative and the correction belongs in code. Annual role expiry also remains an unbuilt AUTH/ADM lifecycle dependency. These are separate from the missing 5a test.

The [OpenShiftsForm](../../src/Frontend/ReFrog.Client/Features/Volunteer/OpenShiftsForm.cs) catch path reports failure without reconciling a possibly committed sign-up, while the VOL design calls for outcome reconciliation. This is an implementation gap against the design and `FR-SAVE-unknown-outcome`. Its correction and tests are tracked in [#145](https://github.com/evelyntrann/cosc-40943-team-3-refrog/issues/145).

## Shift management

`UC-ADM-manage-shifts` remains In progress / Partial. [PR #135](https://github.com/evelyntrann/cosc-40943-team-3-refrog/pull/135) is open and its extension 6a implementation is outside the audited source.

| Flow | Passing test evidence | Coverage limit |
|---|---|---|
| Main | [AdministratorCreatesAShiftThatOpensForSignup](../../tests/ReFrog.Server.Tests/Administration/AdminShiftEndpointTests.cs), [ShiftOptionsShowTheCurrentEventItsLocationsHoursAndDefaults](../../tests/ReFrog.Server.Tests/Administration/AdminShiftEndpointTests.cs), [CreatedShiftIsConfirmedOnlyAfterTheServerAnswersAndTheNextFillGetsANewRequestId](../../tests/ReFrog.Client.Tests/Administration/ShiftAdminFormTests.cs) | Creation, UTC conversion, defaults, audit, VOL visibility, and client confirmation are checked. |
| 2a | [SignedOutAndNonAdministratorCallersAreRefusedAndNothingIsStored](../../tests/ReFrog.Server.Tests/Administration/AdminShiftEndpointTests.cs), [AdministratorWhoseRoleWasRemovedIsRefusedOnTheNextRequest](../../tests/ReFrog.Server.Tests/Administration/AdminShiftEndpointTests.cs), [UnreadableRoleStateIsUnavailableNotAGuess](../../tests/ReFrog.Server.Tests/Administration/AdminShiftEndpointTests.cs), [ARefusedCallerSeesTheServersMessageAndNoOptions](../../tests/ReFrog.Client.Tests/Administration/ShiftAdminFormTests.cs) | Authorization and unavailable-state refusals are checked. |
| 4a | [InvalidDetailsNameOnlyTheirFieldsAndStoreNothing](../../tests/ReFrog.Server.Tests/Administration/AdminShiftEndpointTests.cs), [AValueOfTheWrongTypeIsNamedAsItsField](../../tests/ReFrog.Server.Tests/Administration/AdminShiftEndpointTests.cs), [RefusedDetailsNameTheFieldAndKeepEverythingEntered](../../tests/ReFrog.Client.Tests/Administration/ShiftAdminFormTests.cs) | Field cases, no writes, and form input preservation are checked. |
| 4b | [StoreFailureReportsNotRecordedAndARetryCreatesExactlyOneShift](../../tests/ReFrog.Server.Tests/Administration/AdminShiftEndpointTests.cs), [ADroppedResponseIsReportedAsUnconfirmedAndTheRetryReusesTheRequestId](../../tests/ReFrog.Client.Tests/Administration/ShiftAdminFormTests.cs) | Transaction rollback and retry are checked. |
| 6a | No implementation or tests at the audited revision. | Pending in PR #135. Rendered shift-page tests also remain absent. |

Supplementary evidence includes [AdministratorWritesWithoutAValidAntiforgeryTokenAreRefused](../../tests/ReFrog.Server.Tests/Administration/AdminShiftEndpointTests.cs), [SameRequestIdGivesTheSameShiftAndChangedInputIsRefused](../../tests/ReFrog.Server.Tests/Administration/AdminShiftEndpointTests.cs), [LocationDefaultAppliesToLaterShiftsOnlyAndEachChangeIsAudited](../../tests/ReFrog.Server.Tests/Administration/AdminShiftEndpointTests.cs), [LockJoinsAnotherContextsTransactionAndHoldsTheRowUntilItCommits](../../tests/ReFrog.Server.Tests/Administration/ShiftServiceTests.cs), and the schema/store/unit-of-work/time tests named in the matrix.

## Event information

`UC-INF-view-event-info` is Built / Partial. Its [INF design test list](../design/INF.md#tests) includes the page output, while the existing client tests exercise view state.

| Flow | Passing test evidence | Coverage limit |
|---|---|---|
| Main | [AnonymousCallerGetsTheActiveEvent](../../tests/ReFrog.Server.Tests/Info/EventInfoEndpointTests.cs), [ActiveEventShowsItsDatesAsCalendarDates](../../tests/ReFrog.Client.Tests/Info/EventInfoViewTests.cs) | Public data and formatted view values are checked. Actual rendering of dates, daily hours, and overview is untested. |
| 2a | [NoEventReturnsOffSeason](../../tests/ReFrog.Server.Tests/Info/EventInfoEndpointTests.cs), [PlannedClosedAndArchivedEventsAreNotShownAsActive](../../tests/ReFrog.Server.Tests/Info/EventInfoEndpointTests.cs), [OffSeasonIsNotTreatedAsActive](../../tests/ReFrog.Client.Tests/Info/EventInfoViewTests.cs) | Off-season state is checked. The displayed purpose/pending-date text is not asserted. Approved wording remains open under OI-27. |
| Failure and retry | [StoreFailureReturnsTheSharedUnexpectedError](../../tests/ReFrog.Server.Tests/Info/EventInfoEndpointTests.cs), [ServerErrorIsUnavailableAndRetryRecovers](../../tests/ReFrog.Client.Tests/Info/EventInfoViewTests.cs) | The shared error and client-state retry are checked. |

OI-29 already records the distinction between the use-case reference to announcement notes and its Associated Information fields. The audit follows that stated default. No additional announcement field is inferred.

## Release comparison

The existing [v0.2.0 release](https://github.com/evelyntrann/cosc-40943-team-3-refrog/releases/tag/v0.2.0) points to `55a4dc417539ba4c7e05a99b1d1871350793d793`. This audit does not change the tag. Its test totals belong to `ede1859`.

| Use case | Existing v0.2.0 source | Audited main source |
|---|---|---|
| UC-AUTH-register-account | Included | Included, with a later throwing-email-provider test |
| UC-AUTH-sign-in | Included | Included, with later stamp/current-role/session-read tests |
| UC-AUTH-recover-account | Absent | Included |
| UC-ADM-manage-shifts | Creation/defaults included, 6a absent | Creation/defaults included, 6a absent |
| UC-VOL-signup | Absent | Included |
| UC-INF-view-event-info | Included | Included |

PRs #122, #135, #137, #138, and #140 were open when this audit checked GitHub. Their content is excluded from the audited revision. The release plan column in the matrix identifies planned releases. It does not establish that a feature exists in the current tag or deployment.

## Remaining work and completion claim

The matrix audit can be reviewed with the named gaps. Registration, sign-in, recovery, volunteer sign-up, shift management, and event information retain Partial verification. Passing existing tests does not close these gaps. The code corrections in #143, #144, and #145, missing tests, and outstanding design dependencies remain follow-up work. Human approval and merge of the audit PR are still required before the updated matrix is evidence on main.

