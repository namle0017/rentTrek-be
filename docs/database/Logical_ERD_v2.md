# Logical ERD v2 — renTrek

> Sinh tự động từ `Logical_ERD_v2.drawio` (trang *Logical ERD*). Giữ nguyên 44 bảng gốc, thêm 2 bảng **ACCESS_TOKENS** và **REFRESH_TOKENS** (đánh dấu 🆕). Các đề xuất sửa nằm ở `ERD_REVIEW.md`, **chưa** áp dụng vào bản này.

Tổng: **46 bảng**, **76 quan hệ**.

## Mục lục

- [ACCESS_TOKENS 🆕](#access_tokens)
- [ACTIVITY_ATTENDANCES](#activity_attendances)
- [APPLIED_PROMOTIONS](#applied_promotions)
- [AUDIT_LOGS](#audit_logs)
- [BOOKING_PARTICIPANTS](#booking_participants)
- [CATEGORIES](#categories)
- [EQUIPMENT_ASSETS](#equipment_assets)
- [LOCATIONS](#locations)
- [NOTIFICATIONS](#notifications)
- [ORDERS](#orders)
- [PARTICIPANT_POST_READS](#participant_post_reads)
- [POSTS](#posts)
- [POST_SOFT_SKILLS](#post_soft_skills)
- [POST_TAGS](#post_tags)
- [PRODUCTS](#products)
- [PRODUCT_BUNDLES](#product_bundles)
- [PRODUCT_BUNDLE_ITEMS](#product_bundle_items)
- [PRODUCT_VARIANTS](#product_variants)
- [PRODUCT_VARIANT_IMAGES](#product_variant_images)
- [PROFILES](#profiles)
- [PROMOTIONS](#promotions)
- [PROMOTION_TIERS](#promotion_tiers)
- [REFRESH_TOKENS 🆕](#refresh_tokens)
- [REFUND_POLICIES](#refund_policies)
- [REFUND_POLICY_RULES](#refund_policy_rules)
- [RENTAL_ASSET_ASSIGNMENTS](#rental_asset_assignments)
- [RENTAL_ORDERS](#rental_orders)
- [RENTAL_ORDER_ITEMS](#rental_order_items)
- [RENTAL_PRICE_TIERS](#rental_price_tiers)
- [REVIEWS](#reviews)
- [ROLES](#roles)
- [SOFT_SKILLS](#soft_skills)
- [SYSTEM_CONFIGURATIONS](#system_configurations)
- [TAGS](#tags)
- [TOURS](#tours)
- [TOUR_POSTS](#tour_posts)
- [TOUR_PREPARATION_ITEMS](#tour_preparation_items)
- [TOUR_PROVIDED_ITEMS](#tour_provided_items)
- [TOUR_RECURRENCE_RULES](#tour_recurrence_rules)
- [TRANSACTIONS](#transactions)
- [TRIPS](#trips)
- [TRIP_ACTIVITIES](#trip_activities)
- [TRIP_BOOKING_ORDERS](#trip_booking_orders)
- [TRIP_GROUPS](#trip_groups)
- [TRIP_PROVIDED_ITEMS](#trip_provided_items)
- [USERS](#users)

## ACCESS_TOKENS

🆕 Bảng mới — xem thiết kế chi tiết ở `ERD_REVIEW.md` §2.

| Kiểu | Cột | Khoá |
|---|---|---|
| bigint | `access_token_id` | PK |
| bigint | `user_id` | FK |
| bigint | `refresh_token_id` | FK |
| string | `jti` | UQ |
| datetime | `expires_at` |  |
| datetime | `revoked_at` |  |
| string | `revoked_reason` |  |
| datetime | `created_at` |  |

## REFRESH_TOKENS

🆕 Bảng mới — xem thiết kế chi tiết ở `ERD_REVIEW.md` §2.

| Kiểu | Cột | Khoá |
|---|---|---|
| bigint | `refresh_token_id` | PK |
| bigint | `user_id` | FK |
| string | `family_id` |  |
| string | `token_hash` | UQ |
| datetime | `expires_at` |  |
| datetime | `created_at` |  |
| datetime | `revoked_at` |  |
| string | `revoked_reason` |  |
| bigint | `replaced_by_refresh_token_id` | FK |

## ACTIVITY_ATTENDANCES

| Kiểu | Cột | Khoá |
|---|---|---|
| bigint | `attendance_id` | PK |
| bigint | `activity_id` | FK |
| bigint | `participant_id` | FK |
| string | `attendance_status` |  |
| datetime | `checked_at` |  |

## APPLIED_PROMOTIONS

| Kiểu | Cột | Khoá |
|---|---|---|
| bigint | `applied_promotion_id` | PK |
| bigint | `promotion_id` | FK |
| bigint | `promotion_tier_id` | FK |
| bigint | `booking_order_id` | FK |
| bigint | `rental_order_id` | FK |
| decimal | `discount_amount` |  |
| int | `gift_quantity` |  |
| datetime | `applied_at` |  |

## AUDIT_LOGS

| Kiểu | Cột | Khoá |
|---|---|---|
| bigint | `audit_log_id` | PK |
| bigint | `actor_user_id` | FK |
| string | `action` |  |
| string | `entity_type` |  |
| bigint | `entity_id` |  |
| string | `details` |  |
| datetime | `created_at` |  |

## BOOKING_PARTICIPANTS

| Kiểu | Cột | Khoá |
|---|---|---|
| bigint | `participant_id` | PK |
| bigint | `booking_order_id` | FK |
| bigint | `linked_user_id` | FK |
| bigint | `trip_group_id` | FK |
| string | `full_name` |  |
| string | `email` |  |
| string | `phone` |  |
| string | `status` |  |

## CATEGORIES

| Kiểu | Cột | Khoá |
|---|---|---|
| bigint | `category_id` | PK |
| string | `category_name` |  |
| string | `description` |  |
| string | `status` |  |
| bigint | `admin_id` | FK |

## EQUIPMENT_ASSETS

| Kiểu | Cột | Khoá |
|---|---|---|
| bigint | `equipment_asset_id` | PK |
| bigint | `product_variant_id` | FK |
| string | `asset_code` |  |
| string | `asset_status` |  |
| string | `condition_note` |  |

## LOCATIONS

| Kiểu | Cột | Khoá |
|---|---|---|
| bigint | `location_id` | PK |
| string | `name` |  |
| string | `address` |  |
| decimal | `latitude` |  |
| decimal | `longitude` |  |

## NOTIFICATIONS

| Kiểu | Cột | Khoá |
|---|---|---|
| bigint | `notification_id` | PK |
| bigint | `recipient_user_id` | FK |
| string | `notification_type` |  |
| string | `title` |  |
| string | `content` |  |
| boolean | `is_read` |  |
| datetime | `created_at` |  |

## ORDERS

| Kiểu | Cột | Khoá |
|---|---|---|
| bigint | `order_id` | PK |
| bigint | `customer_user_id` | FK |
| string | `order_type` |  |
| decimal | `total_amount` |  |
| string | `status` |  |
| datetime | `created_at` |  |
| datetime | `updated_at` |  |

## PARTICIPANT_POST_READS

| Kiểu | Cột | Khoá |
|---|---|---|
| bigint | `participant_post_read_id` | PK |
| bigint | `participant_id` | FK |
| bigint | `post_id` | FK |
| boolean | `is_acknowledged` |  |
| datetime | `read_at` |  |

## POSTS

| Kiểu | Cột | Khoá |
|---|---|---|
| bigint | `post_id` | PK |
| bigint | `created_by_user_id (ADMIN)` | FK |
| string | `title` |  |
| string | `slug` |  |
| string | `post_type` |  |
| string | `category` |  |
| string | `content` |  |
| string | `status` |  |
| datetime | `published_at` |  |
| datetime | `created_at` |  |

## POST_SOFT_SKILLS

| Kiểu | Cột | Khoá |
|---|---|---|
| bigint | `post_soft_skill_id` | PK |
| bigint | `post_id` | FK |
| bigint | `soft_skill_id` | FK |

## POST_TAGS

| Kiểu | Cột | Khoá |
|---|---|---|
| bigint | `post_tag_id` | PK |
| bigint | `post_id` | FK |
| bigint | `tag_id` | FK |

## PRODUCTS

| Kiểu | Cột | Khoá |
|---|---|---|
| bigint | `product_id` | PK |
| bigint | `category_id` | FK |
| string | `name` |  |
| string | `description` |  |
| string | `status` |  |

## PRODUCT_BUNDLES

| Kiểu | Cột | Khoá |
|---|---|---|
| bigint | `bundle_id` | PK |
| bigint | `category_id` | FK |
| string | `name` |  |
| string | `description` |  |
| string | `status` |  |
| datetime | `created_at` |  |
| datetime | `updated_at` |  |

## PRODUCT_BUNDLE_ITEMS

| Kiểu | Cột | Khoá |
|---|---|---|
| bigint | `bundle_item_id` | PK |
| bigint | `bundle_id` | FK |
| bigint | `product_variant_id` | FK |
| int | `quantity` |  |

## PRODUCT_VARIANTS

| Kiểu | Cột | Khoá |
|---|---|---|
| bigint | `product_variant_id` | PK |
| bigint | `product_id` | FK |
| string | `sku` |  |
| string | `variant_name` |  |
| int | `quantity_on_hand` |  |
| string | `status` |  |

## PRODUCT_VARIANT_IMAGES

| Kiểu | Cột | Khoá |
|---|---|---|
| bigint | `image_id` | PK |
| bigint | `product_variant_id` | FK |
| string | `image_url` |  |
| int | `display_order` |  |
| boolean | `is_primary` |  |

## PROFILES

| Kiểu | Cột | Khoá |
|---|---|---|
| bigint | `profile_id` | PK |
| bigint | `user_id` | FK |
| string | `avatar_url` |  |
| string | `address` |  |
| date | `date_of_birth` |  |
| string | `bio` |  |
| int | `experience_years` |  |
| string | `customer_code` |  |
| string | `staff_code` |  |

## PROMOTIONS

| Kiểu | Cột | Khoá |
|---|---|---|
| bigint | `promotion_id` | PK |
| bigint | `created_by_user_id` | FK |
| string | `name` |  |
| string | `description` |  |
| datetime | `start_at` |  |
| datetime | `end_at` |  |
| string | `status` |  |

## PROMOTION_TIERS

| Kiểu | Cột | Khoá |
|---|---|---|
| bigint | `promotion_tier_id` | PK |
| bigint | `promotion_id` | FK |
| bigint | `gift_product_variant_id` | FK |
| int | `min_quantity` |  |
| int | `max_quantity` |  |
| string | `discount_type` |  |
| decimal | `discount_value` |  |
| int | `gift_quantity` |  |

## REFUND_POLICIES

| Kiểu | Cột | Khoá |
|---|---|---|
| bigint | `refund_policy_id` | PK |
| bigint | `created_by_user_id` | FK |
| string | `policy_type` |  |
| string | `name` |  |
| string | `description` |  |
| string | `status` |  |

## REFUND_POLICY_RULES

| Kiểu | Cột | Khoá |
|---|---|---|
| bigint | `refund_policy_rule_id` | PK |
| bigint | `refund_policy_id` | FK |
| int | `min_hours_before` |  |
| int | `max_hours_before` |  |
| decimal | `refund_percentage` |  |

## RENTAL_ASSET_ASSIGNMENTS

| Kiểu | Cột | Khoá |
|---|---|---|
| bigint | `asset_assignment_id` | PK |
| bigint | `rental_order_item_id` | FK |
| bigint | `equipment_asset_id` | FK |
| datetime | `assigned_at` |  |
| datetime | `returned_at` |  |
| string | `return_condition` |  |
| string | `status` |  |

## RENTAL_ORDERS

| Kiểu | Cột | Khoá |
|---|---|---|
| bigint | `order_id` | PK, FK |
| bigint | `refund_policy_id` | FK |
| bigint | `handled_by_user_id` | FK |
| date | `rental_start_date` |  |
| date | `rental_end_date` |  |
| string | `note` |  |

## RENTAL_ORDER_ITEMS

| Kiểu | Cột | Khoá |
|---|---|---|
| bigint | `rental_order_item_id` | PK |
| bigint | `rental_order_id` | FK |
| bigint | `product_id` | FK |
| int | `quantity` |  |
| decimal | `unit_price` |  |
| decimal | `subtotal` |  |

## RENTAL_PRICE_TIERS

| Kiểu | Cột | Khoá |
|---|---|---|
| bigint | `price_tier_id` | PK |
| bigint | `product_variant_id` | FK |
| bigint | `bundle_id` | FK |
| int | `min_days` |  |
| int | `max_days` |  |
| decimal | `daily_price` |  |

## REVIEWS

| Kiểu | Cột | Khoá |
|---|---|---|
| bigint | `review_id` | PK |
| bigint | `customer_user_id` | FK |
| bigint | `trip_id` | FK |
| int | `rating` |  |
| string | `comment` |  |
| datetime | `created_at` |  |

## ROLES

| Kiểu | Cột | Khoá |
|---|---|---|
| bigint | `role_id` | PK |
| string | `role_name` |  |
| string | `description` |  |
| string | `status` |  |

## SOFT_SKILLS

| Kiểu | Cột | Khoá |
|---|---|---|
| bigint | `soft_skill_id` | PK |
| string | `name` |  |
| string | `description` |  |
| string | `status` |  |

## SYSTEM_CONFIGURATIONS

| Kiểu | Cột | Khoá |
|---|---|---|
| bigint | `configuration_id` | PK |
| bigint | `updated_by_user_id` | FK |
| string | `config_key` |  |
| string | `config_value` |  |
| datetime | `updated_at` |  |

## TAGS

| Kiểu | Cột | Khoá |
|---|---|---|
| bigint | `tag_id` | PK |
| string | `name` |  |
| string | `slug` |  |

## TOURS

| Kiểu | Cột | Khoá |
|---|---|---|
| bigint | `tour_id` | PK |
| bigint | `category_id` | FK |
| bigint | `location_id` | FK |
| bigint | `created_by_user_id (ADMIN)` | FK |
| string | `tour_name` |  |
| string | `description` |  |
| double | `duration_days` |  |
| string | `status` |  |
| datetime | `created_at` |  |
| datetime | `updated_at` |  |

## TOUR_POSTS

| Kiểu | Cột | Khoá |
|---|---|---|
| bigint | `tour_post_id` | PK |
| bigint | `tour_id` | FK |
| bigint | `post_id` | FK |
| int | `display_order` |  |

## TOUR_PREPARATION_ITEMS

| Kiểu | Cột | Khoá |
|---|---|---|
| bigint | `preparation_item_id` | PK |
| bigint | `tour_id` | FK |
| string | `item_name` |  |
| string | `note` |  |

## TOUR_PROVIDED_ITEMS

| Kiểu | Cột | Khoá |
|---|---|---|
| bigint | `tour_provided_item_id` | PK |
| bigint | `tour_id` | FK |
| bigint | `product_variant_id` | FK |
| string | `item_name` |  |
| string | `note` |  |

## TOUR_RECURRENCE_RULES

| Kiểu | Cột | Khoá |
|---|---|---|
| bigint | `recurrence_rule_id` | PK |
| bigint | `tour_id` | FK |
| string | `frequency` |  |
| int | `interval_value` |  |
| date | `start_date` |  |
| date | `end_date` |  |
| boolean | `is_active` |  |

## TRANSACTIONS

| Kiểu | Cột | Khoá |
|---|---|---|
| bigint | `transaction_id` | PK |
| bigint | `order_id` | FK |
| bigint | `original_transaction_id` | FK |
| string | `transaction_type` |  |
| decimal | `amount` |  |
| string | `payment_method` |  |
| string | `provider_transaction_id` |  |
| string | `status` |  |
| datetime | `created_at` |  |

## TRIPS

| Kiểu | Cột | Khoá |
|---|---|---|
| bigint | `trip_id` | PK |
| bigint | `tour_id` | FK |
| bigint | `recurrence_rule_id` | FK |
| bigint | `refund_policy_id` | FK |
| bigint | `managed_by_user_id (STAFF)` | FK |
| datetime | `start_datetime` |  |
| datetime | `end_datetime` |  |
| int | `total_slots` |  |
| string | `status` |  |

## TRIP_ACTIVITIES

| Kiểu | Cột | Khoá |
|---|---|---|
| bigint | `activity_id` | PK |
| bigint | `trip_id` | FK |
| string | `name` |  |
| string | `description` |  |
| datetime | `start_datetime` |  |
| datetime | `end_datetime` |  |
| string | `status` |  |

## TRIP_BOOKING_ORDERS

| Kiểu | Cột | Khoá |
|---|---|---|
| bigint | `order_id` | PK, FK |
| bigint | `trip_id` | FK |
| string | `booking_code` |  |
| string | `note` |  |

## TRIP_GROUPS

| Kiểu | Cột | Khoá |
|---|---|---|
| bigint | `trip_group_id` | PK |
| bigint | `trip_id` | FK |
| bigint | `guide_user_id` | FK |
| string | `group_name` |  |
| int | `max_participants` |  |
| string | `status` |  |

## TRIP_PROVIDED_ITEMS

| Kiểu | Cột | Khoá |
|---|---|---|
| bigint | `trip_provided_item_id` | PK |
| bigint | `trip_id` | FK |
| bigint | `tour_provided_item_id` | FK |
| bigint | `prepared_by_user_id` | FK |
| int | `required_quantity` |  |
| int | `allocated_quantity` |  |
| string | `status` |  |

## USERS

| Kiểu | Cột | Khoá |
|---|---|---|
| bigint | `user_id` | PK |
| bigint | `role_id` | FK |
| string | `email` |  |
| string | `password_hash` |  |
| string | `full_name` |  |
| string | `phone` |  |
| string | `status` |  |
| datetime | `created_at` |  |
| datetime | `updated_at` |  |

## Quan hệ

Cardinality đọc theo ký hiệu chân chim trong file draw.io: đầu trái thuộc bảng cha, đầu phải thuộc bảng con.

| Bảng cha | Card. | Quan hệ | Card. | Bảng con | Ghi chú |
|---|---|---|---|---|---|
| REFRESH_TOKENS 🆕 | 1 | Issues | 1..N | ACCESS_TOKENS 🆕 |  |
| REFRESH_TOKENS 🆕 | 0..1 | Replaced by | 0..1 | REFRESH_TOKENS 🆕 |  |
| USERS | 1 | Owns | 0..N | ACCESS_TOKENS 🆕 |  |
| USERS | 1 | Owns | 0..N | REFRESH_TOKENS 🆕 |  |
| BOOKING_PARTICIPANTS | 1 | Attends | 1..N | ACTIVITY_ATTENDANCES | ⚠ edge chưa gắn vào bảng (chỉ đặt đè vị trí) |
| BOOKING_PARTICIPANTS | 1 | Reads | 1..N | PARTICIPANT_POST_READS |  |
| CATEGORIES | 1 | Categorizes | 1..N | PRODUCTS |  |
| CATEGORIES | 1 | Categorizes | 1..N | PRODUCT_BUNDLES |  |
| CATEGORIES | 1 | Categorizes | 1..N | TOURS |  |
| EQUIPMENT_ASSETS | 1 | Assigned through | 1..N | RENTAL_ASSET_ASSIGNMENTS |  |
| LOCATIONS | 1 | location for | 1..N | TOURS |  |
| ORDERS | 1 | Has | 0..1 | RENTAL_ORDERS |  |
| ORDERS | 1 | Generates | 1..N | TRANSACTIONS |  |
| ORDERS | 1 | Has | 0..1 | TRIP_BOOKING_ORDERS | ⚠ edge chưa gắn vào bảng (chỉ đặt đè vị trí) |
| POSTS | 1 | Read through | 1..N | PARTICIPANT_POST_READS |  |
| POSTS | 1 | Covers | 1..N | POST_SOFT_SKILLS |  |
| POSTS | 1 | Has | 1..N | POST_TAGS |  |
| POSTS | 1 | Used by | 1..N | TOUR_POSTS |  |
| PRODUCTS | 1 | Has | 1..N | PRODUCT_VARIANTS | ⚠ edge chưa gắn vào bảng (chỉ đặt đè vị trí) |
| PRODUCT_BUNDLES | 1 | Contains | 1..N | PRODUCT_BUNDLE_ITEMS |  |
| PRODUCT_BUNDLES | 1 | Rented through | 1..N | RENTAL_ORDER_ITEMS |  |
| PRODUCT_BUNDLES | 1 | Priced by | 1..N | RENTAL_PRICE_TIERS |  |
| PRODUCT_BUNDLE_ITEMS | 1 | Identifies component for | 1..N | RENTAL_ASSET_ASSIGNMENTS |  |
| PRODUCT_VARIANTS | 1 | Has assets | 1..N | EQUIPMENT_ASSETS |  |
| PRODUCT_VARIANTS | 1 | Included in | 1..N | PRODUCT_BUNDLE_ITEMS |  |
| PRODUCT_VARIANTS | 1 | Has | 1..N | PRODUCT_VARIANT_IMAGES |  |
| PRODUCT_VARIANTS | 1 | Gift variant | 1..N | PROMOTION_TIERS |  |
| PRODUCT_VARIANTS | 1 | Rented through | 1..N | RENTAL_ORDER_ITEMS |  |
| PRODUCT_VARIANTS | 1 | Priced by | 1..N | RENTAL_PRICE_TIERS |  |
| PRODUCT_VARIANTS | 1 | Referenced by | 1..N | TOUR_PROVIDED_ITEMS |  |
| PROFILES | 1 | Has | 1 | USERS |  |
| PROMOTIONS | 1 | Applied as | 1..N | APPLIED_PROMOTIONS |  |
| PROMOTIONS | 1 | Has | 1..N | PROMOTION_TIERS |  |
| PROMOTION_TIERS | 1 | Selected by | 1..N | APPLIED_PROMOTIONS |  |
| REFUND_POLICIES | 1 | Contains | 1..N | REFUND_POLICY_RULES | ⚠ edge chưa gắn vào bảng (chỉ đặt đè vị trí) |
| REFUND_POLICIES | 1 | Applies to | 1..N | RENTAL_ORDERS |  |
| REFUND_POLICIES | 1 | Policy for | 1..N | TRIPS |  |
| REFUND_POLICIES | 1 | Applies to | 1..N | TRIP_BOOKING_ORDERS |  |
| REFUND_POLICY_RULES | 1 | Applied to refund | 1..N | TRANSACTIONS |  |
| RENTAL_ORDERS | 1 | Receives | 1..N | APPLIED_PROMOTIONS |  |
| RENTAL_ORDERS | 1 | Includes | 1..N | RENTAL_ORDER_ITEMS |  |
| RENTAL_ORDER_ITEMS | 1 | Assigned assets | 1..N | RENTAL_ASSET_ASSIGNMENTS | ⚠ edge chưa gắn vào bảng (chỉ đặt đè vị trí) |
| ROLES | 1 | Has | 1..N | USERS | ⚠ edge chưa gắn vào bảng (chỉ đặt đè vị trí) |
| SOFT_SKILLS | 1 | Described by | 1..N | POST_SOFT_SKILLS |  |
| TAGS | 1 | Used in | 1..N | POST_TAGS |  |
| TOURS | 1 | Has | 1..N | TOUR_POSTS |  |
| TOURS | 1 | Need Prepare | 1..N | TOUR_PREPARATION_ITEMS |  |
| TOURS | 1 | Provides | 1..N | TOUR_PROVIDED_ITEMS |  |
| TOURS | 1 | Has | 1..N | TOUR_RECURRENCE_RULES |  |
| TOURS | 1 | Has | 1..N | TRIPS |  |
| TOUR_PROVIDED_ITEMS | 1 | defines | 1..N | TRIP_PROVIDED_ITEMS |  |
| TOUR_RECURRENCE_RULES | 1 | Generates | 1..N | TRIPS |  |
| TRANSACTIONS | 1 | Refunded by | 1..N | TRANSACTIONS |  |
| TRIPS | 1 | Receives | 1..N | REVIEWS |  |
| TRIPS | 1 | Includes | 1..N | TRIP_ACTIVITIES |  |
| TRIPS | 1 | Receives | 1..N | TRIP_BOOKING_ORDERS |  |
| TRIPS | 1 | Divided into | 1..N | TRIP_GROUPS |  |
| TRIPS | 1 | provides | 1..N | TRIP_PROVIDED_ITEMS |  |
| TRIP_ACTIVITIES | 1 | Has attendance | 1..N | ACTIVITY_ATTENDANCES |  |
| TRIP_BOOKING_ORDERS | 1 | Receives | 1..N | APPLIED_PROMOTIONS |  |
| TRIP_BOOKING_ORDERS | 1 | Includes | 1..N | BOOKING_PARTICIPANTS |  |
| TRIP_GROUPS | 1 | Includes | 1..N | BOOKING_PARTICIPANTS |  |
| USERS | 1 | Performs | 1..N | AUDIT_LOGS |  |
| USERS | 1 | Linked to | 1..N | BOOKING_PARTICIPANTS |  |
| USERS | 1 | Receives | 1..N | NOTIFICATIONS |  |
| USERS | 1 | Place | 1..N | ORDERS |  |
| USERS | 1 | Creates | 1..N | POSTS |  |
| USERS | 1 | Creates | 1..N | PROMOTIONS |  |
| USERS | 1 | Creates | 1..N | REFUND_POLICIES |  |
| USERS | 1 | Handles | 1..N | RENTAL_ORDERS |  |
| USERS | 1 | Writes | 1..N | REVIEWS |  |
| USERS | 1 | Updates | 1..N | SYSTEM_CONFIGURATIONS |  |
| USERS | 1 | Creates | 1..N | TOURS |  |
| USERS | 1 | Manages | 1..N | TRIPS |  |
| USERS | 1 | Leads | 1..N | TRIP_GROUPS |  |
| USERS | 1 | Prepares | 1..N | TRIP_PROVIDED_ITEMS |  |
