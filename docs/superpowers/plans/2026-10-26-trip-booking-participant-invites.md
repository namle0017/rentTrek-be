# Trip Booking and Participant Invitations Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement multi-participant trip booking, paid-order invitation generation, invitation claiming, and scoped booking visibility.

**Architecture:** Follow API → BLL → DAL. Business rules live in `TripBookingService`; repositories own EF Core queries and locking; controllers only translate HTTP and wrap DTOs.

**Tech Stack:** .NET 8, ASP.NET Core Web API, EF Core 8, Npgsql, FluentValidation, xUnit.

**Spec:** `docs/superpowers/specs/2026-10-26-trip-booking-participant-invites-design.md`

## Global Constraints

- Follow `docs/01-CONVENTIONS.md`, `docs/02-ARCHITECTURE.md`, and `docs/03-API-CONTRACT.md`.
- Use current Supabase snake_case table and column names exactly.
- Never expose entities, password hashes, token hashes, or other participants to a linked participant.
- Do not apply schema changes to Supabase automatically.
- All database I/O is asynchronous and saved through `IUnitOfWork`.

## Review Focus

- Two concurrent requests for the last slots: only one transaction may reserve capacity.
- Duplicate emails differing by case or whitespace: reject before database write.
- Replayed create request: return the original booking without consuming more capacity.
- Reused, expired, revoked, wrong-email, or already-linked invitation: reject without partial changes.
- Linked participant detail access: never return payment fields or other participants.

---

### Task 1: DAL model and PostgreSQL transition

**Files:**
- Create: `src/RenTrek.DAL/Entities/*.cs`
- Create: `src/RenTrek.DAL/Data/AppDbContext.cs`
- Create: `src/RenTrek.DAL/Data/Configurations/*.cs`
- Create: `src/RenTrek.DAL/Repositories/**`
- Create: `src/RenTrek.DAL/UnitOfWork/**`
- Create: `src/RenTrek.DAL/DependencyInjection.cs`
- Create: `database/migrations/20261026_add_participant_invitations.sql`
- Test: `tests/RenTrek.BLL.Tests/TripBookingServiceTests.cs`

**Interfaces:**
- Produces: `IUnitOfWork`, `ITripBookingRepository`, mapped booking entities, transactional methods.

- [x] Create a failing test proving the BLL cannot yet construct the booking service against repository interfaces.
- [x] Add minimal entities and Fluent API mappings for accounts, trips, orders, trip bookings, participants, payments, and invitations.
- [x] Add repository queries for row locking, capacity totals, idempotency lookup, payment proof, booking detail, and invitation lookup.
- [x] Add Unit of Work transaction methods and Npgsql DI registration.
- [x] Add the idempotent SQL migration with preflight duplicate-email checks.
- [x] Run `dotnet test` and confirm the foundational test passes.

### Task 2: Booking creation business rules

**Files:**
- Create: `src/RenTrek.BLL/DTOs/TripBooking/*.cs`
- Create: `src/RenTrek.BLL/Validators/TripBooking/*.cs`
- Create: `src/RenTrek.BLL/Services/Interfaces/ITripBookingService.cs`
- Create: `src/RenTrek.BLL/Services/Implementations/TripBookingService.cs`
- Create: `src/RenTrek.BLL/Exceptions/*.cs`
- Create: `src/RenTrek.BLL/Abstractions/ICurrentUserService.cs`
- Modify: `src/RenTrek.BLL/DependencyInjection.cs`
- Test: `tests/RenTrek.BLL.Tests/TripBookingServiceTests.cs`

**Interfaces:**
- Consumes: DAL `IUnitOfWork` and booking repositories.
- Produces: `CreateAsync`, `GetByIdAsync`, `GenerateParticipantInvitationsAsync`, `ClaimParticipantInvitationAsync`.

- [x] Write failing tests for buyer inclusion, server pricing, duplicate email, unavailable capacity, and idempotent replay.
- [x] Implement DTOs, validators, exception types, and current-user abstraction.
- [x] Implement atomic booking creation and owner/participant-scoped detail projection.
- [x] Run focused tests and confirm they pass.

### Task 3: Paid invitation generation and claiming

**Files:**
- Modify: `src/RenTrek.BLL/Services/Implementations/TripBookingService.cs`
- Test: `tests/RenTrek.BLL.Tests/TripBookingServiceTests.cs`

**Interfaces:**
- Consumes: current user, payment proof, invitation repository operations.
- Produces: one-time plaintext claim tokens and participant linkage.

- [x] Write failing tests for unpaid order, non-owner generation, token hashing, expiry, wrong account email, reuse, and successful claim.
- [x] Implement secure token generation, hashing, revocation, expiry, and claim transaction.
- [x] Run focused tests and confirm they pass.

### Task 4: HTTP API and shared response pipeline

**Files:**
- Create: `src/RenTrek.API/Common/ApiResponse.cs`
- Create: `src/RenTrek.API/Middlewares/ExceptionHandlingMiddleware.cs`
- Create: `src/RenTrek.API/Services/CurrentUserService.cs`
- Create: `src/RenTrek.API/Controllers/TripBookingsController.cs`
- Modify: `src/RenTrek.API/Program.cs`
- Modify: `src/RenTrek.API/RenTrek.API.csproj`
- Test: `tests/RenTrek.API.Tests/TripBookingsControllerTests.cs`

**Interfaces:**
- Consumes: `ITripBookingService`.
- Produces: REST endpoints under `/api/v1/trip-bookings` with `ApiResponse<T>`.

- [x] Write failing controller tests for status codes, wrappers, auth boundary, and idempotency header.
- [x] Implement controller, exception middleware, claims adapter, JSON enums, `.env` loading, layer registration, and middleware order.
- [x] Run API tests and confirm they pass.

### Task 5: Contract, build, and database verification

**Files:**
- Modify: `docs/02-ARCHITECTURE.md`
- Modify: `docs/03-API-CONTRACT.md`

**Interfaces:**
- Produces: implementation-aligned API contract and PostgreSQL architecture documentation.

- [x] Document create, detail, generate-invitations, and claim endpoints with request/response/error cases.
- [x] Replace obsolete SQL Server provider references with PostgreSQL/Npgsql.
- [x] Run `dotnet test RenTrek.sln` and require zero failures.
- [x] Run `dotnet build RenTrek.sln --no-restore` and require zero warnings and errors.
- [x] Run `git diff --check` and inspect the final diff for secrets or unintended schema writes.

