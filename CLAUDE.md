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

`Program.cs` calls `DotNetEnv.Env.TraversePath().Load()`, so the repo-root `.env` (gitignored) supplies config via `__` keys:
- `ConnectionStrings__DefaultConnection` — Npgsql connection string to the Supabase pooler (required; DAL throws without it).
- `Jwt__Key` (≥32 bytes), `Jwt__Issuer`, `Jwt__Audience` — required outside Development/Test; in Development a random temporary key is generated with a warning.
- `SePay__Api__Key` — payment provider key.

## Architecture

Strict 3 layers, one direction only: `RenTrek.API` → `RenTrek.BLL` → `RenTrek.DAL`.

- **API**: thin controllers; always return `ApiResponse<T>` (`Common/ApiResponse.cs`), including model-validation errors (custom `InvalidModelStateResponseFactory` in `Program.cs`). Enums serialize as strings. `CurrentUserService` reads the user id from JWT claims and implements BLL's `ICurrentUserService` — BLL never touches `HttpContext`.
- **BLL**: services hold all business rules, map Entity ↔ DTO, and own transactions via `IUnitOfWork`. Errors are thrown as `AppException` subclasses (`Exceptions/`, each carries its HTTP status); `ExceptionHandlingMiddleware` turns them into `ApiResponse.Fail`. Non-`AppException` → generic 500. `TimeProvider` is injected for testable time. Wiring: `BLL/DependencyInjection.cs` → `AddBusinessLayer()`.
- **DAL**: `AppDbContext` + `IEntityTypeConfiguration` classes under `Data/Configurations/` (auto-applied). Repositories + `UnitOfWork` (explicit `BeginTransactionAsync/CommitAsync/RollbackAsync`). Wiring: `DAL/DependencyInjection.cs` → `AddDataAccessLayer()`.

### Database mapping — differs from the docs

`docs/01-CONVENTIONS.md` says PascalCase tables and an `Id` PK with `BaseEntity`; the **real Supabase schema is snake_case** with named PKs (`orders.order_id`, `trips.trip_id`). Entities use PascalCase properties named after the column (`OrderId`) and every column is mapped explicitly with `ToTable(...)` / `HasColumnName(...)` in its Configuration. Follow the existing configurations, not the doc. Other naming gaps vs the PRD: ERD `USERS` = table `accounts`, ERD `TRANSACTIONS` = table `payments`. Money is `numeric(18,0)` (VND) → `HasPrecision(18, 0)`; timestamps are `timestamptz` → `DateTimeOffset`. Soft-delete tables use `is_deleted` + `HasQueryFilter(x => !x.IsDeleted)`.

Only a subset of tables is mapped so far (accounts, trips, orders, trip_booking_orders, booking_participants, payments, participant_invitations). Map others on demand.

### Schema changes

No EF migrations are used (`Data/Migrations/` is empty). Schema changes are hand-written, idempotent SQL in `database/migrations/YYYYMMDD_description.sql`, applied manually to Supabase. As of 2026-10-26, `20261026_add_participant_invitations.sql` is **not yet applied** on Supabase (no `participant_invitations` table, no `orders.idempotency_key`), so the trip-booking invitation flow fails at runtime until it is.

### Concurrency patterns

Capacity/idempotency are enforced in `TripBookingRepository` with Postgres primitives: transaction-scoped advisory locks keyed by string (`AcquireIdempotencyLockAsync`, `AcquireBookingLockAsync`) and `SELECT ... FOR UPDATE` via `FromSqlInterpolated`. Call them inside a UnitOfWork transaction. Reuse this approach for other oversell/double-assign rules (PRD BR-07, BR-19).

## Inspecting the Supabase DB

No `psql` on this machine; Python `psycopg2` is installed. Parse `ConnectionStrings__DefaultConnection` from `.env` and connect with `sslmode=require`, setting `conn.set_session(readonly=True)` for exploration. Never print the password.

## Git

Branch from `develop` as `<type>/<RT-id>-<kebab-desc>`; `main`/`develop` are PR-only. Commits follow Conventional Commits with an English imperative subject ≤72 chars, e.g. `feat(booking): add participant invitations`.
