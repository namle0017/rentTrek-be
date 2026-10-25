# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

RenTrek backend: ASP.NET Core 8 Web API for trekking tour booking + outdoor equipment rental. Database is PostgreSQL hosted on **Supabase** (schema already exists — 51 tables, derived from the ERD in the PRD at `../PRD_RenTrekk.md` / `Conceptual.drawio`). Team docs are in Vietnamese and are the source of truth for conventions:

- `docs/01-CONVENTIONS.md` — naming, URL rules, request/response shape, git workflow
- `docs/02-ARCHITECTURE.md` — 3-layer rules, how to add a module
- `docs/03-API-CONTRACT.md` — endpoint contract; update it when adding/changing endpoints
- `docs/superpowers/{specs,plans}/` — design spec + plan for features in progress

## Commands

```bash
dotnet build RenTrek.sln
dotnet run --project src/RenTrek.API          # Swagger at /swagger in Development
dotnet test                                    # all tests (xUnit)
dotnet test tests/RenTrek.BLL.Tests --filter "FullyQualifiedName~TripBookingServiceTests"
dotnet test --filter "FullyQualifiedName~TripBookingServiceTests.MethodName"   # single test
```

No lint step or CI workflow exists yet (`.github/workflows/` is empty).

## Configuration

`Program.cs` calls `DotNetEnv.Env.TraversePath().Load()`, so the repo-root `.env` (gitignored) supplies config via `__` keys; env vars override `appsettings.json`.
- `ConnectionStrings__DefaultConnection` — Npgsql connection string to the Supabase pooler (required; not in `appsettings.json`).
- `Jwt__SecretKey` (≥32 chars), plus `Jwt:Issuer/Audience/...` in `appsettings.json`. The committed `SecretKey` is a public placeholder — every real environment must override it.
- `AutoMapper__LicenseKey`, `Cors:AllowedOrigins`, `SePay__Api__Key`.
- `Seed:Tours` (default `true`): in Development, `TourSeedData` seeds sample tours, equipment, a refund policy and an inactive `system@rentrek.local` account — only when `tours` is empty, in one transaction. Set `false` to skip.

## Architecture

Strict 3 layers, one direction only: `RenTrek.API` → `RenTrek.BLL` → `RenTrek.DAL`. The code came from merging 4 feature branches (auth, tour, product, trip booking), so some pieces still exist twice:

- **API**: thin controllers; always return `ApiResponse<T>` (`Common/ApiResponse.cs`). `ValidationFilter` runs FluentValidation; `Middlewares/ExceptionHandlingMiddleware` maps `AppException` subclasses (`BLL/Exceptions/`) to their HTTP status. JWT setup lives in `Extensions/AuthenticationExtensions.cs` (`OnTokenValidated` rejects revoked `jti`). Web-only: no multi-device session management; refresh-token families exist only for rotation and reuse detection.
- **Current user**: one `API/Services/CurrentUserService` implements two BLL interfaces — `BLL.Services.Interfaces.ICurrentUserService` (auth/users: `Guid? UserId`, `Jti`, `Role`) and `BLL.Abstractions.ICurrentUserService` (trip booking: `Guid UserId` that throws 401). BLL never touches `HttpContext`.
- **Unit of work**: single `DAL/UnitOfWorks/IUnitOfWork` exposing repositories (`Accounts`, `Tours`, `TripBookings`, …), generic `Repository<T>()` and `BeginTransactionAsync/CommitAsync/RollbackAsync`. `CommitAsync` saves changes and requires an open transaction.
- **BLL**: services hold business rules, map with AutoMapper profiles (`Mappings/`), and own transactions. `TimeProvider` is injected in trip booking for testable time.

### Database mapping — differs from the docs

`docs/01-CONVENTIONS.md` says PascalCase tables; the **real Supabase schema is snake_case** with named PKs (`orders.order_id`, `tours.tour_id`). `AppDbContext.OnModelCreating` snake-cases every table and column name after the configurations run, so configurations only declare exceptions: `ToTable(...)` when the name differs, the PK column for `BaseEntity.Id` (`HasColumnName("tour_id")`), and odd columns (`ProductVariant.Name` → `variant_name`). Entity `Account` = table `accounts` (the single account entity; auth and trip booking both use it); ERD `TRANSACTIONS` = table `payments`. Money is `numeric(18,0)` (VND); all keys are `Guid`. Soft delete uses `is_deleted`; `BaseEntity` types get the query filter automatically.

### Schema changes

No EF migrations. Schema changes are hand-written, idempotent SQL in `database/migrations/YYYYMMDD_description.sql`, applied manually to Supabase. As of 2026-10-27 these are **not yet applied**, and the code depends on them: `20261026_add_participant_invitations.sql` (trip-booking invitations) and `20261027_merge_feature_columns.sql` (tour display columns + default roles; register fails without role `Customer`).

### Concurrency patterns

Capacity/idempotency are enforced in `TripBookingRepository` with Postgres primitives: transaction-scoped advisory locks keyed by string (`AcquireIdempotencyLockAsync`, `AcquireBookingLockAsync`) and `SELECT ... FOR UPDATE` via `FromSqlInterpolated`. Call them inside a UnitOfWork transaction. Reuse this approach for other oversell/double-assign rules (PRD BR-07, BR-19). `TripBookingRepository.HoldsSeats` is the single definition of a seat-holding booking; tour catalog `Trip.BookedSlots` (not a DB column) is computed from it.

## Inspecting the Supabase DB

No `psql` on this machine; Python `psycopg2` is installed. Parse `ConnectionStrings__DefaultConnection` from `.env` and connect with `sslmode=require`, setting `conn.set_session(readonly=True)` for exploration. Never print the password.

## Git

Branch from `develop` as `<type>/<RT-id>-<kebab-desc>`; `main`/`develop` are PR-only. Commits follow Conventional Commits with an English imperative subject ≤72 chars, e.g. `feat(booking): add participant invitations`.
