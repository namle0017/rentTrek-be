-- =====================================================================
--  RenTrek – PostgreSQL schema (Supabase)
--  Source: Conceptual.drawio → page "Logical ERD" (51 tables)
--
--  Type mapping:
--    Guid      → uuid  (default gen_random_uuid())
--    string    → varchar(n)
--    text      → text
--    datetime  → timestamptz
--    date      → date
--    time(0)   → time(0)
--    bit       → boolean
--    dec(p,s)  → numeric(p,s)  (money columns → bigint, VND)
--    "?"       → NULL allowed, everything else NOT NULL
--
--  Supabase already gives you the "postgres" database, so this script
--  creates the tables in schema "public". Run it in SQL Editor.
-- =====================================================================

BEGIN;

-- ---------------------------------------------------------------------
-- Helper: auto-update updated_at
-- ---------------------------------------------------------------------
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
-- 1. IDENTITY & AUTH
-- =====================================================================

CREATE TABLE roles (
    role_id         uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    role_name       varchar(50)  NOT NULL UNIQUE,
    description     text,
    created_at      timestamptz  NOT NULL DEFAULT now(),
    updated_at      timestamptz  NOT NULL DEFAULT now(),
    is_deleted      boolean      NOT NULL DEFAULT false
);

CREATE TABLE users (
    user_id             uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    role_id             uuid         NOT NULL REFERENCES roles(role_id),
    email               varchar(255) NOT NULL UNIQUE,
    password_hash       varchar(255),
    full_name           varchar(150) NOT NULL,
    phone               varchar(20),
    status              varchar(30)  NOT NULL,
    email_confirmed_at  timestamptz,
    last_login_at       timestamptz,
    created_at          timestamptz  NOT NULL DEFAULT now(),
    updated_at          timestamptz  NOT NULL DEFAULT now(),
    is_deleted          boolean      NOT NULL DEFAULT false
);

CREATE TABLE profiles (
    profile_id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id             uuid         NOT NULL UNIQUE REFERENCES users(user_id) ON DELETE CASCADE,
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
    user_id                 uuid         NOT NULL REFERENCES users(user_id) ON DELETE CASCADE,
    provider                varchar(50)  NOT NULL,
    provider_subject        varchar(255) NOT NULL,
    created_at              timestamptz  NOT NULL DEFAULT now(),
    CONSTRAINT uq_user_external_logins_provider_subject UNIQUE (provider, provider_subject)
);

CREATE TABLE password_reset_tokens (
    password_reset_token_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id                 uuid         NOT NULL REFERENCES users(user_id) ON DELETE CASCADE,
    token_hash              varchar(255) NOT NULL UNIQUE,
    expires_at              timestamptz  NOT NULL,
    used_at                 timestamptz,
    created_at              timestamptz  NOT NULL DEFAULT now()
);

CREATE TABLE refresh_tokens (
    refresh_token_id                uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id                         uuid         NOT NULL REFERENCES users(user_id) ON DELETE CASCADE,
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
    user_id             uuid         NOT NULL REFERENCES users(user_id) ON DELETE CASCADE,
    refresh_token_id    uuid         NOT NULL REFERENCES refresh_tokens(refresh_token_id) ON DELETE CASCADE,
    jti                 varchar(100) NOT NULL UNIQUE,
    expires_at          timestamptz  NOT NULL,
    revoked_at          timestamptz,
    revoked_reason      varchar(255),
    created_at          timestamptz  NOT NULL DEFAULT now()
);

-- =====================================================================
-- 2. SYSTEM
-- =====================================================================

CREATE TABLE system_configurations (
    configuration_id    uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    updated_by_user_id  uuid         NOT NULL REFERENCES users(user_id),
    config_key          varchar(100) NOT NULL UNIQUE,
    config_value        varchar(1000) NOT NULL,
    value_type          varchar(30)  NOT NULL,
    description         text,
    created_at          timestamptz  NOT NULL DEFAULT now(),
    updated_at          timestamptz  NOT NULL DEFAULT now(),
    is_deleted          boolean      NOT NULL DEFAULT false
);

CREATE TABLE notifications (
    notification_id     uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    recipient_user_id   uuid         NOT NULL REFERENCES users(user_id),
    notification_type   varchar(50)  NOT NULL,
    title               varchar(255) NOT NULL,
    content             varchar(2000) NOT NULL,
    read_at             timestamptz,
    related_entity_type varchar(50),
    related_entity_id   uuid,
    created_at          timestamptz  NOT NULL DEFAULT now()
);

CREATE TABLE audit_logs (
    audit_log_id    uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    actor_user_id   uuid         REFERENCES users(user_id),
    action          varchar(50)  NOT NULL,
    entity_type     varchar(50)  NOT NULL,
    entity_id       uuid         NOT NULL,
    details         text         NOT NULL,
    created_at      timestamptz  NOT NULL DEFAULT now()
);

-- =====================================================================
-- 3. MASTER DATA
-- =====================================================================

CREATE TABLE categories (
    category_id     uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    name            varchar(150) NOT NULL,
    description     text,
    status          varchar(30)  NOT NULL,
    category_type   varchar(30)  NOT NULL,
    created_at      timestamptz  NOT NULL DEFAULT now(),
    updated_at      timestamptz  NOT NULL DEFAULT now(),
    is_deleted      boolean      NOT NULL DEFAULT false
);

CREATE TABLE locations (
    location_id     uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    name            varchar(200) NOT NULL,
    address         varchar(500) NOT NULL,
    latitude        numeric(9,6) NOT NULL,
    longitude       numeric(9,6) NOT NULL,
    created_at      timestamptz  NOT NULL DEFAULT now(),
    updated_at      timestamptz  NOT NULL DEFAULT now(),
    is_deleted      boolean      NOT NULL DEFAULT false
);

-- =====================================================================
-- 4. CONTENT (POSTS)
-- =====================================================================

CREATE TABLE tags (
    tag_id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    name            varchar(100) NOT NULL UNIQUE,
    slug            varchar(120) NOT NULL UNIQUE,
    created_at      timestamptz  NOT NULL DEFAULT now(),
    updated_at      timestamptz  NOT NULL DEFAULT now(),
    is_deleted      boolean      NOT NULL DEFAULT false
);

CREATE TABLE soft_skills (
    soft_skill_id   uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    name            varchar(150) NOT NULL UNIQUE,
    description     text,
    status          varchar(30)  NOT NULL,
    created_at      timestamptz  NOT NULL DEFAULT now(),
    updated_at      timestamptz  NOT NULL DEFAULT now(),
    is_deleted      boolean      NOT NULL DEFAULT false
);

CREATE TABLE posts (
    post_id             uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    created_by_user_id  uuid         NOT NULL REFERENCES users(user_id),
    title               varchar(255) NOT NULL,
    slug                varchar(300) NOT NULL UNIQUE,
    post_type           varchar(30)  NOT NULL,
    content             text         NOT NULL,
    status              varchar(30)  NOT NULL,
    published_at        timestamptz,
    created_at          timestamptz  NOT NULL DEFAULT now(),
    updated_at          timestamptz  NOT NULL DEFAULT now(),
    is_deleted          boolean      NOT NULL DEFAULT false
);

CREATE TABLE post_tags (
    post_tag_id     uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    post_id         uuid         NOT NULL REFERENCES posts(post_id) ON DELETE CASCADE,
    tag_id          uuid         NOT NULL REFERENCES tags(tag_id) ON DELETE CASCADE,
    created_at      timestamptz  NOT NULL DEFAULT now(),
    CONSTRAINT uq_post_tags UNIQUE (post_id, tag_id)
);

CREATE TABLE post_soft_skills (
    post_soft_skill_id  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    post_id             uuid         NOT NULL REFERENCES posts(post_id) ON DELETE CASCADE,
    soft_skill_id       uuid         NOT NULL REFERENCES soft_skills(soft_skill_id) ON DELETE CASCADE,
    created_at          timestamptz  NOT NULL DEFAULT now(),
    CONSTRAINT uq_post_soft_skills UNIQUE (post_id, soft_skill_id)
);

-- =====================================================================
-- 5. REFUND POLICIES
-- =====================================================================

CREATE TABLE refund_policies (
    refund_policy_id    uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    created_by_user_id  uuid         NOT NULL REFERENCES users(user_id),
    policy_type         varchar(30)  NOT NULL,
    name                varchar(200) NOT NULL,
    description         text,
    status              varchar(30)  NOT NULL,
    policy_family_id    uuid         NOT NULL,
    version             integer      NOT NULL DEFAULT 1,
    effective_from      timestamptz  NOT NULL,
    effective_until     timestamptz,
    created_at          timestamptz  NOT NULL DEFAULT now(),
    updated_at          timestamptz  NOT NULL DEFAULT now(),
    is_deleted          boolean      NOT NULL DEFAULT false,
    CONSTRAINT uq_refund_policies_family_version UNIQUE (policy_family_id, version)
);

CREATE TABLE refund_policy_rules (
    refund_policy_rule_id   uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    refund_policy_id        uuid         NOT NULL REFERENCES refund_policies(refund_policy_id),
    min_hours_before        integer      NOT NULL CHECK (min_hours_before >= 0),
    refund_percentage       numeric(5,2) NOT NULL CHECK (refund_percentage BETWEEN 0 AND 100),
    created_at              timestamptz  NOT NULL DEFAULT now(),
    updated_at              timestamptz  NOT NULL DEFAULT now(),
    is_deleted              boolean      NOT NULL DEFAULT false
);

-- =====================================================================
-- 6. PRODUCTS / EQUIPMENT
-- =====================================================================

CREATE TABLE products (
    product_id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    category_id         uuid         NOT NULL REFERENCES categories(category_id),
    created_by_user_id  uuid         NOT NULL REFERENCES users(user_id),
    name                varchar(200) NOT NULL,
    description         text,
    status              varchar(30)  NOT NULL,
    created_at          timestamptz  NOT NULL DEFAULT now(),
    updated_at          timestamptz  NOT NULL DEFAULT now(),
    is_deleted          boolean      NOT NULL DEFAULT false
);

CREATE TABLE product_variants (
    product_variant_id  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    product_id          uuid         NOT NULL REFERENCES products(product_id),
    sku                 varchar(100) NOT NULL UNIQUE,
    variant_name        varchar(200) NOT NULL,
    status              varchar(30)  NOT NULL,
    size                varchar(50),
    color               varchar(50),
    created_at          timestamptz  NOT NULL DEFAULT now(),
    updated_at          timestamptz  NOT NULL DEFAULT now(),
    is_deleted          boolean      NOT NULL DEFAULT false
);

CREATE TABLE product_variant_images (
    image_id            uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    product_variant_id  uuid         NOT NULL REFERENCES product_variants(product_variant_id),
    image_url           text         NOT NULL,
    display_order       integer      NOT NULL DEFAULT 0,
    is_primary          boolean      NOT NULL DEFAULT false,
    created_at          timestamptz  NOT NULL DEFAULT now(),
    updated_at          timestamptz  NOT NULL DEFAULT now(),
    is_deleted          boolean      NOT NULL DEFAULT false
);

CREATE TABLE equipment_assets (
    equipment_asset_id  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    product_variant_id  uuid         NOT NULL REFERENCES product_variants(product_variant_id),
    asset_code          varchar(100) NOT NULL UNIQUE,
    asset_status        varchar(30)  NOT NULL,
    condition_note      text,
    created_at          timestamptz  NOT NULL DEFAULT now(),
    updated_at          timestamptz  NOT NULL DEFAULT now(),
    is_deleted          boolean      NOT NULL DEFAULT false
);

CREATE TABLE product_bundles (
    bundle_id       uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    category_id     uuid         NOT NULL REFERENCES categories(category_id),
    name            varchar(200) NOT NULL,
    description     text,
    status          varchar(30)  NOT NULL,
    created_at      timestamptz  NOT NULL DEFAULT now(),
    updated_at      timestamptz  NOT NULL DEFAULT now(),
    is_deleted      boolean      NOT NULL DEFAULT false
);

CREATE TABLE product_bundle_items (
    bundle_item_id      uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    bundle_id           uuid         NOT NULL REFERENCES product_bundles(bundle_id),
    product_variant_id  uuid         NOT NULL REFERENCES product_variants(product_variant_id),
    quantity            integer      NOT NULL CHECK (quantity > 0),
    created_at          timestamptz  NOT NULL DEFAULT now(),
    updated_at          timestamptz  NOT NULL DEFAULT now(),
    is_deleted          boolean      NOT NULL DEFAULT false
);

-- A price tier belongs to EITHER a variant OR a bundle
CREATE TABLE rental_price_tiers (
    price_tier_id       uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    product_variant_id  uuid         REFERENCES product_variants(product_variant_id),
    bundle_id           uuid         REFERENCES product_bundles(bundle_id),
    min_days            integer      NOT NULL CHECK (min_days >= 1),
    max_days            integer,
    daily_price         bigint        NOT NULL CHECK (daily_price >= 0),
    created_at          timestamptz  NOT NULL DEFAULT now(),
    updated_at          timestamptz  NOT NULL DEFAULT now(),
    is_deleted          boolean      NOT NULL DEFAULT false,
    CONSTRAINT ck_rental_price_tiers_target CHECK (num_nonnulls(product_variant_id, bundle_id) = 1),
    CONSTRAINT ck_rental_price_tiers_days   CHECK (max_days IS NULL OR max_days >= min_days)
);

-- =====================================================================
-- 7. TOURS
-- =====================================================================

CREATE TABLE tours (
    tour_id             uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    category_id         uuid         NOT NULL REFERENCES categories(category_id),
    main_location_id    uuid         NOT NULL REFERENCES locations(location_id),
    created_by_user_id  uuid         NOT NULL REFERENCES users(user_id),
    tour_family_id      uuid         NOT NULL,
    version             integer      NOT NULL DEFAULT 1,
    name                varchar(255) NOT NULL,
    description         text,
    duration_days       integer      NOT NULL,
    duration_nights     integer      NOT NULL,
    duration_minutes    integer      NOT NULL,
    base_price          bigint        NOT NULL CHECK (base_price >= 0),
    status              varchar(30)  NOT NULL,
    itinerary_locked_at timestamptz,
    created_at          timestamptz  NOT NULL DEFAULT now(),
    updated_at          timestamptz  NOT NULL DEFAULT now(),
    is_deleted          boolean      NOT NULL DEFAULT false,
    CONSTRAINT uq_tours_family_version UNIQUE (tour_family_id, version)
);

CREATE TABLE tour_images (
    tour_image_id   uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tour_id         uuid         NOT NULL REFERENCES tours(tour_id),
    image_url       text         NOT NULL,
    display_order   integer      NOT NULL DEFAULT 0,
    is_primary      boolean      NOT NULL DEFAULT false,
    created_at      timestamptz  NOT NULL DEFAULT now(),
    updated_at      timestamptz  NOT NULL DEFAULT now(),
    is_deleted      boolean      NOT NULL DEFAULT false
);

CREATE TABLE tour_activities (
    tour_activity_id    uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tour_id             uuid         NOT NULL REFERENCES tours(tour_id),
    name                varchar(255) NOT NULL,
    description         text,
    day_number          integer      NOT NULL CHECK (day_number >= 1),
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
    tour_id                 uuid         NOT NULL REFERENCES tours(tour_id),
    product_variant_id      uuid         NOT NULL REFERENCES product_variants(product_variant_id),
    note                    text,
    quantity_basis          varchar(30)  NOT NULL,
    quantity                integer      NOT NULL CHECK (quantity > 0),
    created_at              timestamptz  NOT NULL DEFAULT now(),
    updated_at              timestamptz  NOT NULL DEFAULT now(),
    is_deleted              boolean      NOT NULL DEFAULT false
);

CREATE TABLE tour_recurrence_rules (
    recurrence_rule_id  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tour_id             uuid         NOT NULL REFERENCES tours(tour_id),
    frequency           varchar(20)  NOT NULL,
    interval_value      integer      NOT NULL DEFAULT 1 CHECK (interval_value >= 1),
    start_date          date         NOT NULL,
    end_date            date         NOT NULL,
    start_time          time(0)      NOT NULL,
    days_of_week        varchar(50),
    default_total_slots integer      NOT NULL CHECK (default_total_slots > 0),
    default_price       bigint        NOT NULL CHECK (default_price >= 0),
    is_active           boolean      NOT NULL DEFAULT true,
    created_at          timestamptz  NOT NULL DEFAULT now(),
    updated_at          timestamptz  NOT NULL DEFAULT now(),
    is_deleted          boolean      NOT NULL DEFAULT false,
    CONSTRAINT ck_tour_recurrence_rules_dates CHECK (end_date >= start_date)
);

CREATE TABLE tour_posts (
    tour_post_id    uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tour_id         uuid         NOT NULL REFERENCES tours(tour_id) ON DELETE CASCADE,
    post_id         uuid         NOT NULL REFERENCES posts(post_id) ON DELETE CASCADE,
    display_order   integer      NOT NULL DEFAULT 0,
    created_at      timestamptz  NOT NULL DEFAULT now(),
    CONSTRAINT uq_tour_posts UNIQUE (tour_id, post_id)
);

-- =====================================================================
-- 8. TRIPS
-- =====================================================================

CREATE TABLE trips (
    trip_id             uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tour_id             uuid         NOT NULL REFERENCES tours(tour_id),
    recurrence_rule_id  uuid         REFERENCES tour_recurrence_rules(recurrence_rule_id),
    refund_policy_id    uuid         NOT NULL REFERENCES refund_policies(refund_policy_id),
    managed_by_user_id  uuid         NOT NULL REFERENCES users(user_id),
    start_at            timestamptz  NOT NULL,
    end_at              timestamptz  NOT NULL,
    total_slots         integer      NOT NULL CHECK (total_slots > 0),
    min_participants    integer      NOT NULL DEFAULT 1 CHECK (min_participants >= 0),
    price_per_person    bigint        NOT NULL CHECK (price_per_person >= 0),
    status              varchar(30)  NOT NULL,
    row_version         integer      NOT NULL DEFAULT 0,   -- optimistic concurrency
    created_at          timestamptz  NOT NULL DEFAULT now(),
    updated_at          timestamptz  NOT NULL DEFAULT now(),
    is_deleted          boolean      NOT NULL DEFAULT false,
    CONSTRAINT ck_trips_time CHECK (end_at > start_at)
);

CREATE TABLE trip_groups (
    trip_group_id       uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    trip_id             uuid         NOT NULL REFERENCES trips(trip_id),
    guide_user_id       uuid         NOT NULL REFERENCES users(user_id),
    group_name          varchar(100) NOT NULL,
    max_participants    integer      NOT NULL CHECK (max_participants > 0),
    status              varchar(30)  NOT NULL,
    created_at          timestamptz  NOT NULL DEFAULT now(),
    updated_at          timestamptz  NOT NULL DEFAULT now(),
    is_deleted          boolean      NOT NULL DEFAULT false
);

CREATE TABLE trip_provided_items (
    trip_provided_item_id   uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    trip_id                 uuid         NOT NULL REFERENCES trips(trip_id),
    tour_provided_item_id   uuid         NOT NULL REFERENCES tour_provided_items(tour_provided_item_id),
    product_variant_id      uuid         NOT NULL REFERENCES product_variants(product_variant_id),
    prepared_by_user_id     uuid         REFERENCES users(user_id),
    required_quantity       integer      NOT NULL CHECK (required_quantity >= 0),
    status                  varchar(30)  NOT NULL,
    created_at              timestamptz  NOT NULL DEFAULT now()
);

-- =====================================================================
-- 9. PROMOTIONS
-- =====================================================================

CREATE TABLE promotions (
    promotion_id        uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    created_by_user_id  uuid         NOT NULL REFERENCES users(user_id),
    name                varchar(200) NOT NULL,
    description         text,
    start_at            timestamptz  NOT NULL,
    end_at              timestamptz  NOT NULL,
    status              varchar(30)  NOT NULL,
    applies_to          varchar(30)  NOT NULL,
    application_scope   varchar(30)  NOT NULL,
    promotion_code      varchar(50)  UNIQUE,
    max_uses            integer,
    max_uses_per_user   integer,
    is_stackable        boolean      NOT NULL DEFAULT false,
    priority            integer      NOT NULL DEFAULT 0,
    created_at          timestamptz  NOT NULL DEFAULT now(),
    updated_at          timestamptz  NOT NULL DEFAULT now(),
    is_deleted          boolean      NOT NULL DEFAULT false,
    CONSTRAINT ck_promotions_time CHECK (end_at > start_at)
);

CREATE TABLE promotion_tiers (
    promotion_tier_id       uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    promotion_id            uuid         NOT NULL REFERENCES promotions(promotion_id),
    gift_product_variant_id uuid         REFERENCES product_variants(product_variant_id),
    qualification_metric    varchar(30)  NOT NULL,
    min_threshold           bigint        NOT NULL CHECK (min_threshold >= 0),
    discount_type           varchar(30)  NOT NULL,
    discount_value          bigint        NOT NULL CHECK (discount_value >= 0),
    gift_quantity           integer,
    created_at              timestamptz  NOT NULL DEFAULT now(),
    updated_at              timestamptz  NOT NULL DEFAULT now(),
    is_deleted              boolean      NOT NULL DEFAULT false
);

-- =====================================================================
-- 10. ORDERS (supertype) + TRIP_BOOKING_ORDERS / RENTAL_ORDERS (subtypes)
-- =====================================================================

CREATE TABLE orders (
    order_id            uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    customer_user_id    uuid         NOT NULL REFERENCES users(user_id),
    refund_policy_id    uuid         NOT NULL REFERENCES refund_policies(refund_policy_id),
    order_code          varchar(50)  NOT NULL UNIQUE,
    order_type          varchar(30)  NOT NULL,
    subtotal_amount     bigint        NOT NULL DEFAULT 0 CHECK (subtotal_amount >= 0),
    discount_amount     bigint        NOT NULL DEFAULT 0 CHECK (discount_amount >= 0),
    total_amount        bigint        NOT NULL DEFAULT 0 CHECK (total_amount >= 0),
    status              varchar(30)  NOT NULL,
    payment_status      varchar(30)  NOT NULL,
    note                text,
    hold_expires_at     timestamptz,
    reconciliation_deadline_at timestamptz,
    idempotency_key      varchar(100),
    cancelled_at        timestamptz,
    cancel_reason       varchar(500),
    created_at          timestamptz  NOT NULL DEFAULT now(),
    updated_at          timestamptz  NOT NULL DEFAULT now(),
    is_deleted          boolean      NOT NULL DEFAULT false
);

CREATE TABLE trip_booking_orders (
    order_id            uuid PRIMARY KEY REFERENCES orders(order_id),
    trip_id             uuid         NOT NULL REFERENCES trips(trip_id),
    participant_count   integer      NOT NULL CHECK (participant_count > 0),
    unit_price          bigint        NOT NULL CHECK (unit_price >= 0)
);

CREATE TABLE rental_orders (
    order_id            uuid PRIMARY KEY REFERENCES orders(order_id),
    handled_by_user_id  uuid         REFERENCES users(user_id),
    rental_start_at     timestamptz  NOT NULL,
    rental_end_at       timestamptz  NOT NULL,
    deposit_amount      bigint        NOT NULL DEFAULT 0 CHECK (deposit_amount >= 0),
    CONSTRAINT ck_rental_orders_time CHECK (rental_end_at > rental_start_at)
);

-- A rental line is EITHER a single variant OR a bundle
CREATE TABLE rental_order_items (
    rental_order_item_id    uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    rental_order_id         uuid         NOT NULL REFERENCES rental_orders(order_id),
    product_variant_id      uuid         REFERENCES product_variants(product_variant_id),
    bundle_id               uuid         REFERENCES product_bundles(bundle_id),
    quantity                integer      NOT NULL CHECK (quantity > 0),
    billable_days           integer      NOT NULL CHECK (billable_days > 0),
    daily_price             bigint        NOT NULL CHECK (daily_price >= 0),
    line_amount             bigint        NOT NULL CHECK (line_amount >= 0),
    created_at              timestamptz  NOT NULL DEFAULT now(),
    updated_at              timestamptz  NOT NULL DEFAULT now(),
    is_deleted              boolean      NOT NULL DEFAULT false,
    CONSTRAINT ck_rental_order_items_target CHECK (num_nonnulls(product_variant_id, bundle_id) = 1)
);

CREATE TABLE rental_bundle_item_snapshots (
    rental_bundle_item_snapshot_id  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    rental_order_item_id            uuid    NOT NULL REFERENCES rental_order_items(rental_order_item_id),
    product_variant_id              uuid    NOT NULL REFERENCES product_variants(product_variant_id),
    quantity_per_bundle             integer NOT NULL CHECK (quantity_per_bundle > 0),
    created_at                      timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE rental_charges (
    rental_charge_id    uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    rental_order_id     uuid         NOT NULL REFERENCES rental_orders(order_id),
    equipment_asset_id  uuid         REFERENCES equipment_assets(equipment_asset_id),
    created_by_user_id  uuid         NOT NULL REFERENCES users(user_id),
    charge_type         varchar(30)  NOT NULL,
    amount              bigint        NOT NULL CHECK (amount >= 0),
    reason              varchar(500) NOT NULL,
    status              varchar(30)  NOT NULL,
    created_at          timestamptz  NOT NULL DEFAULT now(),
    voided_at           timestamptz
);

CREATE TABLE applied_promotions (
    applied_promotion_id    uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    promotion_tier_id       uuid         NOT NULL REFERENCES promotion_tiers(promotion_tier_id),
    order_id                uuid         NOT NULL REFERENCES orders(order_id),
    rental_order_item_id    uuid         REFERENCES rental_order_items(rental_order_item_id),
    gift_product_variant_id uuid         REFERENCES product_variants(product_variant_id),
    discount_amount         bigint        NOT NULL DEFAULT 0 CHECK (discount_amount >= 0),
    gift_quantity           integer,
    status                  varchar(30)  NOT NULL,
    applied_at              timestamptz  NOT NULL DEFAULT now(),
    created_at              timestamptz  NOT NULL DEFAULT now()
);

CREATE TABLE asset_assignments (
    asset_assignment_id     uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    equipment_asset_id      uuid         NOT NULL REFERENCES equipment_assets(equipment_asset_id),
    rental_order_item_id    uuid         REFERENCES rental_order_items(rental_order_item_id),
    trip_provided_item_id   uuid         REFERENCES trip_provided_items(trip_provided_item_id),
    applied_promotion_id    uuid         REFERENCES applied_promotions(applied_promotion_id),
    reserved_from           timestamptz  NOT NULL,
    reserved_until          timestamptz  NOT NULL,
    assigned_at             timestamptz  NOT NULL,
    returned_at             timestamptz,
    return_condition        varchar(50),
    status                  varchar(30)  NOT NULL,
    created_at              timestamptz  NOT NULL DEFAULT now(),
    CONSTRAINT ck_asset_assignments_time CHECK (reserved_until > reserved_from)
);

CREATE TABLE transactions (
    transaction_id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    order_id                uuid         NOT NULL REFERENCES orders(order_id),
    original_transaction_id uuid         REFERENCES transactions(transaction_id),  -- "Refunded by"
    refund_policy_rule_id   uuid         REFERENCES refund_policy_rules(refund_policy_rule_id),
    transaction_type        varchar(30)  NOT NULL,
    purpose                 varchar(30)  NOT NULL,
    amount                  bigint        NOT NULL CHECK (amount >= 0),
    payment_method          varchar(30)  NOT NULL,
    provider_transaction_id varchar(255),
    status                  varchar(30)  NOT NULL,
    processed_at            timestamptz,
    created_at              timestamptz  NOT NULL DEFAULT now()
);

-- =====================================================================
-- 11. PARTICIPANTS, ATTENDANCE, REVIEWS
-- =====================================================================

CREATE TABLE booking_participants (
    participant_id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    booking_order_id        uuid         NOT NULL REFERENCES trip_booking_orders(order_id),
    trip_id                 uuid         NOT NULL REFERENCES trips(trip_id),
    linked_user_id          uuid         REFERENCES users(user_id),
    trip_group_id           uuid         REFERENCES trip_groups(trip_group_id),
    full_name               varchar(150) NOT NULL,
    email                   varchar(255),
    phone                   varchar(20),
    date_of_birth           date,
    emergency_contact_name  varchar(150),
    emergency_contact_phone varchar(20),
    status                  varchar(30)  NOT NULL,
    created_at              timestamptz  NOT NULL DEFAULT now(),
    updated_at              timestamptz  NOT NULL DEFAULT now(),
    is_deleted              boolean      NOT NULL DEFAULT false
);

CREATE TABLE activity_attendances (
    attendance_id       uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tour_activity_id    uuid         NOT NULL REFERENCES tour_activities(tour_activity_id),
    participant_id      uuid         NOT NULL REFERENCES booking_participants(participant_id),
    checked_by_user_id  uuid         NOT NULL REFERENCES users(user_id),
    attendance_status   varchar(30)  NOT NULL,
    checked_at          timestamptz  NOT NULL,
    created_at          timestamptz  NOT NULL DEFAULT now(),
    CONSTRAINT uq_activity_attendances UNIQUE (tour_activity_id, participant_id)
);

CREATE TABLE participant_post_reads (
    participant_post_read_id    uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    participant_id              uuid NOT NULL REFERENCES booking_participants(participant_id),
    post_id                     uuid NOT NULL REFERENCES posts(post_id),
    read_at                     timestamptz NOT NULL DEFAULT now(),
    created_at                  timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_participant_post_reads UNIQUE (participant_id, post_id)
);

CREATE TABLE reviews (
    review_id       uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    participant_id  uuid         NOT NULL UNIQUE REFERENCES booking_participants(participant_id),
    is_hidden boolean NOT NULL DEFAULT false,
    hidden_reason text,
    rating          integer      NOT NULL CHECK (rating BETWEEN 1 AND 5),
    comment         text,
    created_at      timestamptz  NOT NULL DEFAULT now(),
    updated_at      timestamptz  NOT NULL DEFAULT now(),
    is_deleted      boolean      NOT NULL DEFAULT false
);

-- =====================================================================
-- 12. INDEXES ON FOREIGN KEYS
--     (PostgreSQL does not index FK columns automatically; columns that
--      already have UNIQUE / PK / a leading composite UNIQUE are skipped)
-- =====================================================================

CREATE INDEX ix_users_role_id                         ON users(role_id);
CREATE INDEX ix_user_external_logins_user_id          ON user_external_logins(user_id);
CREATE INDEX ix_password_reset_tokens_user_id         ON password_reset_tokens(user_id);
CREATE INDEX ix_refresh_tokens_user_id                ON refresh_tokens(user_id);
CREATE INDEX ix_refresh_tokens_family_id              ON refresh_tokens(family_id);
CREATE INDEX ix_access_tokens_user_id                 ON access_tokens(user_id);
CREATE INDEX ix_access_tokens_refresh_token_id        ON access_tokens(refresh_token_id);
CREATE INDEX ix_system_configurations_updated_by      ON system_configurations(updated_by_user_id);
CREATE INDEX ix_notifications_recipient_user_id       ON notifications(recipient_user_id);
CREATE INDEX ix_audit_logs_actor_user_id              ON audit_logs(actor_user_id);
CREATE INDEX ix_audit_logs_entity                     ON audit_logs(entity_type, entity_id);
CREATE INDEX ix_posts_created_by_user_id              ON posts(created_by_user_id);
CREATE INDEX ix_post_tags_tag_id                      ON post_tags(tag_id);
CREATE INDEX ix_post_soft_skills_soft_skill_id        ON post_soft_skills(soft_skill_id);
CREATE INDEX ix_refund_policies_created_by_user_id    ON refund_policies(created_by_user_id);
CREATE INDEX ix_refund_policy_rules_refund_policy_id  ON refund_policy_rules(refund_policy_id);
CREATE INDEX ix_products_category_id                  ON products(category_id);
CREATE INDEX ix_products_created_by_user_id           ON products(created_by_user_id);
CREATE INDEX ix_product_variants_product_id           ON product_variants(product_id);
CREATE INDEX ix_product_variant_images_variant_id     ON product_variant_images(product_variant_id);
CREATE INDEX ix_equipment_assets_variant_id           ON equipment_assets(product_variant_id);
CREATE INDEX ix_product_bundles_category_id           ON product_bundles(category_id);
CREATE INDEX ix_product_bundle_items_bundle_id        ON product_bundle_items(bundle_id);
CREATE INDEX ix_product_bundle_items_variant_id       ON product_bundle_items(product_variant_id);
CREATE INDEX ix_rental_price_tiers_variant_id         ON rental_price_tiers(product_variant_id);
CREATE INDEX ix_rental_price_tiers_bundle_id          ON rental_price_tiers(bundle_id);
CREATE INDEX ix_tours_category_id                     ON tours(category_id);
CREATE INDEX ix_tours_main_location_id                ON tours(main_location_id);
CREATE INDEX ix_tours_created_by_user_id              ON tours(created_by_user_id);
CREATE INDEX ix_tour_images_tour_id                   ON tour_images(tour_id);
CREATE INDEX ix_tour_activities_tour_id               ON tour_activities(tour_id);
CREATE INDEX ix_tour_preparation_items_tour_id        ON tour_preparation_items(tour_id);
CREATE INDEX ix_tour_provided_items_tour_id           ON tour_provided_items(tour_id);
CREATE INDEX ix_tour_provided_items_variant_id        ON tour_provided_items(product_variant_id);
CREATE INDEX ix_tour_recurrence_rules_tour_id         ON tour_recurrence_rules(tour_id);
CREATE INDEX ix_tour_posts_post_id                    ON tour_posts(post_id);
CREATE INDEX ix_trips_tour_id                         ON trips(tour_id);
CREATE INDEX ix_trips_recurrence_rule_id              ON trips(recurrence_rule_id);
CREATE INDEX ix_trips_refund_policy_id                ON trips(refund_policy_id);
CREATE INDEX ix_trips_managed_by_user_id              ON trips(managed_by_user_id);
CREATE INDEX ix_trips_start_at                        ON trips(start_at);
CREATE INDEX ix_trip_groups_trip_id                   ON trip_groups(trip_id);
CREATE INDEX ix_trip_groups_guide_user_id             ON trip_groups(guide_user_id);
CREATE INDEX ix_trip_provided_items_trip_id           ON trip_provided_items(trip_id);
CREATE INDEX ix_trip_provided_items_tour_item_id      ON trip_provided_items(tour_provided_item_id);
CREATE INDEX ix_trip_provided_items_variant_id        ON trip_provided_items(product_variant_id);
CREATE INDEX ix_trip_provided_items_prepared_by       ON trip_provided_items(prepared_by_user_id);
CREATE INDEX ix_promotions_created_by_user_id         ON promotions(created_by_user_id);
CREATE INDEX ix_promotion_tiers_promotion_id          ON promotion_tiers(promotion_id);
CREATE INDEX ix_promotion_tiers_gift_variant_id       ON promotion_tiers(gift_product_variant_id);
CREATE INDEX ix_orders_customer_user_id               ON orders(customer_user_id);
CREATE INDEX ix_orders_refund_policy_id               ON orders(refund_policy_id);
CREATE INDEX ix_trip_booking_orders_trip_id           ON trip_booking_orders(trip_id);
CREATE INDEX ix_rental_orders_handled_by_user_id      ON rental_orders(handled_by_user_id);
CREATE INDEX ix_rental_order_items_rental_order_id    ON rental_order_items(rental_order_id);
CREATE INDEX ix_rental_order_items_variant_id         ON rental_order_items(product_variant_id);
CREATE INDEX ix_rental_order_items_bundle_id          ON rental_order_items(bundle_id);
CREATE INDEX ix_rental_bundle_snapshots_item_id       ON rental_bundle_item_snapshots(rental_order_item_id);
CREATE INDEX ix_rental_bundle_snapshots_variant_id    ON rental_bundle_item_snapshots(product_variant_id);
CREATE INDEX ix_rental_charges_rental_order_id        ON rental_charges(rental_order_id);
CREATE INDEX ix_rental_charges_equipment_asset_id     ON rental_charges(equipment_asset_id);
CREATE INDEX ix_rental_charges_created_by_user_id     ON rental_charges(created_by_user_id);
CREATE INDEX ix_applied_promotions_tier_id            ON applied_promotions(promotion_tier_id);
CREATE INDEX ix_applied_promotions_order_id           ON applied_promotions(order_id);
CREATE INDEX ix_applied_promotions_rental_item_id     ON applied_promotions(rental_order_item_id);
CREATE INDEX ix_applied_promotions_gift_variant_id    ON applied_promotions(gift_product_variant_id);
CREATE INDEX ix_asset_assignments_asset_id            ON asset_assignments(equipment_asset_id);
CREATE INDEX ix_asset_assignments_asset_period        ON asset_assignments(equipment_asset_id, reserved_from, reserved_until);
CREATE INDEX ix_asset_assignments_rental_item_id      ON asset_assignments(rental_order_item_id);
CREATE INDEX ix_asset_assignments_trip_item_id        ON asset_assignments(trip_provided_item_id);
CREATE INDEX ix_asset_assignments_applied_promo_id    ON asset_assignments(applied_promotion_id);
CREATE INDEX ix_transactions_order_id                 ON transactions(order_id);
CREATE INDEX ix_transactions_original_id              ON transactions(original_transaction_id);
CREATE INDEX ix_transactions_refund_policy_rule_id    ON transactions(refund_policy_rule_id);
CREATE INDEX ix_booking_participants_booking_order_id ON booking_participants(booking_order_id);
CREATE INDEX ix_booking_participants_trip_id          ON booking_participants(trip_id);
CREATE INDEX ix_booking_participants_linked_user_id   ON booking_participants(linked_user_id);
CREATE INDEX ix_booking_participants_trip_group_id    ON booking_participants(trip_group_id);
CREATE INDEX ix_activity_attendances_participant_id   ON activity_attendances(participant_id);
CREATE INDEX ix_activity_attendances_checked_by       ON activity_attendances(checked_by_user_id);
CREATE INDEX ix_participant_post_reads_post_id        ON participant_post_reads(post_id);

-- =====================================================================
-- 13. updated_at TRIGGERS + ROW LEVEL SECURITY
-- =====================================================================
DO $$
DECLARE
    t text;
BEGIN
    -- Trigger set_updated_at for every table that has an updated_at column
    FOR t IN
        SELECT c.table_name
        FROM information_schema.columns c
        JOIN information_schema.tables tb
          ON tb.table_schema = c.table_schema AND tb.table_name = c.table_name
        WHERE c.table_schema = 'public'
          AND c.column_name  = 'updated_at'
          AND tb.table_type  = 'BASE TABLE'
    LOOP
        EXECUTE format(
            'CREATE TRIGGER trg_%1$s_updated_at BEFORE UPDATE ON public.%1$I
             FOR EACH ROW EXECUTE FUNCTION public.set_updated_at();', t);
    END LOOP;

    -- Supabase exposes "public" through its REST API (anon/authenticated keys).
    -- Enable RLS with no policies so only the backend (postgres / service_role
    -- connection) can read/write these tables.
    FOR t IN
        SELECT table_name
        FROM information_schema.tables
        WHERE table_schema = 'public' AND table_type = 'BASE TABLE'
    LOOP
        EXECUTE format('ALTER TABLE public.%I ENABLE ROW LEVEL SECURITY;', t);
    END LOOP;
END;
$$;

COMMIT;
