# Backend CI

CI validates the documented .NET 8 **API → BLL → DAL** architecture. It does not deploy,
start the API, run seed services, scaffold a database, or change any database schema.
The initial findings and the exact verification results are in [REPORT.md](REPORT.md).

## Events and checks

`.github/workflows/ci.yml` runs on pushes to `main`/`develop`, PRs targeting either branch,
merge queues, and manual dispatch. Feature branches are validated through their PRs;
pushes to feature branches without a PR are deliberately outside these configured events.
This avoids running both push and PR workflows for the same feature commit. There are
no path filters, so shared-library, configuration and documentation changes all validate.
Concurrency cancels older runs for the same PR/ref. Draft PRs are checked as well.

Five independent matrix jobs (`Quality`, `Architecture`, `Security`, `Tests`, `Build`)
and a `Docker` job run in parallel. `CI Required` runs even after failures and succeeds
only when every job succeeded. No required check uses `continue-on-error`.
Each .NET job restores once and builds once when required; tests/publish reuse that build.
Security needs only restore. NuGet packages are cached against committed lockfiles.
Full history is fetched for all refs reachable in that checkout. Unfetched/deleted remote
history cannot be scanned; this is not a guarantee that no secret was ever published.

Actions are pinned to verified upstream tag commit SHAs, tokens have only `contents: read`,
checkout does not persist credentials, and fork PRs need no repository secrets.
Jobs have timeouts. Reports are retained for seven days. Gitleaks reports are redacted;
never enable verbose secret output or upload raw configuration/build binary logs.

## Run locally (Windows or Linux x64)

Install the SDK selected by `global.json`, PowerShell 7, Git, and Docker with a running
Linux container engine for the image check. No production credentials are needed.
From the repository root:

```powershell
pwsh -NoProfile -File scripts/ci/Install-Tools.ps1
pwsh -NoProfile -File scripts/ci/Validate.ps1

# Run an individual CI job using the same commands as GitHub:
pwsh -NoProfile -File scripts/ci/Validate.ps1 -Phase Quality
pwsh -NoProfile -File scripts/ci/Validate.ps1 -Phase Architecture
pwsh -NoProfile -File scripts/ci/Validate.ps1 -Phase Security
pwsh -NoProfile -File scripts/ci/Validate.ps1 -Phase Tests
pwsh -NoProfile -File scripts/ci/Validate.ps1 -Phase Build
pwsh -NoProfile -File scripts/ci/Validate.ps1 -Phase Docker

# Both existing solution configurations are supported:
pwsh -NoProfile -File scripts/ci/Validate.ps1 -Phase Build -Configuration Debug
```

`Validate.ps1` aggregates independent failures, writes `artifacts/validation-<phase>.json`,
and exits nonzero on failure. A failed restore/build marks dependent checks **NOT RUN**;
it never runs stale test binaries. Test projects are discovered in `src/` and `tests/`
using `IsTestProject`/`Microsoft.NET.Test.Sdk`; each must be included in the solution.
Zero executed tests or missing coverage is an error. TRX and Cobertura/OpenCover reports
are placed under `artifacts/`. No arbitrary coverage percentage is enforced.

## Rules mapped to the existing docs

| Requirement | Enforcement |
|---|---|
| `01-CONVENTIONS` §1, §6.3: PascalCase types/members/constants; `I` interfaces; `_camelCase` private fields including readonly; camelCase parameters/locals; `T` generics | `.editorconfig`, Roslyn naming diagnostics, `dotnet format` |
| §6.3: file-scoped namespace matches directories; one primary type per file with matching name | Roslyn syntax tests over production `src/` |
| `02-ARCHITECTURE` §6: Task/ValueTask methods end in `Async`; no async void | Assembly reflection, including private methods |
| API references DAL only for registration in global Program | Project graph, NetArchTest IL checks, complete Cecil type-reference metadata (includes async/lambda generated code); only `DAL.DependencyInjection` is allowed in API metadata |
| BLL cannot depend on API/ASP.NET Core/DbContext/concrete repositories/UoW | NetArchTest IL dependency checks |
| DAL cannot depend on BLL/API/ASP.NET Core | NetArchTest IL dependency checks |
| §6.8: services inject interfaces and use IUnitOfWork instead of direct repositories | Constructor parameter reflection |
| Entities must not cross BLL service contracts or HTTP DTO properties | Recursive generic/array parameter/return/property inspection; DAL enums are allowed |
| Controllers do not catch exceptions; repositories do not save changes | Roslyn invocation/catch syntax checks |
| DTO roles and suffixes | Existing `DtoNamingConventionTests`, run explicitly in Architecture as well as existing unit suite |
| Controller/repository/validator/AutoMapper profile placement | Assembly/type namespace checks |
| PostgreSQL Fluent model compatibility; snake_case tables/columns | EF builds and validates the complete relational model offline |
| EF package/provider compatibility | Lockfile check requires EF Core/Design and Npgsql EF provider major/minor 8.0 |
| Formatting, unused usings/assignments, nullable and resource/async analysis | .NET 8 recommended analyzers; full formatting check; dedicated IDE0005 check |
| No newly introduced compiler/analyzer warnings (§7 checklist) | Per-project SARIF reports compared to reviewed warning baseline, matching project/file/rule/message/count |
| Secrets, unsafe defaults, vulnerable dependencies | Gitleaks built-in and custom rules, tracked-config checks, NuGet Audit of direct and transitive packages |

Some requirements need human review: whether a controller contains business rules,
all response paths use ApiResponse, semantic correctness/authorization, enum singular
English nouns, ambiguous Boolean prefixes (`Success` and `OnlyAvailable` are documented
wire names), good abbreviations, N+1 behavior, I/O correctness and UTC timestamps.
The pipeline does not claim complete API/business-contract compliance. It does not
require unimplemented endpoints from the proposed `03.1` contract.

## Warning policy

`Directory.Build.props` fixes analysis at .NET 8 recommended rules. Nullable remains
enabled by each existing project. Existing warnings stay visible; no blanket `NoWarn`
or `TreatWarningsAsErrors` is introduced. `CS4014`, `CA2012`, `CA2200` always fail builds.
The Quality job also rejects any new warning signature or count increase in each source
file using `scripts/ci/warnings-baseline.json`. Line movement does not affect the key.
Fixing warnings is allowed; remove obsolete baseline entries in the same reviewed PR
so resolved warnings cannot recur. Baseline edits require deliberate review; never copy
`warnings-current.json` over the baseline merely to pass CI. Initial baseline generation
refuses to overwrite an existing baseline and is not used in CI.

Format/style violations still fail even when the same diagnostics appear in the warning
baseline. The baseline applies only to the incremental warning gate, not to architecture,
formatting, tests or security. IDE0005 uses a dedicated `dotnet format style` check at
info severity because build-time IDE0005 requires generating XML docs; this keeps the
check enforceable without adding/suppressing missing-XML-comment warnings.

## Security policy

NuGet Audit checks **all** dependency levels; low/moderate findings are reported and
High/Critical (`NU1903`/`NU1904`) fail Security. Advisory service failures (`NU1900`/
`NU1905`) also fail. JSON from `dotnet list package --vulnerable --include-transitive`
is retained even if restore finds an advisory; that listing's exit code alone is not
used to determine whether dependencies are safe.

Gitleaks 8.30.1 and actionlint 1.7.7 binaries are downloaded only from upstream releases
and checked against committed SHA-256 values on Windows/Linux. Gitleaks scans full
reachable Git history (`--all`) and the working directory, including local ignored
`.env`/appsettings. Thus local results can contain findings absent from a clean runner.
Built-in rules cover provider keys, tokens and private keys. Custom rules additionally
cover low-entropy JWT/password/connection-string and environment assignments.
Explicit angle-bracket/TODO examples are distinguished by value; no complete config,
documentation or test directory is exempted. Historical realistic examples/test keys may
still require review. A placeholder shaped like a usable default signing key remains
dangerous and is rejected in non-example tracked appsettings. No secret baseline or
finding suppression is used. Inline `gitleaks:allow` comments cannot bypass the gate.

`Test-SecretRules.ps1` checks placeholder acceptance, JSON/env credential detection and
report redaction with a generated synthetic value. It never prints the value.
Configuration checks reject populated credential defaults in tracked JSON and detect
`EnableSensitiveDataLogging`/`DangerousAcceptAnyServerCertificateValidator` in source.
These conservative text checks also flag comments and do not replace a security review.

The tracked JWT fallback has been cleared. Running the API now requires
`Jwt__SecretKey` or `dotnet user-secrets` as documented in `01-CONVENTIONS` §6.1.
Do not put a real value into committed config. Build, tests and publish do not start
the host and require no JWT key, database password or AutoMapper license key.

If a historical finding is real, rotate/revoke it first, then coordinate history cleanup
with repository administrators. Removing a value from HEAD alone does not erase history.
Do not disable the scanner or mass-allowlist paths to turn the check green.

## Dependencies and Docker

Every project has a committed `packages.lock.json`. CI/local/Docker restore in locked
mode, so dependency graphs cannot change silently. For an intentional package change:

```powershell
dotnet restore RenTrek.sln --force-evaluate
pwsh -NoProfile -File scripts/ci/Validate.ps1
```

Review/commit the corresponding project and lockfile changes together. The SDK remains
.NET 8 (installed local patch 8.0.424); `rollForward: latestPatch` permits only patch
updates in the same feature band. Review SDK/analysis changes as tooling maintenance.
The Docker build copies SDK/analyzer settings and lockfiles before cached restore.
The SDK/runtime base images remain on the existing .NET 8 image tags and receive servicing
updates. Docker validation only builds the image; it does not run the production host.
Compose uses the developer's `.env` and is not started by PR validation.

## Database and integration testing limits

The current project is PostgreSQL/Supabase DB-first, not SQL Server. No SQL Server
container or migration application is added. `Data/Migrations` has no EF migration source.
Offline EF validation checks the actual mapped model; it cannot prove that a deployed
database matches that model or that SQL snapshots apply cleanly.

There is currently no live-database integration test project. API/controller tests and
fake-repository service tests run as unit tests; they are not reported as database
integration PASS. New projects named `Integration` fail discovery until explicitly
connected to a disposable PostgreSQL fixture/service with isolated configuration. Do not
reuse local `.env`, Supabase or production credentials for such a suite. Schema snapshots
in docs are reference material, not scripts to execute automatically in PRs.

## Enable branch protection (administrator action)

This change has **not** changed remote repository settings or published the workflow.
After reviewing and pushing these files yourself, run the workflow and resolve the
reported failures, then configure GitHub Settings → Rules → Rulesets (or Branches):

1. Target `main` and `develop`; require a PR and at least one approval from another member.
2. Require the check named **`CI Required`**, source GitHub Actions, and require the branch
   to be up to date (or use a merge queue; `merge_group` is supported).
3. Block force pushes and deletion; restrict bypass/direct pushes, including administrators
   as appropriate. Enable squash merge to match `01-CONVENTIONS` §5.
4. For `main`, manually enforce allowed source branches (`develop` or `hotfix/*`) in
   review/repository policy; the current workflow does not validate PR branch naming,
   Conventional Commit grammar or allowed merge sources.
5. Exercise a PR with a deliberately failing change and verify merging is blocked.
   A cancelled/skipped prerequisite must not produce a successful `CI Required` check.

GitHub requires a workflow run before a new check may appear in the selector. Local
actionlint checks YAML/actions expressions, not actual GitHub runtime behavior. The first
hosted run (including Ubuntu Docker) remains to be verified after your push.

## Upstream references

- [dotnet format](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-format)
- [NuGet Audit](https://learn.microsoft.com/en-us/nuget/concepts/auditing-packages)
- [Gitleaks v8.30.1 CLI/configuration](https://github.com/gitleaks/gitleaks/tree/v8.30.1)
- [NetArchTest](https://github.com/BenMorris/NetArchTest)
- [actionlint](https://github.com/rhysd/actionlint)
