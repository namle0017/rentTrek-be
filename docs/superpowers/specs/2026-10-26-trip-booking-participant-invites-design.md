# Trip Booking and Participant Invitations Design

## Intent

Build the first backend vertical slice for booking a scheduled trekking trip. One authenticated customer books for themselves and may add multiple other participants. The buyer always participates and owns payment and booking management.

## Domain model

- `Order.CustomerUserId` identifies the buyer.
- `TripBookingOrder.OrderId` identifies the booking and connects it to one trip.
- `BookingParticipant.BookingOrderId` groups participants inside the booking.
- The buyer is the first participant and is linked immediately through `LinkedUserId`.
- Additional participants require unique normalized emails within the booking and start with `LinkedUserId = null`.
- `TripGroupId` is an operational group assigned later by Staff. It does not group the commercial order.
- A participant invitation proves permission to claim one unlinked participant. Email alone never proves ownership.

## Booking flow

1. An authenticated customer selects an open trip.
2. The API loads the buyer from the authenticated account and adds them as the first participant.
3. The customer supplies each additional participant's full name and email.
4. The service normalizes emails, rejects duplicates, locks the trip row, and checks confirmed plus unexpired held capacity.
5. The service creates `Order`, `TripBookingOrder`, and all participants in one transaction.
6. The order starts as `PendingPayment` with `Unpaid` payment status and a 15-minute hold.
7. Price comes only from `trips.price_per_person`; client totals are ignored.
8. Retrying the same request with the same `Idempotency-Key` returns the original booking.

## Invitation flow

1. Only the booking owner may generate invitations.
2. Invitations are generated only after the order is `Confirmed`, payment status is `Paid`, and successful payments cover the order total.
3. Regeneration revokes prior active invitations.
4. Each invitation uses a cryptographically random token. Only its SHA-256 hash is stored.
5. The plaintext token is returned once so the buyer can share it through an external channel.
6. The invited person must register or sign in, use the token before expiry, and have the same normalized account email as the participant.
7. Claiming atomically sets `BookingParticipant.LinkedUserId` and marks the invitation redeemed.
8. A token is single-use and cannot overwrite an existing participant link.

## Visibility

- The buyer sees the full booking, all participants, and payment summary.
- A linked participant sees trip details and only their participant record.
- A linked participant never sees prices, payment data, or other participants.
- Unlinked participants cannot access the booking.

## Architecture

- API contains controllers, `ApiResponse<T>`, middleware, and the claims-based current-user adapter.
- BLL contains DTOs, validation, business rules, and service interfaces/implementations.
- DAL contains EF Core entities, configurations, repositories, Unit of Work, and Npgsql registration.
- Controllers never use repositories or `AppDbContext`.
- Services never depend on ASP.NET Core types.

## Database transition

Existing Supabase tables remain unchanged except for:

- Nullable `orders.idempotency_key`, unique per customer when present.
- Unique normalized participant email per active booking.
- New `participant_invitations` table with token hash, expiry, redemption, revocation, and audit timestamps.

The repository contains an idempotent SQL migration. It is not applied automatically to Supabase.

## Non-goals

- Payment provider callback implementation.
- Account registration/login implementation.
- Email delivery.
- Staff group assignment.
- Partial participant cancellation.
- Applying the SQL migration to production.

