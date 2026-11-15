-- =====================================================================
-- RenTrek - PostgreSQL schema for Supabase
-- Generated from "Logical ERD" (Conceptual.drawio)
-- Type mapping: Guid -> uuid, string -> varchar, text -> text,
--               datetime -> timestamptz, bit -> boolean,
--               dec(p,s) -> numeric(p,s), time(0) -> time(0)
-- Columns marked "?" in the ERD are NULL, the rest are NOT NULL.
-- =====================================================================

BEGIN;

CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- Auto-update updated_at
CREATE OR REPLACE FUNCTION public.set_updated_at()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    NEW.updated_at = now();
    RETURN NEW;
END;
$$;

-- =====================================================================
-- IDENTITY & AUTH
-- =====================================================================

CREATE TABLE roles (
    role_id         uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    role_name       varchar(50)  NOT NULL UNIQUE,
    description     text,
    created_at      timestamptz  NOT NULL DEFAULT now(),
    updated_at      timestamptz  NOT NULL DEFAULT now(),
    is_deleted      boolean      NOT NULL DEFAULT false
);

CREATE TABLE accounts (
    user_id             uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    role_id             uuid         NOT NULL REFERENCES roles(role_id),
    email               varchar(255) NOT NULL UNIQUE,
    password_hash       varchar(255),
    full_name           varchar(255) NOT NULL,
    phone               varchar(20),
    status              varchar(50)  NOT NULL,
    created_at          timestamptz  NOT NULL DEFAULT now(),
    updated_at          timestamptz  NOT NULL DEFAULT now(),
    email_confirmed_at  timestamptz,
    last_login_at       timestamptz,
    is_deleted          boolean      NOT NULL DEFAULT false
);

CREATE TABLE profiles (
    profile_id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id             uuid         NOT NULL UNIQUE REFERENCES accounts(user_id),
    avatar_url          text,
    address             varchar(500),
    date_of_birth       date,
    bio                 text,
    career_start_date   date,
    user_code           varchar(50)  NOT NULL UNIQUE,
    created_at          timestamptz  NOT NULL DEFAULT now(),
    updated_at          timestamptz  NOT NULL DEFAULT now(),
    is_deleted          boolean      NOT NULL DEFAULT false
);

CREATE TABLE user_external_logins (
    user_external_login_id  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id                 uuid         NOT NULL REFERENCES accounts(user_id),
    provider                varchar(50)  NOT NULL,
    provider_subject        varchar(255) NOT NULL,
    created_at              timestamptz  NOT NULL DEFAULT now(),
    UNIQUE (provider, provider_subject)
);

CREATE TABLE password_reset_tokens (
    password_reset_token_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id                 uuid         NOT NULL REFERENCES accounts(user_id),
    token_hash              varchar(255) NOT NULL UNIQUE,
    expires_at              timestamptz  NOT NULL,
    used_at                 timestamptz,
    created_at              timestamptz  NOT NULL DEFAULT now()
);

CREATE TABLE refresh_tokens (
    refresh_token_id                uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id                         uuid         NOT NULL REFERENCES accounts(user_id),
    family_id                       uuid         NOT NULL,
    token_hash                      varchar(255) NOT NULL UNIQUE,
    expires_at                      timestamptz  NOT NULL,
    created_at                      timestamptz  NOT NULL DEFAULT now(),
    revoked_at                      timestamptz,
    revoked_reason                  varchar(255),
    replaced_by_refresh_token_id    uuid UNIQUE REFERENCES refresh_tokens(refresh_token_id)
);

CREATE TABLE access_tokens (
    access_token_id     uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id             uuid         NOT NULL REFERENCES accounts(user_id),
    refresh_token_id    uuid         NOT NULL REFERENCES refresh_tokens(refresh_token_id),
    jti                 varchar(255) NOT NULL UNIQUE,
    expires_at          timestamptz  NOT NULL,
    revoked_at          timestamptz,
    revoked_reason      varchar(255),
    created_at          timestamptz  NOT NULL DEFAULT now()
);

-- =====================================================================
-- CONTENT (POSTS)
-- =====================================================================

CREATE TABLE posts (
    post_id             uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    created_by_user_id  uuid         NOT NULL REFERENCES accounts(user_id),
    title               varchar(255) NOT NULL,
    slug                varchar(255) NOT NULL UNIQUE,
    post_type           varchar(50)  NOT NULL,
    content             text         NOT NULL,
    status              varchar(50)  NOT NULL,
    published_at        timestamptz,
    created_at          timestamptz  NOT NULL DEFAULT now(),
    updated_at          timestamptz  NOT NULL DEFAULT now(),
    is_deleted          boolean      NOT NULL DEFAULT false
);

CREATE TABLE tags (
    tag_id      uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    name        varchar(100) NOT NULL UNIQUE,
    slug        varchar(100) NOT NULL UNIQUE,
    created_at  timestamptz  NOT NULL DEFAULT now(),
    updated_at  timestamptz  NOT NULL DEFAULT now(),
    is_deleted  boolean      NOT NULL DEFAULT false
);

CREATE TABLE post_tags (
    post_tag_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    post_id     uuid        NOT NULL REFERENCES posts(post_id),
    tag_id      uuid        NOT NULL REFERENCES tags(tag_id),
    created_at  timestamptz NOT NULL DEFAULT now(),
    UNIQUE (post_id, tag_id)
);

CREATE TABLE soft_skills (
    soft_skill_id   uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    name            varchar(100) NOT NULL UNIQUE,
    description     text,
    status          varchar(50)  NOT NULL,
    created_at      timestamptz  NOT NULL DEFAULT now(),
    updated_at      timestamptz  NOT NULL DEFAULT now(),
    is_deleted      boolean      NOT NULL DEFAULT false
);

CREATE TABLE post_soft_skills (
    post_soft_skill_id  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    post_id             uuid        NOT NULL REFERENCES posts(post_id),
    soft_skill_id       uuid        NOT NULL REFERENCES soft_skills(soft_skill_id),
    created_at          timestamptz NOT NULL DEFAULT now(),
    UNIQUE (post_id, soft_skill_id)
);

-- =====================================================================
-- CATALOG: CATEGORIES, LOCATIONS, REFUND POLICIES
-- =====================================================================

CREATE TABLE categories (
    category_id     uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    name            varchar(255) NOT NULL,
    description     text,
    status          varchar(50)  NOT NULL,
    category_type   varchar(50)  NOT NULL,
    created_at      timestamptz  NOT NULL DEFAULT now(),
    updated_at      timestamptz  NOT NULL DEFAULT now(),
    is_deleted      boolean      NOT NULL DEFAULT false
);

CREATE TABLE locations (
    location_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    name        varchar(255)  NOT NULL,
    address     varchar(500)  NOT NULL,
    latitude    numeric(9,6)  NOT NULL,
    longitude   numeric(9,6)  NOT NULL,
    created_at  timestamptz   NOT NULL DEFAULT now(),
    updated_at  timestamptz   NOT NULL DEFAULT now(),
    is_deleted  boolean       NOT NULL DEFAULT false
);

CREATE TABLE refund_policies (
    refund_policy_id    uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    created_by_user_id  uuid         NOT NULL REFERENCES accounts(user_id),
    policy_type         varchar(50)  NOT NULL,
    name                varchar(255) NOT NULL,
    description         text,
    status              varchar(50)  NOT NULL,
    policy_family_id    uuid         NOT NULL,
    version             integer      NOT NULL DEFAULT 1,
    effective_from      timestamptz  NOT NULL,
    effective_until     timestamptz,
    created_at          timestamptz  NOT NULL DEFAULT now(),
    updated_at          timestamptz  NOT NULL DEFAULT now(),
    is_deleted          boolean      NOT NULL DEFAULT false,
    UNIQUE (policy_family_id, version),
    CONSTRAINT ck_refund_policies_policy_type CHECK (policy_type IN ('Tour', 'Rental')),
    CONSTRAINT ck_refund_policies_status CHECK (status IN ('Draft', 'Active', 'Archived'))
);

CREATE TABLE refund_policy_rules (
    refund_policy_rule_id   uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    refund_policy_id        uuid         NOT NULL REFERENCES refund_policies(refund_policy_id),
    min_hours_before        integer      NOT NULL,
    refund_percentage       numeric(5,2) NOT NULL CHECK (refund_percentage BETWEEN 0 AND 100),
    created_at              timestamptz  NOT NULL DEFAULT now(),
    updated_at              timestamptz  NOT NULL DEFAULT now(),
    is_deleted              boolean      NOT NULL DEFAULT false
);

-- =====================================================================
-- PRODUCTS & EQUIPMENT
-- =====================================================================

CREATE TABLE products (
    product_id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    category_id         uuid         NOT NULL REFERENCES categories(category_id),
    name                varchar(255) NOT NULL,
    description         text,
    status              varchar(50)  NOT NULL,
    created_by_user_id  uuid         NOT NULL REFERENCES accounts(user_id),
    created_at          timestamptz  NOT NULL DEFAULT now(),
    updated_at          timestamptz  NOT NULL DEFAULT now(),
    is_deleted          boolean      NOT NULL DEFAULT false
);

CREATE TABLE product_variants (
    product_variant_id  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    product_id          uuid         NOT NULL REFERENCES products(product_id),
    sku                 varchar(100) NOT NULL UNIQUE,
    variant_name        varchar(255) NOT NULL,
    status              varchar(50)  NOT NULL,
    size                varchar(50),
    color               varchar(50),
    created_at          timestamptz  NOT NULL DEFAULT now(),
    updated_at          timestamptz  NOT NULL DEFAULT now(),
    is_deleted          boolean      NOT NULL DEFAULT false
);

CREATE TABLE product_variant_images (
    image_id            uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    product_variant_id  uuid        NOT NULL REFERENCES product_variants(product_variant_id),
    image_url           text        NOT NULL,
    display_order       integer     NOT NULL DEFAULT 0,
    is_primary          boolean     NOT NULL DEFAULT false,
    created_at          timestamptz NOT NULL DEFAULT now(),
    updated_at          timestamptz NOT NULL DEFAULT now(),
    is_deleted          boolean     NOT NULL DEFAULT false
);

CREATE TABLE equipment_assets (
    equipment_asset_id  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    product_variant_id  uuid         NOT NULL REFERENCES product_variants(product_variant_id),
    asset_code          varchar(100) NOT NULL UNIQUE,
    asset_status        varchar(50)  NOT NULL,
    condition_note      text,
    created_at          timestamptz  NOT NULL DEFAULT now(),
    updated_at          timestamptz  NOT NULL DEFAULT now(),
    is_deleted          boolean      NOT NULL DEFAULT false
);

CREATE TABLE product_bundles (
    bundle_id   uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    category_id uuid         NOT NULL REFERENCES categories(category_id),
    name        varchar(255) NOT NULL,
    description text,
    status      varchar(50)  NOT NULL,
    created_at  timestamptz  NOT NULL DEFAULT now(),
    updated_at  timestamptz  NOT NULL DEFAULT now(),
    is_deleted  boolean      NOT NULL DEFAULT false
);

CREATE TABLE product_bundle_items (
    bundle_item_id      uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    bundle_id           uuid        NOT NULL REFERENCES product_bundles(bundle_id),
    product_variant_id  uuid        NOT NULL REFERENCES product_variants(product_variant_id),
    quantity            integer     NOT NULL CHECK (quantity > 0),
    created_at          timestamptz NOT NULL DEFAULT now(),
    updated_at          timestamptz NOT NULL DEFAULT now(),
    is_deleted          boolean     NOT NULL DEFAULT false
);

CREATE TABLE rental_price_tiers (
    price_tier_id       uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    product_variant_id  uuid REFERENCES product_variants(product_variant_id),
    bundle_id           uuid REFERENCES product_bundles(bundle_id),
    min_days            integer       NOT NULL,
    max_days            integer,
    daily_price         numeric(18,0) NOT NULL,
    created_at          timestamptz   NOT NULL DEFAULT now(),
    updated_at          timestamptz   NOT NULL DEFAULT now(),
    is_deleted          boolean       NOT NULL DEFAULT false,
    -- a tier belongs to exactly one of: variant or bundle
    CHECK ((product_variant_id IS NULL) <> (bundle_id IS NULL))
);

-- =====================================================================
-- TOURS
-- =====================================================================

CREATE TABLE tours (
    tour_id             uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    category_id         uuid          NOT NULL REFERENCES categories(category_id),
    main_location_id    uuid          NOT NULL REFERENCES locations(location_id),
    created_by_user_id  uuid          NOT NULL REFERENCES accounts(user_id),
    name                varchar(255)  NOT NULL,
    description         text,
    duration_days       integer       NOT NULL,
    status              varchar(50)   NOT NULL,
    created_at          timestamptz   NOT NULL DEFAULT now(),
    base_price          numeric(18,0) NOT NULL,
    duration_minutes    integer       NOT NULL,
    duration_nights     integer       NOT NULL,
    updated_at          timestamptz   NOT NULL DEFAULT now(),
    is_deleted          boolean       NOT NULL DEFAULT false,
    tour_family_id      uuid          NOT NULL,
    version             integer       NOT NULL DEFAULT 1,
    itinerary_locked_at timestamptz,
    UNIQUE (tour_family_id, version)
);

CREATE TABLE tour_images (
    tour_image_id   uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tour_id         uuid        NOT NULL REFERENCES tours(tour_id),
    image_url       text        NOT NULL,
    display_order   integer     NOT NULL DEFAULT 0,
    is_primary      boolean     NOT NULL DEFAULT false,
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now(),
    is_deleted      boolean     NOT NULL DEFAULT false
);

CREATE TABLE tour_activities (
    tour_activity_id    uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tour_id             uuid         NOT NULL REFERENCES tours(tour_id),
    name                varchar(255) NOT NULL,
    description         text,
    day_number          integer      NOT NULL,
    start_time          time(0)      NOT NULL,
    duration_minutes    integer      NOT NULL,
    display_order       integer      NOT NULL DEFAULT 0,
    created_at          timestamptz  NOT NULL DEFAULT now(),
    updated_at          timestamptz  NOT NULL DEFAULT now(),
    is_deleted          boolean      NOT NULL DEFAULT false
);

CREATE TABLE tour_preparation_items (
    preparation_item_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tour_id             uuid         NOT NULL REFERENCES tours(tour_id),
    item_name           varchar(255) NOT NULL,
    note                text,
    created_at          timestamptz  NOT NULL DEFAULT now(),
    updated_at          timestamptz  NOT NULL DEFAULT now(),
    is_deleted          boolean      NOT NULL DEFAULT false
);

CREATE TABLE tour_provided_items (
    tour_provided_item_id   uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tour_id                 uuid        NOT NULL REFERENCES tours(tour_id),
    product_variant_id      uuid        NOT NULL REFERENCES product_variants(product_variant_id),
    note                    text,
    quantity_basis          varchar(50) NOT NULL,
    quantity                integer     NOT NULL CHECK (quantity > 0),
    created_at              timestamptz NOT NULL DEFAULT now(),
    updated_at              timestamptz NOT NULL DEFAULT now(),
    is_deleted              boolean     NOT NULL DEFAULT false
);

CREATE TABLE tour_posts (
    tour_post_id    uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tour_id         uuid        NOT NULL REFERENCES tours(tour_id),
    post_id         uuid        NOT NULL REFERENCES posts(post_id),
    display_order   integer     NOT NULL DEFAULT 0,
    created_at      timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tour_id, post_id)
);

CREATE TABLE tour_recurrence_rules (
    recurrence_rule_id  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tour_id             uuid          NOT NULL REFERENCES tours(tour_id),
    frequency           varchar(50)   NOT NULL,
    interval_value      integer       NOT NULL DEFAULT 1,
    start_date          date          NOT NULL,
    end_date            date          NOT NULL,
    is_active           boolean       NOT NULL DEFAULT true,
    start_time          time(0)       NOT NULL,
    days_of_week        varchar(50),
    default_total_slots integer       NOT NULL,
    default_price       numeric(18,0) NOT NULL,
    created_at          timestamptz   NOT NULL DEFAULT now(),
    updated_at          timestamptz   NOT NULL DEFAULT now(),
    is_deleted          boolean       NOT NULL DEFAULT false
);

-- =====================================================================
-- TRIPS
-- =====================================================================

CREATE TABLE trips (
    trip_id             uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tour_id             uuid          NOT NULL REFERENCES tours(tour_id),
    recurrence_rule_id  uuid REFERENCES tour_recurrence_rules(recurrence_rule_id),
    refund_policy_id    uuid          NOT NULL REFERENCES refund_policies(refund_policy_id),
    managed_by_user_id  uuid          NOT NULL REFERENCES accounts(user_id),
    start_at            timestamptz   NOT NULL,
    end_at              timestamptz   NOT NULL,
    total_slots         integer       NOT NULL CHECK (total_slots >= 0),
    status              varchar(50)   NOT NULL,
    price_per_person    numeric(18,0) NOT NULL,
    min_participants    integer       NOT NULL,
    row_version         integer       NOT NULL DEFAULT 1,  -- optimistic concurrency
    created_at          timestamptz   NOT NULL DEFAULT now(),
    updated_at          timestamptz   NOT NULL DEFAULT now(),
    is_deleted          boolean       NOT NULL DEFAULT false,
    CHECK (end_at > start_at)
);

CREATE TABLE trip_groups (
    trip_group_id       uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    trip_id             uuid         NOT NULL REFERENCES trips(trip_id),
    guide_user_id       uuid         NOT NULL REFERENCES accounts(user_id),
    group_name          varchar(255) NOT NULL,
    max_participants    integer      NOT NULL,
    status              varchar(50)  NOT NULL,
    created_at          timestamptz  NOT NULL DEFAULT now(),
    updated_at          timestamptz  NOT NULL DEFAULT now(),
    is_deleted          boolean      NOT NULL DEFAULT false
);

CREATE TABLE trip_provided_items (
    trip_provided_item_id   uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    trip_id                 uuid        NOT NULL REFERENCES trips(trip_id),
    tour_provided_item_id   uuid        NOT NULL REFERENCES tour_provided_items(tour_provided_item_id),
    prepared_by_user_id     uuid REFERENCES accounts(user_id),
    required_quantity       integer     NOT NULL,
    status                  varchar(50) NOT NULL,
    product_variant_id      uuid        NOT NULL REFERENCES product_variants(product_variant_id),
    created_at              timestamptz NOT NULL DEFAULT now()
);

-- =====================================================================
-- ORDERS (supertype) + TRIP_BOOKING_ORDERS / RENTAL_ORDERS (subtypes)
-- =====================================================================

CREATE TABLE orders (
    order_id            uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    customer_user_id    uuid          NOT NULL REFERENCES accounts(user_id),
    order_type          varchar(50)   NOT NULL,
    total_amount        numeric(18,0) NOT NULL,
    status              varchar(50)   NOT NULL,
    created_at          timestamptz   NOT NULL DEFAULT now(),
    updated_at          timestamptz   NOT NULL DEFAULT now(),
    order_code          varchar(50)   NOT NULL UNIQUE,
    note                text,
    subtotal_amount     numeric(18,0) NOT NULL,
    discount_amount     numeric(18,0) NOT NULL DEFAULT 0,
    payment_status      varchar(50)   NOT NULL,
    hold_expires_at     timestamptz,
    reconciliation_deadline_at timestamptz,
    idempotency_key      varchar(100),
    cancelled_at        timestamptz,
    cancel_reason       varchar(500),
    refund_policy_id    uuid          NOT NULL REFERENCES refund_policies(refund_policy_id),
    is_deleted          boolean       NOT NULL DEFAULT false
);

CREATE TABLE trip_booking_orders (
    order_id                    uuid PRIMARY KEY REFERENCES orders(order_id),
    trip_id                     uuid          NOT NULL REFERENCES trips(trip_id),
    participant_count           integer       NOT NULL CHECK (participant_count > 0),
    unit_price                  numeric(18,0) NOT NULL,
    grouping_preference         varchar(50)   NOT NULL,
    is_split                    boolean       NOT NULL DEFAULT false,
    split_reason                text,
    split_approved_by_user_id   uuid REFERENCES accounts(user_id),
    split_at                    timestamptz
);

CREATE TABLE rental_orders (
    order_id            uuid PRIMARY KEY REFERENCES orders(order_id),
    handled_by_user_id  uuid REFERENCES accounts(user_id),
    rental_start_at     timestamptz   NOT NULL,
    rental_end_at       timestamptz   NOT NULL,
    deposit_amount      numeric(18,0) NOT NULL DEFAULT 0,
    CHECK (rental_end_at > rental_start_at)
);

-- =====================================================================
-- PARTICIPANTS, ATTENDANCE, REVIEWS
-- =====================================================================

CREATE TABLE booking_participants (
    participant_id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    booking_order_id        uuid         NOT NULL REFERENCES trip_booking_orders(order_id),
    linked_user_id          uuid REFERENCES accounts(user_id),
    trip_group_id           uuid REFERENCES trip_groups(trip_group_id),
    full_name               varchar(255) NOT NULL,
    email                   varchar(255),
    phone                   varchar(20),
    status                  varchar(50)  NOT NULL,
    trip_id                 uuid         NOT NULL REFERENCES trips(trip_id),
    date_of_birth           date,
    emergency_contact_name  varchar(255),
    emergency_contact_phone varchar(20),
    created_at              timestamptz  NOT NULL DEFAULT now(),
    updated_at              timestamptz  NOT NULL DEFAULT now(),
    is_deleted              boolean      NOT NULL DEFAULT false
);

CREATE TABLE participant_post_reads (
    participant_post_read_id    uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    participant_id              uuid        NOT NULL REFERENCES booking_participants(participant_id),
    post_id                     uuid        NOT NULL REFERENCES posts(post_id),
    read_at                     timestamptz NOT NULL,
    created_at                  timestamptz NOT NULL DEFAULT now(),
    UNIQUE (participant_id, post_id)
);

CREATE TABLE activity_attendances (
    attendance_id       uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tour_activity_id    uuid        NOT NULL REFERENCES tour_activities(tour_activity_id),
    participant_id      uuid        NOT NULL REFERENCES booking_participants(participant_id),
    attendance_status   varchar(50) NOT NULL,
    checked_at          timestamptz NOT NULL,
    checked_by_user_id  uuid        NOT NULL REFERENCES accounts(user_id),
    created_at          timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tour_activity_id, participant_id)
);

CREATE TABLE reviews (
    review_id       uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    is_hidden boolean NOT NULL DEFAULT false,
    hidden_reason text,
    rating          integer     NOT NULL CHECK (rating BETWEEN 1 AND 5),
    comment         text,
    created_at      timestamptz NOT NULL DEFAULT now(),
    participant_id  uuid        NOT NULL UNIQUE REFERENCES booking_participants(participant_id),
    updated_at      timestamptz NOT NULL DEFAULT now(),
    is_deleted      boolean     NOT NULL DEFAULT false
);

-- =====================================================================
-- PROMOTIONS
-- =====================================================================

CREATE TABLE promotions (
    promotion_id        uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    created_by_user_id  uuid         NOT NULL REFERENCES accounts(user_id),
    name                varchar(255) NOT NULL,
    description         text,
    start_at            timestamptz  NOT NULL,
    end_at              timestamptz  NOT NULL,
    status              varchar(50)  NOT NULL,
    applies_to          varchar(50)  NOT NULL,
    application_scope   varchar(50)  NOT NULL,
    promotion_code      varchar(50) UNIQUE,
    max_uses            integer,
    max_uses_per_user   integer,
    is_stackable        boolean      NOT NULL DEFAULT false,
    priority            integer      NOT NULL DEFAULT 0,
    created_at          timestamptz  NOT NULL DEFAULT now(),
    updated_at          timestamptz  NOT NULL DEFAULT now(),
    is_deleted          boolean      NOT NULL DEFAULT false,
    CHECK (end_at > start_at)
);

CREATE TABLE promotion_tiers (
    promotion_tier_id       uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    promotion_id            uuid          NOT NULL REFERENCES promotions(promotion_id),
    gift_product_variant_id uuid REFERENCES product_variants(product_variant_id),
    min_threshold           numeric(18,0) NOT NULL,
    discount_type           varchar(50)   NOT NULL,
    discount_value          numeric(18,2) NOT NULL,
    gift_quantity           integer,
    qualification_metric    varchar(50)   NOT NULL,
    created_at              timestamptz   NOT NULL DEFAULT now(),
    updated_at              timestamptz   NOT NULL DEFAULT now(),
    is_deleted              boolean       NOT NULL DEFAULT false
);

-- =====================================================================
-- RENTAL ITEMS, PROMOTIONS APPLIED, ASSET ASSIGNMENTS, CHARGES
-- =====================================================================

CREATE TABLE rental_order_items (
    rental_order_item_id    uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    rental_order_id         uuid          NOT NULL REFERENCES rental_orders(order_id),
    product_variant_id      uuid REFERENCES product_variants(product_variant_id),
    quantity                integer       NOT NULL CHECK (quantity > 0),
    daily_price             numeric(18,0) NOT NULL,
    line_amount             numeric(18,0) NOT NULL,
    bundle_id               uuid REFERENCES product_bundles(bundle_id),
    billable_days           integer       NOT NULL,
    created_at              timestamptz   NOT NULL DEFAULT now(),
    updated_at              timestamptz   NOT NULL DEFAULT now(),
    is_deleted              boolean       NOT NULL DEFAULT false,
    -- an item is either a single variant or a bundle
    CHECK ((product_variant_id IS NULL) <> (bundle_id IS NULL))
);

CREATE TABLE rental_bundle_item_snapshots (
    rental_bundle_item_snapshot_id  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    rental_order_item_id            uuid        NOT NULL REFERENCES rental_order_items(rental_order_item_id),
    product_variant_id              uuid        NOT NULL REFERENCES product_variants(product_variant_id),
    quantity_per_bundle             integer     NOT NULL CHECK (quantity_per_bundle > 0),
    created_at                      timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE applied_promotions (
    applied_promotion_id    uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    promotion_tier_id       uuid          NOT NULL REFERENCES promotion_tiers(promotion_tier_id),
    discount_amount         numeric(18,0) NOT NULL DEFAULT 0,
    gift_quantity           integer,
    applied_at              timestamptz   NOT NULL DEFAULT now(),
    order_id                uuid          NOT NULL REFERENCES orders(order_id),
    rental_order_item_id    uuid REFERENCES rental_order_items(rental_order_item_id),
    gift_product_variant_id uuid REFERENCES product_variants(product_variant_id),
    status                  varchar(50)   NOT NULL,
    created_at              timestamptz   NOT NULL DEFAULT now()
);

CREATE TABLE asset_assignments (
    asset_assignment_id     uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    rental_order_item_id    uuid REFERENCES rental_order_items(rental_order_item_id),
    equipment_asset_id      uuid        NOT NULL REFERENCES equipment_assets(equipment_asset_id),
    assigned_at             timestamptz NOT NULL,
    returned_at             timestamptz,
    return_condition        varchar(255),
    status                  varchar(50) NOT NULL,
    trip_provided_item_id   uuid REFERENCES trip_provided_items(trip_provided_item_id),
    applied_promotion_id    uuid REFERENCES applied_promotions(applied_promotion_id),
    reserved_from           timestamptz NOT NULL,
    reserved_until          timestamptz NOT NULL,
    created_at              timestamptz NOT NULL DEFAULT now(),
    CHECK (reserved_until > reserved_from)
);

CREATE TABLE rental_charges (
    rental_charge_id    uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    rental_order_id     uuid          NOT NULL REFERENCES rental_orders(order_id),
    equipment_asset_id  uuid REFERENCES equipment_assets(equipment_asset_id),
    charge_type         varchar(50)   NOT NULL,
    amount              numeric(18,0) NOT NULL,
    reason              varchar(500)  NOT NULL,
    status              varchar(50)   NOT NULL,
    created_by_user_id  uuid          NOT NULL REFERENCES accounts(user_id),
    created_at          timestamptz   NOT NULL DEFAULT now(),
    voided_at           timestamptz
);

CREATE TABLE rental_order_adjustments (
    rental_order_adjustment_id  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    rental_order_id             uuid          NOT NULL REFERENCES rental_orders(order_id),
    adjustment_type             varchar(30)   NOT NULL,
    old_end_at                  timestamptz   NOT NULL,
    new_end_at                  timestamptz   NOT NULL,
    extra_amount                numeric(12,2) NOT NULL CHECK (extra_amount >= 0),
    requested_by_user_id        uuid          NOT NULL REFERENCES accounts(user_id),
    created_at                  timestamptz   NOT NULL DEFAULT now()
);

CREATE INDEX ix_refund_policies_type_status ON refund_policies(policy_type, status) WHERE NOT is_deleted;
CREATE INDEX ix_rental_order_adjustments_rental_order_id ON rental_order_adjustments(rental_order_id);
CREATE INDEX ix_rental_order_adjustments_requested_by_user_id ON rental_order_adjustments(requested_by_user_id);

-- =====================================================================
-- PAYMENTS (formerly "transactions")
-- =====================================================================

CREATE TABLE payments (
    payment_id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    order_id                uuid          NOT NULL REFERENCES orders(order_id),
    original_payment_id     uuid REFERENCES payments(payment_id),  -- refund -> original payment
    payment_type            varchar(50)   NOT NULL,
    amount                  numeric(18,0) NOT NULL,
    payment_method          varchar(50)   NOT NULL,
    provider_transaction_id varchar(255),                            -- gateway reference (VNPay/MoMo/...)
    status                  varchar(50)   NOT NULL,
    created_at              timestamptz   NOT NULL DEFAULT now(),
    processed_at            timestamptz,
    refund_policy_rule_id   uuid REFERENCES refund_policy_rules(refund_policy_rule_id),
    purpose                 varchar(50)   NOT NULL
);

-- =====================================================================
-- SYSTEM: NOTIFICATIONS, AUDIT LOGS, CONFIGURATIONS
-- =====================================================================

CREATE TABLE notifications (
    notification_id     uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    recipient_user_id   uuid          NOT NULL REFERENCES accounts(user_id),
    notification_type   varchar(50)   NOT NULL,
    title               varchar(255)  NOT NULL,
    content             varchar(2000) NOT NULL,
    read_at             timestamptz,
    created_at          timestamptz   NOT NULL DEFAULT now(),
    related_entity_type varchar(50),
    related_entity_id   uuid,
    event_key           varchar(200)
);

CREATE UNIQUE INDEX ux_notifications_recipient_event_key
    ON public.notifications (recipient_user_id, event_key) WHERE event_key IS NOT NULL;

CREATE TABLE audit_logs (
    audit_log_id    uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    actor_user_id   uuid REFERENCES accounts(user_id),
    action          varchar(100) NOT NULL,
    entity_type     varchar(100) NOT NULL,
    entity_id       uuid         NOT NULL,
    details         text         NOT NULL,
    created_at      timestamptz  NOT NULL DEFAULT now()
);

CREATE TABLE system_configurations (
    configuration_id    uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    updated_by_user_id  uuid          NOT NULL REFERENCES accounts(user_id),
    config_key          varchar(100)  NOT NULL UNIQUE,
    config_value        varchar(2000) NOT NULL,
    updated_at          timestamptz   NOT NULL DEFAULT now(),
    value_type          varchar(50)   NOT NULL,
    description         text,
    created_at          timestamptz   NOT NULL DEFAULT now(),
    is_deleted          boolean       NOT NULL DEFAULT false
);

-- =====================================================================
-- INDEXES on every foreign-key column (Postgres does not create them)
-- + updated_at triggers + RLS on every table
-- =====================================================================

DO $$
DECLARE
    r record;
BEGIN
    -- FK indexes (skip columns that already lead a PK/UNIQUE index)
    FOR r IN
        SELECT c.conrelid::regclass AS tbl, a.attname AS col
        FROM pg_constraint c
        JOIN pg_attribute a ON a.attrelid = c.conrelid AND a.attnum = c.conkey[1]
        JOIN pg_namespace n ON n.oid = c.connamespace
        WHERE c.contype = 'f' AND n.nspname = 'public'
          AND NOT EXISTS (
              SELECT 1 FROM pg_index i
              WHERE i.indrelid = c.conrelid AND i.indkey[0] = c.conkey[1]
          )
    LOOP
        EXECUTE format('CREATE INDEX IF NOT EXISTS %I ON %s (%I)',
                       'ix_' || r.tbl::text || '_' || r.col, r.tbl, r.col);
    END LOOP;

    -- updated_at triggers
    FOR r IN
        SELECT table_name FROM information_schema.columns
        WHERE table_schema = 'public' AND column_name = 'updated_at'
    LOOP
        EXECUTE format('CREATE TRIGGER trg_%s_updated_at BEFORE UPDATE ON public.%I
                        FOR EACH ROW EXECUTE FUNCTION public.set_updated_at()',
                       r.table_name, r.table_name);
    END LOOP;

    -- Enable RLS so tables are NOT exposed through Supabase's public REST API
    -- (anon/authenticated keys). The ASP.NET backend connecting as the
    -- "postgres" role bypasses RLS, so it is unaffected.
    FOR r IN
        SELECT tablename FROM pg_tables WHERE schemaname = 'public'
    LOOP
        EXECUTE format('ALTER TABLE public.%I ENABLE ROW LEVEL SECURITY', r.tablename);
    END LOOP;
END;
$$;

COMMIT;
