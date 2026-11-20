# Traceability Matrix

**Project:** ReFrog
**Team:** Team 3
**Version:** 0.1
**Last checked:** Section 4 fan-out audit on 2026-11-19 at `ede1859` by Codex.

---

_**How to use this template.** Instructions appear in italic square brackets. Fill in underneath them and leave them in place until the document is stable._

_**What this file is.** The map of your project, from the reason something exists to the thing that does it. Every other document owns its own contents; this one owns the **links between them**, so nobody has to restate anything to find out what connects to what._

_**Why it exists before any code does.** The question it answers in week 4 is "did we specify everything we promised, and nothing we did not?" The question it answers in week 12 is "this broke, what was it supposed to do, and who asked for it?" Those are the same question, and the file that answers the second one is the file you started in week 4. Starting it later means reconstructing links from memory, which is the same work done worse._

_**It grows.** Week 4 fills in two tables. Week 8 adds the columns for design, code, and tests once those exist, and the tables below leave room for them. Do not add empty columns now._

_**Run the checks by hand this week.** Each table below ends with two checks. They are cheap, and they fail loudly. Hand the whole file to your agent and ask it to run them, then verify what it tells you, because a check reported as passing by the thing that also wrote the table is not evidence._

---

## 1. Feature to use case area

_[Your vision and scope lists product **features** (`FEAT-<slug>`), each a capability a stakeholder can see. Your use case file groups behavior into **use case areas** (`UC-<AREA>-*`). This table connects them, and it is where **business objectives attach**: the objective hangs on the feature, and every use case underneath inherits it. That is why the objective is named here once, rather than repeated on forty use cases._

_Features and areas are deliberately many-to-many. A feature that lands on exactly one area is probably too narrow, and a feature that reads "create, edit, and delete X" is a list of use cases wearing a feature's clothes._

_Worked example, from Project Pulse:]_

| Feature | Business objectives | Use case areas |
|---|---|---|
| `FEAT-performance-tracking` | `BO-PERF-instructor-efficiency`, `BO-PERF-student-participation` | `WAR`, `EVA` |
| `FEAT-administration` | (enabling, serves no objective directly) | `SEC`, `TEA`, `STU`, `INS`, `ACC`, `RUB` |

_[An enabling feature that serves no measurable objective on its own says so, as the second row does. That is different from forgetting to fill the column in.]_

**Checks.**

1. **Every feature has at least one area.** A feature with none means your vision promises something nobody has specified.
2. **Every area is reached by a feature.** An area no feature points at means you have use cases no stakeholder asked for.

---

## 2. Business rule to what enforces it

_[A business rule is a property of your client's business, so it has no implementation of its own. What it has is a set of requirements that enforce it, and this table is how you find them. Rules are **cited, never restated**: the rule's text lives in `business-rules.md` and nowhere else, and this table holds only the identifier and the links._

_The **Source** column is duplicated here from `business-rules.md` on purpose, and it is the one exception in this file. It is the column reviewers and clients actually read, and a rule whose source is one click away is a rule nobody checks._

_Worked example, from Project Pulse:]_

| Business rule | Source | Enforced by |
|---|---|---|
| `BR-evaluation-submission-window` | Not recorded | `UC-EVA-submit-evaluation` step 7 and extension 1b |
| `BR-section-admin-only` | Not recorded | `UC-RUB-assign-rubric`, `UC-SEC-create-section`, `UC-SEC-edit-section`, `UC-SEC-setup-active-weeks` |

**Checks.**

1. **Every rule is enforced by something.** A rule with an empty third column is a policy your software ignores. That may be the right answer, and it is only the right answer if you decided it on purpose. Write the decision in `OPEN-ISSUES.md` rather than leaving the cell blank.
2. **Every requirement that cites a rule cites one that exists.** Search your use cases and specification for every `BR-` identifier and confirm each one is defined in `business-rules.md`. This is the check that catches a draft citing `BR-late-penalty` because a late penalty is the kind of thing a course usually has. A generated document cites plausible identifiers with complete confidence, and nothing else in your process will notice.

---

## 3. From here (week 8)

_[Leave this section as it is until week 8. It says what the two tables above grow into, so that nobody restructures the file in the meantime._

_In week 8 the spine extends past the use case: **business objective → feature → use case area → use case → design → code → test.** A third table arrives with one row per use case and columns for the design element, the source files, and the tests, tracked as two separate states, built and verified, so that a use case which is built but untested stays visibly unverified rather than being absorbed into a single "done".]_

---

## 4. Use case to design, code, and tests

Release values follow the course's release plan: `v0.1.0` is the proving slice (Checkpoint 2), `v0.2.0` is Checkpoint 3, `v1.0.0` is the December MVP, `Spring` is before the late-April go-live, and `Out` is not planned. Rows are ordered by planned release. The AUTH proving slice is tracked by issue #79 and the release-plan cleanup by issue #93. `UC-IDV-verify-affiliation` precedes its consumer, `UC-SHP-log-item-taken`. Planned release assignment and presence in a tagged source are distinct claims.

The [Checkpoint 3 audit](operations/checkpoint-3-traceability-audit.md) records exact tests for the six implemented or partly implemented use cases at `ede1859`, the 142-server/46-client passing run, missing coverage, implementation defects, and the comparison with the existing `v0.2.0` tag. Verify remains Partial wherever the evidence is incomplete or a confirmed defect remains.

| Use case or FR | Design | Release | Build | Code | Tests | Verify |
|---|---|---|---|---|---|---|
| `UC-AUTH-register-account` | [design/AUTH.md](design/AUTH.md#uc-auth-register-account) | v0.1.0 | ✅ Built | [RegistrationService](../src/Backend/ReFrog.Server/Features/Authentication/RegistrationService.cs), [EfAuthRepository](../src/Backend/ReFrog.Server/Features/Authentication/Persistence/EfAuthRepository.cs), [AuthEndpoints](../src/Backend/ReFrog.Server/Features/Authentication/AuthEndpoints.cs), [RegisterPage](../src/Frontend/ReFrog.Client/Features/Authentication/RegisterPage.razor), [RegistrationForm](../src/Frontend/ReFrog.Client/Features/Authentication/RegistrationForm.cs) | [RegistrationFlowTests](../tests/ReFrog.Server.Tests/Authentication/RegistrationFlowTests.cs), [RegistrationCodeTests](../tests/ReFrog.Server.Tests/Authentication/RegistrationCodeTests.cs), [AuthSchemaTests](../tests/ReFrog.Server.Tests/Authentication/AuthSchemaTests.cs), [RegistrationFormTests](../tests/ReFrog.Client.Tests/Authentication/RegistrationFormTests.cs), [flow map and run evidence](#registration-audit-2026-11-19) | 🟡 Partial. Targeted audit at `ede1859` passed 33 server and 7 client tests with no failures or skips (2026-11-19). Remaining gaps include pre-verification HTTP sign-in refusal, rendered registration feedback, cross-purpose API isolation, and narrower assertions identified in the flow map. |
| `UC-AUTH-sign-in` | [AUTH.md](design/AUTH.md#uc-auth-sign-in-and-sign-out) | v0.1.0 | ✅ Built | [SessionService.cs](../src/Backend/ReFrog.Server/Features/Authentication/SessionService.cs), [AuthEndpoints.cs](../src/Backend/ReFrog.Server/Features/Authentication/AuthEndpoints.cs), [SignInPage.razor](../src/Frontend/ReFrog.Client/Features/Authentication/SignInPage.razor) | [SessionFlowTests](../tests/ReFrog.Server.Tests/Authentication/SessionFlowTests.cs), [SignInFormTests](../tests/ReFrog.Client.Tests/Authentication/SignInFormTests.cs), [flow map](operations/checkpoint-3-traceability-audit.md#sign-in) | 🟡 Partial. Existing tests pass at `ede1859`. Server sign-in store-failure coverage, administrator sliding/inactivity boundaries, and session preservation after destination refusal remain unverified. |
| `UC-AUTH-recover-account` | [AUTH.md](design/AUTH.md#uc-auth-recover-account) | v0.1.0 | ✅ Built | [RecoveryService.cs](../src/Backend/ReFrog.Server/Features/Authentication/RecoveryService.cs), [EfRecoveryRepository.cs](../src/Backend/ReFrog.Server/Features/Authentication/Persistence/EfRecoveryRepository.cs), [RecoveryTokenProvider.cs](../src/Backend/ReFrog.Server/Features/Authentication/RecoveryTokenProvider.cs), [AuthCookieValidation.cs](../src/Backend/ReFrog.Server/Features/Authentication/AuthCookieValidation.cs), [ResetPasswordPage.razor](../src/Frontend/ReFrog.Client/Features/Authentication/ResetPasswordPage.razor) | [RecoveryFlowTests](../tests/ReFrog.Server.Tests/Authentication/RecoveryFlowTests.cs), [RecoveryTokenProviderTests](../tests/ReFrog.Server.Tests/Authentication/RecoveryTokenProviderTests.cs), [RecoverySchemaTests](../tests/ReFrog.Server.Tests/Authentication/RecoverySchemaTests.cs), [RecoveryFormTests](../tests/ReFrog.Client.Tests/Authentication/RecoveryFormTests.cs), [flow map](operations/checkpoint-3-traceability-audit.md#recovery) | 🟡 Partial. Main and all extensions have passing tests at `ede1859`, but accepted-email history-write failure violates send-limit accounting ([#143](https://github.com/evelyntrann/cosc-40943-team-3-refrog/issues/143)) and lacks a retained regression. |
| `FR-AUTH-account-signup` | [AUTH.md](design/AUTH.md) | v0.1.0 | ✅ Built | [RegistrationService.cs](../src/Backend/ReFrog.Server/Features/Authentication/RegistrationService.cs) | [registration flow map](#registration-audit-2026-11-19) | 🟡 Partial. Registration coverage and remaining gaps are recorded in the linked flow map. |
| `FR-AUTH-password-policy` | [AUTH.md](design/AUTH.md) | v0.1.0 | ✅ Built | [Identity configuration](../src/Backend/ReFrog.Server/Configuration/ServiceCollectionExtensions.cs) | [RegistrationFlowTests](../tests/ReFrog.Server.Tests/Authentication/RegistrationFlowTests.cs), [RecoveryFlowTests](../tests/ReFrog.Server.Tests/Authentication/RecoveryFlowTests.cs) | 🟡 Partial. Reset policy boundaries pass. Equivalent registration boundary and rendered-feedback coverage is incomplete. |
| `FR-AUTH-session-policy` | [AUTH.md](design/AUTH.md) | v0.1.0 | 🟡 In progress | [SessionService.cs](../src/Backend/ReFrog.Server/Features/Authentication/SessionService.cs), [AuthCookieValidation.cs](../src/Backend/ReFrog.Server/Features/Authentication/AuthCookieValidation.cs) | [SessionFlowTests](../tests/ReFrog.Server.Tests/Authentication/SessionFlowTests.cs), [Checkpoint 3 audit](operations/checkpoint-3-traceability-audit.md) | 🟡 Partial. Sliding/inactivity cases remain untested and VOL antiforgery correction is pending in #144. |
| `FR-AUTH-password-recovery` | [AUTH.md](design/AUTH.md) | v0.1.0 | ✅ Built | [RecoveryService.cs](../src/Backend/ReFrog.Server/Features/Authentication/RecoveryService.cs), [EfRecoveryRepository.cs](../src/Backend/ReFrog.Server/Features/Authentication/Persistence/EfRecoveryRepository.cs), [RecoveryTokenProvider.cs](../src/Backend/ReFrog.Server/Features/Authentication/RecoveryTokenProvider.cs), [AuthCookieValidation.cs](../src/Backend/ReFrog.Server/Features/Authentication/AuthCookieValidation.cs), [ResetPasswordPage.razor](../src/Frontend/ReFrog.Client/Features/Authentication/ResetPasswordPage.razor) | [RecoveryFlowTests](../tests/ReFrog.Server.Tests/Authentication/RecoveryFlowTests.cs), [RecoveryTokenProviderTests](../tests/ReFrog.Server.Tests/Authentication/RecoveryTokenProviderTests.cs), [RecoverySchemaTests](../tests/ReFrog.Server.Tests/Authentication/RecoverySchemaTests.cs), [RecoveryFormTests](../tests/ReFrog.Client.Tests/Authentication/RecoveryFormTests.cs), [flow map](operations/checkpoint-3-traceability-audit.md#recovery) | 🟡 Partial. Existing tests pass, but #143 records a confirmed accepted-send accounting defect. |
| `UC-IDV-verify-affiliation` | [design/IDV.md](design/IDV.md#uc-idv-verify-affiliation-verifying-shopping-eligibility) | v0.1.0 | 📐 Designed | | | |
| `FR-AUTH-affiliation-from-signin` | [AUTH.md](design/AUTH.md) and [IDV.md](design/IDV.md) | v0.1.0 | 🟡 In progress | [RegistrationService.cs](../src/Backend/ReFrog.Server/Features/Authentication/RegistrationService.cs) | [RegistrationFlowTests](../tests/ReFrog.Server.Tests/Authentication/RegistrationFlowTests.cs) | 🟡 Partial. Exact-domain registration verification is tested. The later IDV profile flow is designed. |
| `FR-AUTH-verification-code` | [AUTH.md](design/AUTH.md) and [IDV.md](design/IDV.md) | v0.1.0 | 🟡 In progress | [RegistrationCode.cs](../src/Backend/ReFrog.Server/Features/Authentication/RegistrationCode.cs), [EfAuthRepository.cs](../src/Backend/ReFrog.Server/Features/Authentication/Persistence/EfAuthRepository.cs) | [RegistrationFlowTests](../tests/ReFrog.Server.Tests/Authentication/RegistrationFlowTests.cs), [RegistrationCodeTests](../tests/ReFrog.Server.Tests/Authentication/RegistrationCodeTests.cs), [Checkpoint 3 audit](operations/checkpoint-3-traceability-audit.md) | 🟡 Partial. Registration implementation and tests exist. Shared IDV use and cross-purpose API verification remain pending. |
| `FR-AUTH-verification-limits` | [AUTH.md](design/AUTH.md) and [IDV.md](design/IDV.md) | v0.1.0 | 🟡 In progress | [RegistrationCode.cs](../src/Backend/ReFrog.Server/Features/Authentication/RegistrationCode.cs), [EfAuthRepository.cs](../src/Backend/ReFrog.Server/Features/Authentication/Persistence/EfAuthRepository.cs) | [RegistrationFlowTests](../tests/ReFrog.Server.Tests/Authentication/RegistrationFlowTests.cs), [RegistrationCodeTests](../tests/ReFrog.Server.Tests/Authentication/RegistrationCodeTests.cs), [Checkpoint 3 audit](operations/checkpoint-3-traceability-audit.md) | 🟡 Partial. Registration implementation and tests exist. Shared IDV use and cross-purpose API verification remain pending. |
| `FR-AUTH-verification-ownership` | [AUTH.md](design/AUTH.md) and [IDV.md](design/IDV.md) | v0.1.0 | 🟡 In progress | [RegistrationCode.cs](../src/Backend/ReFrog.Server/Features/Authentication/RegistrationCode.cs), [EfAuthRepository.cs](../src/Backend/ReFrog.Server/Features/Authentication/Persistence/EfAuthRepository.cs) | [RegistrationFlowTests](../tests/ReFrog.Server.Tests/Authentication/RegistrationFlowTests.cs), [RegistrationCodeTests](../tests/ReFrog.Server.Tests/Authentication/RegistrationCodeTests.cs), [Checkpoint 3 audit](operations/checkpoint-3-traceability-audit.md) | 🟡 Partial. Registration implementation and tests exist. Shared IDV use and cross-purpose API verification remain pending. |
| `FR-VAL-server-validation` | | v0.1.0 | ❌ Not started | | | |
| `FR-VAL-field-feedback` | | v0.1.0 | ❌ Not started | | | |
| `FR-VAL-preserve-input` | | v0.1.0 | ❌ Not started | | | |
| `FR-SAVE-confirmed-success` | | v0.1.0 | ❌ Not started | | | |
| `UC-ADM-manage-shifts` | [ADM.md](design/ADM.md#uc-adm-manage-shifts-create-and-configure-volunteer-shifts) | v0.2.0 | 🟡 In progress | [AdminShiftEndpoints.cs](../src/Backend/ReFrog.Server/Features/Administration/AdminShiftEndpoints.cs), [ShiftAdminService.cs](../src/Backend/ReFrog.Server/Features/Administration/ShiftAdminService.cs), [ShiftService.cs](../src/Backend/ReFrog.Server/Features/Administration/ShiftService.cs), [EfShiftStore.cs](../src/Backend/ReFrog.Server/Features/Administration/Persistence/EfShiftStore.cs), [ShiftAdminPage.razor](../src/Frontend/ReFrog.Client/Features/Administration/ShiftAdminPage.razor) | [AdminShiftEndpointTests](../tests/ReFrog.Server.Tests/Administration/AdminShiftEndpointTests.cs), [ShiftServiceTests](../tests/ReFrog.Server.Tests/Administration/ShiftServiceTests.cs), [ShiftStoreTests](../tests/ReFrog.Server.Tests/Administration/ShiftStoreTests.cs), [SqlUnitOfWorkTests](../tests/ReFrog.Server.Tests/Administration/SqlUnitOfWorkTests.cs), [AdmSchemaTests](../tests/ReFrog.Server.Tests/Administration/AdmSchemaTests.cs), [EventConfigServiceTests](../tests/ReFrog.Server.Tests/Info/EventConfigServiceTests.cs), [ShiftAdminFormTests](../tests/ReFrog.Client.Tests/Administration/ShiftAdminFormTests.cs), [EventTimeTests](../tests/ReFrog.Client.Tests/Time/EventTimeTests.cs), [flow map](operations/checkpoint-3-traceability-audit.md#shift-management) | 🟡 Partial. Main, 2a, 4a, and 4b tests pass at `ede1859`. Extension 6a is absent from this revision and pending in [PR #135](https://github.com/evelyntrann/cosc-40943-team-3-refrog/pull/135). Rendered-page coverage remains missing. |
| `UC-VOL-signup` | [VOL.md](design/VOL.md#uc-vol-signup-signing-up-for-a-shift) | v0.2.0 | ✅ Built | [AssignmentService.cs](../src/Backend/ReFrog.Server/Features/Volunteer/AssignmentService.cs), [VolunteerEndpoints.cs](../src/Backend/ReFrog.Server/Features/Volunteer/VolunteerEndpoints.cs), [VolunteerAuthorization.cs](../src/Backend/ReFrog.Server/Features/Volunteer/VolunteerAuthorization.cs), [EfVolStore.cs](../src/Backend/ReFrog.Server/Features/Volunteer/Persistence/EfVolStore.cs), [OpenShiftsPage.razor](../src/Frontend/ReFrog.Client/Features/Volunteer/OpenShiftsPage.razor) | [VolunteerShiftEndpointTests](../tests/ReFrog.Server.Tests/Volunteer/VolunteerShiftEndpointTests.cs), [VolSchemaTests](../tests/ReFrog.Server.Tests/Volunteer/VolSchemaTests.cs), [OpenShiftsFormTests](../tests/ReFrog.Client.Tests/Volunteer/OpenShiftsFormTests.cs), [flow map](operations/checkpoint-3-traceability-audit.md#volunteer-sign-up) | 🟡 Partial. Existing tests pass at `ede1859`. Explicit 2a empty-list and 5a failed-save tests, concurrent capacity/duplicate tests, and lifecycle cases are missing. Antiforgery and unknown-outcome defects are tracked in [#144](https://github.com/evelyntrann/cosc-40943-team-3-refrog/issues/144) and [#145](https://github.com/evelyntrann/cosc-40943-team-3-refrog/issues/145). |
| `UC-VOL-cancel-shift` | [design/VOL.md](design/VOL.md) | v0.2.0 | 📐 Designed | | | |
| `UC-ADM-review-volunteer` | [design/ADM.md](design/ADM.md) | v0.2.0 | 📐 Designed | | | |
| `FR-AUTH-enforce-permissions` | [Architecture §8.1](design/architectural-design.md#81-security) | v0.2.0 | 🟡 In progress | [AdminAuthorization.cs](../src/Backend/ReFrog.Server/Features/Administration/AdminAuthorization.cs), [VolunteerAuthorization.cs](../src/Backend/ReFrog.Server/Features/Volunteer/VolunteerAuthorization.cs) | [Checkpoint 3 audit](operations/checkpoint-3-traceability-audit.md) | 🟡 Partial. AUTH/ADM/VOL checks have passing tests. Complete lifecycle and future-area authorization remain pending. |
| `FR-SAVE-duplicate-submission` | | v0.2.0 | ❌ Not started | | | |
| `FR-SAVE-unknown-outcome` | | v0.2.0 | ❌ Not started | | | |
| `UC-VOL-view-schedule` | [design/VOL.md](design/VOL.md) | v1.0.0 | 📐 Designed | | | |
| `UC-VOL-request-access` | [design/VOL.md](design/VOL.md#uc-vol-request-access-requesting-volunteer-access) | v1.0.0 | 📐 Designed | | | |
| `UC-ADM-review-volunteer-request` | | v1.0.0 | ❌ Not started | | | |
| `UC-ADM-grant-volunteer-access` | | v1.0.0 | ❌ Not started | | | |
| `UC-ADM-remove-volunteer-access` | | v1.0.0 | ❌ Not started | | | |
| `UC-ADM-lift-account-restriction` | | v1.0.0 | ❌ Not started | | | |
| `UC-ADM-view-dashboard` | [design/ADM.md](design/ADM.md) | v1.0.0 | 📐 Designed | | | |
| `UC-ADM-notify-volunteers` | [design/ADM.md](design/ADM.md) | v1.0.0 | 📐 Designed | | | |
| `UC-INF-view-event-info` | [INF.md](design/INF.md#uc-inf-view-event-info-view-overall-event-dates-and-general-information) | v1.0.0 | ✅ Built | [InfoEndpoints.cs](../src/Backend/ReFrog.Server/Features/Info/InfoEndpoints.cs), [InfoService.cs](../src/Backend/ReFrog.Server/Features/Info/InfoService.cs), [EfEventInfoStore.cs](../src/Backend/ReFrog.Server/Features/Info/Persistence/EfEventInfoStore.cs), [EventInfoPanel.razor](../src/Frontend/ReFrog.Client/Features/Info/EventInfoPanel.razor), [EventInfoView.cs](../src/Frontend/ReFrog.Client/Features/Info/EventInfoView.cs) | [EventInfoEndpointTests](../tests/ReFrog.Server.Tests/Info/EventInfoEndpointTests.cs), [InfoServiceTests](../tests/ReFrog.Server.Tests/Info/InfoServiceTests.cs), [EventInfoViewTests](../tests/ReFrog.Client.Tests/Info/EventInfoViewTests.cs), [flow map](operations/checkpoint-3-traceability-audit.md#event-information) | 🟡 Partial. Main and 2a API/view-state tests pass at `ede1859`. Actual rendered dates/hours/overview and off-season text lack assertions. Approved off-season wording remains open under `OI-27`. |
| `UC-INF-view-locations` | [design/INF.md](design/INF.md) | v1.0.0 | 📐 Designed | | | |
| `UC-INF-view-role-guides` | [design/INF.md](design/INF.md) | v1.0.0 | 📐 Designed | | | |
| `UC-INF-view-install-guide` | [design/INF.md](design/INF.md#uc-inf-view-install-guide-view-app-installation-guide-and-device-instructions) | v1.0.0 | 📐 Designed | | | |
| `FR-AUTH-volunteer-role` | | v1.0.0 | ❌ Not started | | | |
| `FR-AUTH-volunteer-lifecycle` | | v1.0.0 | ❌ Not started | | | |
| `FR-NOTIFY-in-app-inbox` | | v1.0.0 | ❌ Not started | | | |
| `FR-NOTIFY-preserve-business-state` | | v1.0.0 | ❌ Not started | | | |
| `FR-DISPLAY-unavailable-data` | | v1.0.0 | ❌ Not started | | | |
| `UC-DON-log-donation` | [design/DON.md](design/DON.md) | Spring | 📐 Designed | | | |
| `UC-SHP-log-item-taken` | [design/SHP.md](design/SHP.md) | Spring | 📐 Designed | | | |
| `UC-ABU-review-alert` | [design/ABU.md](design/ABU.md) | Spring | 📐 Designed | | | |
| `FR-AUTH-shopping-eligibility` | [design/IDV.md](design/IDV.md) (IDV's part only) | Spring | ❌ Not started | | | |
| `FR-NOTIFY-shift-reminder` | | Spring | ❌ Not started | | | |
| `FR-NOTIFY-current-state` | | Spring | ❌ Not started | | | |

---

### Registration audit (2026-11-19)

This audit covers `UC-AUTH-register-account` at [ede18599eafcf4bd913cbe3c1aaa1bda0ade906e](https://github.com/evelyntrann/cosc-40943-team-3-refrog/tree/ede18599eafcf4bd913cbe3c1aaa1bda0ade906e). Issue [#93](https://github.com/evelyntrann/cosc-40943-team-3-refrog/issues/93) identifies the duplicate AUTH release rows. The consolidated registration row retains its existing `v0.1.0` assignment.

The targeted Release run passed all 33 server tests (30 `RegistrationFlowTests`, two `RegistrationCodeTests`, one `AuthSchemaTests`) and seven client `RegistrationFormTests`, with zero failures and zero skips. The run used a Linux .NET 10 SDK container, disposable SQL Server data, fixed clocks, fake email, and two test workers. Runtime results apply to the audited revision. The missing tests below remain gaps despite this passing run.

Reproduce against that revision with `REFROG_TEST_SQL` configured for a disposable SQL Server instance.

```sh
dotnet test tests/ReFrog.Server.Tests/ReFrog.Server.Tests.csproj -c Release --filter 'FullyQualifiedName~RegistrationFlowTests|FullyQualifiedName~RegistrationCodeTests|FullyQualifiedName~AuthSchemaTests' --logger 'trx;LogFileName=registration-server.trx' -- MSTest.Parallelize.Workers=2
dotnet test tests/ReFrog.Client.Tests/ReFrog.Client.Tests.csproj -c Release --filter FullyQualifiedName~RegistrationFormTests --logger 'trx;LogFileName=registration-client.trx' -- MSTest.Parallelize.Workers=2
```

The following test methods passed in that run. The assertions were also inspected against `UC-AUTH-register-account` and the test list in [AUTH.md](design/AUTH.md#tests).

| Flow | Exact test methods | Coverage limits found |
|---|---|---|
| Main | [RegistrationCreatesAccountOnlyAfterCodeAndPersistsExactTcuVerification](../tests/ReFrog.Server.Tests/Authentication/RegistrationFlowTests.cs#L25), [NonTcuRegistrationDoesNotEstablishAffiliation](../tests/ReFrog.Server.Tests/Authentication/RegistrationFlowTests.cs#L256), [SuccessfulVerificationClearsPasswordAndCode](../tests/ReFrog.Client.Tests/Authentication/RegistrationFormTests.cs#L137) | Registration confirmation and the TCU badge lack rendered-component assertions. |
| 3a | [DuplicateEmailCannotCreateAnotherAccount](../tests/ReFrog.Server.Tests/Authentication/RegistrationFlowTests.cs#L117) | Checks account and email counts. Does not assert that pending-registration count stays unchanged or that sign-in/recovery guidance is returned. |
| 3b | [InvalidInputNamesFieldsWithoutCreatingPendingState](../tests/ReFrog.Server.Tests/Authentication/RegistrationFlowTests.cs#L83), [EmailLongerThanStorageIsAValidationFailure](../tests/ReFrog.Server.Tests/Authentication/RegistrationFlowTests.cs#L603), [ValidationFailureNamesTheFieldAndKeepsTheInput](../tests/ReFrog.Client.Tests/Authentication/RegistrationFormTests.cs#L18) | Field feedback and input preservation are checked in form state. The design also calls for Blazor component coverage. |
| 4a | [RequestLimitsUseTheFixedClockAndProvideRetryTime](../tests/ReFrog.Server.Tests/Authentication/RegistrationFlowTests.cs#L138), [DestinationEmailLimitAppliesAcrossPendingRegistrations](../tests/ReFrog.Server.Tests/Authentication/RegistrationFlowTests.cs#L269), [ResendWaitsForTheServersResendTime](../tests/ReFrog.Client.Tests/Authentication/RegistrationFormTests.cs#L114) | Boundary and retry-time assertions exist. Client timing is checked in form state without rendering the resend button or retry feedback. |
| 4b | [FailedInitialDeliveryKeepsPendingRegistrationAndNeverClaimsSent](../tests/ReFrog.Server.Tests/Authentication/RegistrationFlowTests.cs#L362), [ThrowingEmailProviderReportsUnavailableWithoutLeakingOrSendingTwice](../tests/ReFrog.Server.Tests/Authentication/RegistrationFlowTests.cs#L379), [ResendSupersedesEarlierCodeEvenWhenDeliveryFails](../tests/ReFrog.Server.Tests/Authentication/RegistrationFlowTests.cs#L60), [RetryAfterLostConnectionReusesTheStartRequestId](../tests/ReFrog.Client.Tests/Authentication/RegistrationFormTests.cs#L38) | Explicit adapter timeout coverage and successful use of the replacement code after failed resend are not asserted by these tests. |
| 5a | [MalformedCodeDoesNotConsumeAnAttempt](../tests/ReFrog.Server.Tests/Authentication/RegistrationFlowTests.cs#L227), [FiveIncorrectCodesInvalidateTheChallenge](../tests/ReFrog.Server.Tests/Authentication/RegistrationFlowTests.cs#L166), [CodeExpiresAtTenMinutes](../tests/ReFrog.Server.Tests/Authentication/RegistrationFlowTests.cs#L185), [ResendSupersedesEarlierCodeEvenWhenDeliveryFails](../tests/ReFrog.Server.Tests/Authentication/RegistrationFlowTests.cs#L60), [CompletedCodeCannotBeReused](../tests/ReFrog.Server.Tests/Authentication/RegistrationFlowTests.cs#L416), [RollingIncorrectLimitSurvivesResends](../tests/ReFrog.Server.Tests/Authentication/RegistrationFlowTests.cs#L286) | Exact expiry is checked. Successful verification immediately before expiry is not asserted. |
| 6a | [TwoConcurrentCompletionsCreateOneAccount](../tests/ReFrog.Server.Tests/Authentication/RegistrationFlowTests.cs#L314) | Checks one success, one conflict, and one account. Does not assert the losing response's sign-in guidance, challenge consumption, or TCU-verification count. |
| 6b | [FailedAffiliationWriteRollsBackAccountAndSession](../tests/ReFrog.Server.Tests/Authentication/RegistrationFlowTests.cs#L341) | Checks unavailable response, no cookie, and zero account/affiliation rows. Does not assert the pending-registration completion state or a successful retry after storage recovers. |

Additional design-test gaps remain. [AccountCannotSignInUntilCodeIsVerified](../tests/ReFrog.Server.Tests/Authentication/RegistrationFlowTests.cs#L456) checks account existence and Identity credentials without calling the HTTP sign-in endpoint. [ChallengeOwnerMustMatchItsPurpose](../tests/ReFrog.Server.Tests/Authentication/RegistrationFlowTests.cs#L618) checks database ownership constraints, while cross-purpose isolation through the API awaits `UC-IDV-verify-affiliation`. The passing client form tests do not render `RegisterPage`. These limits keep Verify at `🟡 Partial`.

For the release comparison, [v0.2.0](https://github.com/evelyntrann/cosc-40943-team-3-refrog/releases/tag/v0.2.0) points to `55a4dc417539ba4c7e05a99b1d1871350793d793` and includes registration. The audited main revision adds `ThrowingEmailProviderReportsUnavailableWithoutLeakingOrSendingTwice` after that tag, while `RegistrationFormTests.cs` is unchanged. The 40-test result above belongs to the audited main revision. The audited main revision also includes volunteer sign-up and recovery, which the existing release notes exclude from the tagged source.

## Working this document with your agent

_[Delegate: running both checks and reporting what fails; finding every `BR-`, `FEAT-`, and `UC-` identifier cited anywhere in `docs/` and listing the ones with no definition; regenerating the second table from the Business Rules fields of your use cases after you have edited them._

_Keep human: whether an area with no feature is a missing feature or a use case that should not exist, and whether an unenforced rule is an oversight or a decision. Both are questions about your client's business, and the answer is not in your repository._

_**The specific failure to watch for:** an agent asked to "fill in the traceability matrix" produces a complete, well-formed table in which every cell is populated and some of the links are invented. A blank cell is information. A confidently wrong link is worse than no link, because it makes the check pass. Verify every row you did not write yourself by opening the document at the other end.]_

## Volunteer role decision traceability

Team decision with Alexa Mercado, October 16, 2026. This table supplements the template examples above.

| Feature | Areas | Use cases |
|---|---|---|
| `FEAT-volunteer-scheduling` | `VOL`, `ADM` | `UC-VOL-request-access`, `UC-ADM-review-volunteer-request` |
| `FEAT-adminstration` | `ADM` | `UC-ADM-review-volunteer-request`, `UC-ADM-grant-volunteer-access`, `UC-ADM-remove-volunteer-access`, `UC-ADM-lift-account-restriction` |
| `FEAT-identity-verification` | `IDV`, `SHP` | `UC-IDV-verify-affiliation` extension 2b, `UC-SHP-log-item-taken` PRE-1 |

| Business rule | Source | Enforced by |
|---|---|---|
| `BR-volunteer-role-approval` | Team decision with Alexa Mercado, 2026-10-16 | `UC-VOL-request-access`, `UC-ADM-review-volunteer-request`, `UC-ADM-grant-volunteer-access`, `UC-ADM-remove-volunteer-access`, `FR-AUTH-volunteer-role` |
| `BR-volunteer-role-approval` claim by email | Team decision, 2026-10-30 (`OI-17`) | `FR-AUTH-account-signup` (sign-in email verified by code at sign-up) |
| `BR-tcu-email-domain` | Client meeting 2026-10-02 (`OI-10`); exact-domain rule, team decision 2026-10-16 | `UC-AUTH-register-account` step 7, `UC-IDV-verify-affiliation` step 3 and extension 3a, `FR-AUTH-account-signup`, `FR-AUTH-verification-code` |
| `BR-shopper-verification` | Client email-verification decision 2026-10-02; team volunteer-role clarification 2026-10-16; Wendy exclusion of visual-ID checks, reported 2026-10-26 (`OI-19`) | `UC-IDV-verify-affiliation` step 2 and extensions 2b/5b, `UC-SHP-log-item-taken` PRE-1, `FR-AUTH-shopping-eligibility` |
