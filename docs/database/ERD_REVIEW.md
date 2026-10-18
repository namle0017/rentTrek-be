# Đánh giá Logical ERD — renTrek

> Ngày: 2026-10-06 · Nguồn: `Conceptual_PRN232.drawio` (trang *Logical ERD*, 44 bảng) · Kèm theo: `Logical_ERD_v2.drawio`, `Logical_ERD_v2.md`
>
> Phạm vi đã chốt với team: **giữ snake_case**; ERD v2 **chỉ thêm 2 bảng token**; mọi đề xuất sửa dưới đây là **khuyến nghị**, chưa áp dụng vào ERD v2. Bảng ACCESS_TOKENS dùng để **thu hồi token tức thì**.

Ký hiệu mức độ: 🔴 Cao (sai dữ liệu / chặn code) · 🟠 Trung bình (rủi ro toàn vẹn, nên sửa trước khi code) · 🟢 Thấp (làm đẹp, nhất quán)

---

## Mục lục

1. [Tóm tắt](#1-tóm-tắt)
2. [Bổ sung ACCESS_TOKENS và REFRESH_TOKENS](#2-bổ-sung-access_tokens-và-refresh_tokens)
3. [Có thật sự "quá nhiều bảng"?](#3-có-thật-sự-quá-nhiều-bảng)
4. [Lỗi trên sơ đồ draw.io](#4-lỗi-trên-sơ-đồ-drawio)
5. [Cột trùng lặp và dữ liệu dư thừa](#5-cột-trùng-lặp-và-dữ-liệu-dư-thừa)
6. [Phát hiện theo từng nhóm bảng](#6-phát-hiện-theo-từng-nhóm-bảng)
7. [Vấn đề xuyên suốt](#7-vấn-đề-xuyên-suốt)
8. [Lộ trình sửa đề xuất](#8-lộ-trình-sửa-đề-xuất)
9. [Câu hỏi cần team chốt](#9-câu-hỏi-cần-team-chốt)
10. [Phương pháp](#10-phương-pháp)

---

## 1. Tóm tắt

**Kết luận chính:** 44 bảng **không phải là vấn đề**. Phạm vi gồm đặt tour, cho thuê đồ, nội dung, thanh toán, khuyến mãi và hoàn tiền; 22 trong số đó là bảng hỗ trợ hợp lệ (lookup, bảng nối, cấu hình, log). Vấn đề thật nằm ở 4 chỗ: **thiếu dữ liệu cốt lõi (giá tour)**, **dữ kiện lưu 2 nơi**, **thiếu ràng buộc (UNIQUE / CHECK / audit)**, và **sơ đồ vẽ sai cardinality / edge chưa gắn bảng**. Gộp tối đa chỉ bớt được 2–3 bảng (§3).

**10 vấn đề cần xử lý trước khi sinh entity:**

| # | Vấn đề | Mức | Mục |
|---|---|---|---|
| 1 | Không có cột giá ở TOURS / TRIPS / TRIP_BOOKING_ORDERS, nên `ORDERS.total_amount` của đơn tour không có nguồn để tính | 🔴 | §6.2 |
| 2 | `RENTAL_ORDER_ITEMS.product_id` sai cấp (giá và asset ở cấp variant), và không có cột để thuê bundle dù đã vẽ edge | 🔴 | §6.3 |
| 3 | 69/72 quan hệ vẽ `1 — 1..N` (bắt buộc ≥1 con). Ví dụ: mỗi user **bắt buộc** có notification, audit log, order… | 🔴 | §4.2 |
| 4 | Kiểu khoá `bigint` (ERD) mâu thuẫn với `BaseEntity.Id : Guid` trong `02-ARCHITECTURE.md` | 🔴 | §7.1 |
| 5 | 0/44 bảng có `is_deleted`, 34/44 bảng không có `created_at`/`updated_at`, trái với `01-CONVENTIONS.md` §1.5 | 🔴 | §7.2 |
| 6 | Thiếu UNIQUE cho mọi khoá nghiệp vụ (email, slug, sku, asset_code, booking_code, config_key, provider_transaction_id, cặp trong bảng nối) | 🔴 | §7.4 |
| 7 | Một asset có thể bị giao cho 2 đơn thuê cùng lúc (RENTAL_ASSET_ASSIGNMENTS không có ràng buộc) | 🔴 | §6.3 |
| 8 | Dữ kiện lưu 2 nơi: `quantity_on_hand`, `APPLIED_PROMOTIONS` (2 FK cùng trỏ ORDERS + `promotion_id` thừa), `POSTS.category`, `subtotal`… | 🟠 | §5 |
| 9 | Chính sách hoàn tiền không được "chụp" (snapshot) lúc đặt; khoảng giờ trong REFUND_POLICY_RULES có thể chồng lấn | 🟠 | §6.4 |
| 10 | 6 edge chưa gắn vào bảng và 4 edge không có cột FK tương ứng | 🟠 | §4.1 |

---

## 2. Bổ sung ACCESS_TOKENS và REFRESH_TOKENS

Đã thêm vào `Logical_ERD_v2.drawio` ở vùng trống phía dưới PROFILES, dùng đúng style bảng và ký hiệu chân chim của sơ đồ gốc.

### 2.1 ACCESS_TOKENS — danh sách access token đang được phép dùng

Mỗi dòng ứng với một JWT đã phát hành. Request chỉ hợp lệ khi tìm thấy `jti`, `revoked_at IS NULL` và `expires_at > now`. Không lưu chuỗi JWT gốc.

| Kiểu | Cột | Khoá | Null | Mô tả |
|---|---|---|---|---|
| bigint | `access_token_id` | PK | NOT NULL | Identity |
| bigint | `user_id` | FK | NOT NULL | → USERS. Dư thừa có chủ đích để thu hồi theo user mà không cần join |
| bigint | `refresh_token_id` | FK | NOT NULL | → REFRESH_TOKENS. Token được phát cùng cặp, dùng cho logout theo phiên và thu hồi theo family |
| string | `jti` | UQ | NOT NULL | Claim `jti` (GUID, 36 ký tự). Middleware tra cứu theo cột này ở mỗi request |
| datetime | `expires_at` | | NOT NULL | Bằng claim `exp`, lưu UTC |
| datetime | `revoked_at` | | NULL | NULL nghĩa là còn hiệu lực |
| string | `revoked_reason` | | NULL | `LOGOUT` / `REUSE_DETECTED` / `USER_LOCKED` / `PASSWORD_CHANGED` |
| datetime | `created_at` | | NOT NULL | Bằng claim `iat` |

### 2.2 REFRESH_TOKENS — có rotation và phát hiện dùng lại (token family)

| Kiểu | Cột | Khoá | Null | Mô tả |
|---|---|---|---|---|
| bigint | `refresh_token_id` | PK | NOT NULL | Identity |
| bigint | `user_id` | FK | NOT NULL | → USERS |
| string | `family_id` | | NOT NULL | GUID sinh một lần khi login; các token rotate từ lần login đó dùng chung giá trị này |
| string | `token_hash` | UQ | NOT NULL | SHA-256 hex (64 ký tự) của token ngẫu nhiên 32 byte. Token gốc chỉ trả cho client một lần |
| datetime | `expires_at` | | NOT NULL | UTC |
| datetime | `created_at` | | NOT NULL | |
| datetime | `revoked_at` | | NULL | Token đã rotate cũng được gán giá trị |
| string | `revoked_reason` | | NULL | `ROTATED` / `LOGOUT` / `REUSE_DETECTED` / `USER_LOCKED` / `PASSWORD_CHANGED` |
| bigint | `replaced_by_refresh_token_id` | FK | NULL | Tự tham chiếu, trỏ tới token kế nhiệm. Khác NULL mà vẫn bị dùng lại thì đó là reuse |

### 2.3 Quan hệ (đã vẽ)

| Cha | | Quan hệ | | Con |
|---|---|---|---|---|
| USERS | 1 | Owns | 0..N | ACCESS_TOKENS |
| USERS | 1 | Owns | 0..N | REFRESH_TOKENS |
| REFRESH_TOKENS | 1 | Issues | 1..N | ACCESS_TOKENS |
| REFRESH_TOKENS | 0..1 | Replaced by | 0..1 | REFRESH_TOKENS |

Mọi FK đặt `ON DELETE NO ACTION`. SQL Server báo lỗi *multiple cascade paths* nếu cascade cả `USERS→ACCESS_TOKENS` lẫn `USERS→REFRESH_TOKENS→ACCESS_TOKENS`.

### 2.4 Index và ràng buộc

- **ACCESS_TOKENS**
  - `UQ_ACCESS_TOKENS_jti (jti) INCLUDE (user_id, expires_at, revoked_at)`: index phủ (covering) cho truy vấn chạy ở mỗi request.
  - `IX_..._user_active (user_id) WHERE revoked_at IS NULL`.
  - `IX_..._refresh_token_id`.
  - `IX_..._expires_at`: phục vụ job dọn dẹp.
- **REFRESH_TOKENS**
  - `UQ_..._token_hash`.
  - `UQ_..._family_active (family_id) WHERE revoked_at IS NULL`: mỗi family chỉ có 1 token còn sống. DB tự chặn khi 2 request refresh chạy đồng thời.
  - `UQ_..._replaced_by (replaced_by_refresh_token_id) WHERE NOT NULL`.
  - `IX_..._user_active`, `IX_..._expires_at`.
- **CHECK**
  - `expires_at > created_at`.
  - `revoked_reason IS NULL OR revoked_at IS NOT NULL`.
  - `replaced_by_refresh_token_id IS NULL OR (revoked_at IS NOT NULL AND replaced_by_refresh_token_id <> refresh_token_id)`.

### 2.5 Luồng xử lý

| Luồng | Xử lý |
|---|---|
| **Login** | Trong 1 transaction: tạo `family_id` mới, INSERT REFRESH_TOKENS (lưu hash), INSERT ACCESS_TOKENS (`jti` mới). Trả token gốc cho client |
| **Gọi API** | Trong `JwtBearerEvents.OnTokenValidated`: tra `jti`; nếu không thấy, đã revoke hoặc hết hạn thì `context.Fail()` và trả 401 |
| **Refresh** | `UPDATE … SET revoked_at, reason='ROTATED' WHERE id=@id AND revoked_at IS NULL`. Nếu 0 dòng bị cập nhật thì coi là reuse. Ngược lại INSERT refresh mới (cùng family) và access mới, rồi gán `replaced_by`. Access token cũ để tự hết hạn, tránh báo nhầm reuse khi user mở nhiều tab |
| **Reuse** | Thu hồi toàn bộ family và các access token thuộc family, ghi AUDIT_LOGS, trả 401 để buộc login lại |
| **Logout** | Lấy `jti` → `refresh_token_id` → `family_id`, thu hồi cả family. Client không cần gửi refresh token |
| **Khoá user / đổi mật khẩu** | `UPDATE` cả 2 bảng `WHERE user_id=@u AND revoked_at IS NULL`. Có hiệu lực ngay ở request kế tiếp |
| **Dọn dẹp** | `BackgroundService` chạy hằng ngày, xoá theo lô. Xoá ACCESS_TOKENS trước; REFRESH_TOKENS xoá theo cả family |

### 2.6 Đánh đổi

- **Lưu hash thay vì token gốc**: lộ DB thì cũng không dùng được token. Chỉ cần SHA-256 vì token đã đủ ngẫu nhiên (256 bit); không dùng BCrypt vì có salt nên không tra cứu trực tiếp được.
- **Chi phí tra DB ở mỗi request**: chỉ là 1 index seek trên bảng hẹp. Có thể thêm `IMemoryCache` TTL 30–60 giây, nhưng khi chạy nhiều instance thì độ trễ thu hồi tối đa bằng TTL. Khuyến nghị: **chưa cache**, chỉ thêm khi đo thấy cần.
- **Gộp 2 bảng thành 1 (SESSIONS)**: khả thi nhưng không nên. Vòng đời khác nhau (15 phút vs nhiều ngày) và tần suất truy cập khác nhau. Giữ 2 bảng đúng yêu cầu.
- **Lưu ý cho 01-CONVENTIONS**: dòng 363 cấm trả `refreshToken` trong response, nên cần ghi rõ ngoại lệ cho `/auth/login` và `/auth/refresh-token`, hoặc chuyển sang cookie HttpOnly (câu hỏi T5).

---

## 3. Có thật sự "quá nhiều bảng"?

### 3.1 Phân loại 44 bảng gốc

| Loại | Số | Bảng |
|---|---|---|
| Lookup / master nhỏ | 5 | ROLES, CATEGORIES, LOCATIONS, TAGS, SOFT_SKILLS |
| Thực thể chính | 9 | USERS, POSTS, TOURS, PRODUCTS, PRODUCT_VARIANTS, PRODUCT_BUNDLES, EQUIPMENT_ASSETS, PROMOTIONS, REFUND_POLICIES |
| Bảng con / mở rộng | 6 | PROFILES, TOUR_PREPARATION_ITEMS, PRODUCT_VARIANT_IMAGES, TRIP_GROUPS, TRIP_ACTIVITIES, RENTAL_ORDER_ITEMS |
| Cấu hình / quy tắc | 5 | TOUR_RECURRENCE_RULES, REFUND_POLICY_RULES, PROMOTION_TIERS, RENTAL_PRICE_TIERS, SYSTEM_CONFIGURATIONS |
| Bảng nối / liên kết | 10 | POST_TAGS, POST_SOFT_SKILLS, TOUR_POSTS, TOUR_PROVIDED_ITEMS, TRIP_PROVIDED_ITEMS, PRODUCT_BUNDLE_ITEMS, PARTICIPANT_POST_READS, ACTIVITY_ATTENDANCES, RENTAL_ASSET_ASSIGNMENTS, APPLIED_PROMOTIONS |
| Giao dịch | 7 | TRIPS, ORDERS, TRIP_BOOKING_ORDERS, RENTAL_ORDERS, BOOKING_PARTICIPANTS, TRANSACTIONS, REVIEWS |
| Log | 2 | NOTIFICATIONS, AUDIT_LOGS |

Chỉ khoảng **20 aggregate root** cần API CRUD riêng; các bảng còn lại được thao tác qua aggregate cha. Việc đã gộp Admin/Staff/Guide/Customer của bản Conceptual thành `USERS + ROLES` là đúng hướng.

### 3.2 Ứng viên gộp

| Ứng viên | Khuyến nghị | Lý do | Phụ thuộc |
|---|---|---|---|
| SOFT_SKILLS + POST_SOFT_SKILLS → TAGS (thêm `tag_type`) + POST_TAGS | ✅ **Gộp nếu** soft skill chỉ là nhãn bài viết (−2 bảng) | Cấu trúc gần giống nhau; 2 bảng nối giống hệt nhau. Bản Conceptual cũng vẽ `SoftSkills → Tags` | Q-C3 |
| PROFILES → USERS | ⚖️ Tuỳ team (−1 bảng) | 1-1, chỉ 7 cột. Nhưng `customer_code`/`staff_code`/`experience_years` là cột thưa theo role | Q-A6 |
| TOUR_PREPARATION_ITEMS + TOUR_PROVIDED_ITEMS → TOUR_ITEMS | ❌ Không gộp | Khác nghiệp vụ: một bên là đồ khách tự mang, một bên là đồ đơn vị cấp (có FK kho và bảng con TRIP_PROVIDED_ITEMS). Gộp sẽ có nhiều NULL và FK con không ép được `item_type` | — |
| TRIP_BOOKING_ORDERS + RENTAL_ORDERS → ORDERS (TPH) | ❌ Giữ TPT như hiện tại | Mỗi bảng con có cột NOT NULL riêng; TPT trong EF Core xử lý được | — |
| REFUND_POLICIES/RULES, PROMOTIONS/TIERS, PRODUCTS/VARIANTS, TOURS/TRIPS | ❌ Không gộp | Vòng đời khác nhau | — |

### 3.3 Bảng có thể phải **thêm** (có điều kiện)

| Bảng | Khi nào cần | Mục |
|---|---|---|
| Tổng quát hoá RENTAL_ASSET_ASSIGNMENTS thành `ASSET_ASSIGNMENTS` (đổi tên, không thêm bảng) | Đồ cấp cho chuyến dùng chung kho với đồ cho thuê | §6.2 |
| `TOUR_ACTIVITY_TEMPLATES` | Lịch trình cố định theo tour, sinh chuyến tự động | §6.2 |
| `RENTAL_CHARGES` | Có tiền cọc hoặc phí phạt hư hỏng/trễ hạn nhiều loại | §6.3 |
| `TOUR_LOCATIONS`, `TOUR_IMAGES`, `TRIP_GROUP_GUIDES` | Tour nhiều điểm / cần ảnh tour / nhóm có nhiều guide | §6.2 |

**Kết luận:** số bảng sau khi sửa dao động 43–48 tuỳ câu trả lời nghiệp vụ. Đừng cắt bảng để giảm số lượng; hãy sửa ràng buộc.

---

## 4. Lỗi trên sơ đồ draw.io

### 4.1 Edge chưa gắn bảng / không khớp cột

**6 edge chưa gắn vào bảng.** Trên hình chúng nằm đúng chỗ (đã kiểm bằng toạ độ), nhưng `source`/`target` rỗng, nên kéo bảng đi là đường nối lệch, và export ra SQL/tool sẽ mất quan hệ. 🟠

| Edge id | Quan hệ trên hình | Thiếu |
|---|---|---|
| `g7UvMp9odqVazCC7gGOr-2354` | ROLES → USERS | cả 2 đầu |
| `DI7Jj1easvfQG83fAV5d-12` | BOOKING_PARTICIPANTS → ACTIVITY_ATTENDANCES | source |
| `DI7Jj1easvfQG83fAV5d-47` | PRODUCTS → PRODUCT_VARIANTS | cả 2 đầu |
| `pb6nDIod9TmFTGB83XVO-35` | RENTAL_ORDER_ITEMS → RENTAL_ASSET_ASSIGNMENTS | cả 2 đầu |
| `qzwZcesLES1b9uYhdJha-2` | REFUND_POLICIES → REFUND_POLICY_RULES | source |
| `tmnQHrdlssFcBae6-_Sy-4` | ORDERS → TRIP_BOOKING_ORDERS | target |

Cách sửa: chọn edge, kéo từng đầu vào bảng cho đến khi viền bảng sáng xanh.

**Edge vẽ nhưng không có cột FK tương ứng:**

| Edge | Vấn đề | Đề xuất | Mức |
|---|---|---|---|
| PRODUCT_VARIANTS → RENTAL_ORDER_ITEMS | Bảng con không có `product_variant_id`, chỉ có `product_id` | Đổi cột (xem §6.3) | 🔴 |
| PRODUCT_BUNDLES → RENTAL_ORDER_ITEMS | Không có cột `bundle_id` | Thêm cột (xem §6.3) | 🔴 |
| PRODUCT_BUNDLE_ITEMS → RENTAL_ASSET_ASSIGNMENTS | Không có FK nào giữa 2 bảng | Xoá edge (asset → variant → bundle item đã suy ra được) | 🟠 |
| REFUND_POLICIES → TRIP_BOOKING_ORDERS | Không có `refund_policy_id` | Vẽ lại thành REFUND_POLICIES → ORDERS (xem §6.4) | 🟠 |
| REFUND_POLICY_RULES → TRANSACTIONS | Không có `refund_policy_rule_id` | Thêm cột nullable nếu cần truy vết rule đã áp, nếu không thì xoá edge (Q-D4) | 🟠 |
| (thiếu) USERS → CATEGORIES | `CATEGORIES.admin_id` là FK nhưng không có edge nào | Vẽ thêm, hoặc bỏ cột (xem §6.1) | 🟢 |

> Trên sơ đồ, nhãn "Refunded by" nằm trong bảng TRANSACTIONS nhưng **không phải cột**. Đó là nhãn của quan hệ tự tham chiếu `original_transaction_id`. Edge này được đặt làm con của bảng TRANSACTIONS nên khi export trông như một dòng. Không cần sửa dữ liệu; nếu muốn gọn, kéo edge ra ngoài bảng.

### 4.2 Cardinality vẽ sai hàng loạt 🔴

Thống kê ký hiệu trên 72 edge gốc:

| Ký hiệu (cha — con) | Số edge | Ý nghĩa |
|---|---|---|
| `ERmandOne — ERoneToMany` | **69** | 1 cha có **ít nhất 1** con (bắt buộc) |
| `ERmandOne — ERzeroToOne` | 2 | 1 — 0..1 (ORDERS → 2 bảng subtype) ✅ |
| `ERmandOne — ERmandOne` | 1 | 1 — 1 (USERS ↔ PROFILES) |

Đầu con là `1..N` gần như luôn sai. Ví dụ:
- USERS → NOTIFICATIONS / AUDIT_LOGS / ORDERS / REVIEWS: user mới tạo chưa có gì.
- TOURS → TRIPS / TOUR_POSTS; TRIPS → REVIEWS; CATEGORIES → PRODUCTS; PROMOTIONS → APPLIED_PROMOTIONS…

**Đề xuất:**
- Đổi đầu con thành `ERzeroToMany` (0..N) cho mọi quan hệ, trừ các quan hệ nghiệp vụ thật sự bắt buộc ≥1. Ứng viên: PRODUCTS → PRODUCT_VARIANTS, ORDERS → RENTAL_ORDER_ITEMS, REFUND_POLICIES → RULES, PROMOTIONS → TIERS. Những quan hệ này cũng chỉ kiểm được ở Service, DB không ép được.
- Với FK **nullable**, đổi đầu cha thành `ERzeroToOne`. Các FK này gồm: `TRIPS.recurrence_rule_id`, `BOOKING_PARTICIPANTS.linked_user_id` / `trip_group_id`, `TRIP_GROUPS.guide_user_id`, `RENTAL_ORDERS.handled_by_user_id`, `TRANSACTIONS.original_transaction_id`, `RENTAL_PRICE_TIERS.product_variant_id` / `bundle_id`, `PROMOTION_TIERS.gift_product_variant_id`, `AUDIT_LOGS.actor_user_id`.
- Riêng `TRANSACTIONS → TRANSACTIONS (Refunded by)` đang vẽ `1 — 1..N`, tức mọi giao dịch bắt buộc có giao dịch hoàn tiền. Nên đổi thành `0..1 — 0..N`.

### 4.3 Khác biệt Conceptual ↔ Logical cần chốt

- POSTS ghi `(ADMIN)`, nhưng Conceptual vẽ `StaffMembers → Posts` (Q-C1).
- Conceptual có `Admins → Products`; Logical không có `PRODUCTS.created_by_user_id`.
- Conceptual có `TripGroups → TripBookingOrders`; Logical đặt `trip_group_id` ở participant (hợp lý hơn, nên sửa Conceptual).
- Conceptual có `RentalOrderItems → Promotions` (khuyến mãi theo dòng); Logical áp theo đơn (Q-D6).

---

## 5. Cột trùng lặp và dữ liệu dư thừa

Phần này trả lời trực tiếp vấn đề "duplicate cột". Phân ra 3 loại: **dư thừa cần bỏ**, **snapshot có chủ đích (giữ, nhưng ghi rõ)**, và **trùng tên nhưng hợp lý**.

### 5.1 Dư thừa: nên bỏ hoặc sửa

| # | Dữ kiện lưu 2 nơi | Rủi ro | Đề xuất | Mức |
|---|---|---|---|---|
| D1 | `PRODUCT_VARIANTS.quantity_on_hand` vs `COUNT(EQUIPMENT_ASSETS)` | Hai nguồn sự thật sẽ lệch khi thêm hoặc thanh lý asset. Tồn kho cho thuê còn phải tính **theo khoảng ngày** | Bỏ cột; tính bằng query/view. Chỉ giữ nếu có hàng không quản lý theo từng chiếc, kèm cờ `is_serialized` (Q-D1) | 🔴 |
| D2 | `APPLIED_PROMOTIONS.booking_order_id` + `rental_order_id` (cả 2 thực chất là `ORDERS.order_id`) | Hai cột nullable cho cùng một khái niệm; có thể không trỏ đơn nào; query phải dùng OR | Thay bằng **một** cột `order_id` NOT NULL → ORDERS | 🔴 |
| D3 | `APPLIED_PROMOTIONS.promotion_id` khi đã có `promotion_tier_id` | Có thể lệch (tier thuộc A, `promotion_id` ghi B) | Bỏ `promotion_id` | 🟠 |
| D4 | `POSTS.category` (string) vs bảng CATEGORIES vs `post_type` vs TAGS vs SOFT_SKILLS | 4–5 cách phân loại một bài viết; chuỗi tự do không thống nhất | Bỏ `category`; dùng `post_type` (enum) + TAGS (Q-C2) | 🔴 |
| D5 | `RENTAL_ORDER_ITEMS.subtotal` = `quantity × unit_price` | Cột dẫn xuất sẽ lệch khi sửa dòng; công thức **bỏ sót số ngày thuê** | Đổi `unit_price` thành `daily_price` (snapshot); bỏ `subtotal`, hoặc đổi tên `line_amount` và ghi rõ là snapshot | 🟠 |
| D6 | `TRIPS.tour_id` vs `TOUR_RECURRENCE_RULES.tour_id` (qua `recurrence_rule_id`) | Trip có thể trỏ tour A nhưng rule của tour B | Giữ `tour_id` (trip tạo tay không có rule); thêm FK ghép `(recurrence_rule_id, tour_id)` → UNIQUE trên TOUR_RECURRENCE_RULES | 🟠 |
| D7 | `TOUR_PROVIDED_ITEMS.item_name` khi đã có `product_variant_id` | Tên lệch với kho | `item_name` chỉ dùng cho đồ ngoài kho; CHECK `product_variant_id IS NOT NULL OR item_name IS NOT NULL` | 🟢 |
| D8 | `TRIP_BOOKING_ORDERS.note` + `RENTAL_ORDERS.note`; `booking_code` chỉ có ở đơn tour | Trùng giữa 2 subtype; đơn thuê không có mã tra cứu | Chuyển lên `ORDERS.note`, `ORDERS.order_code` (UNIQUE) | 🟠 |
| D9 | `TRIPS.refund_policy_id` + `RENTAL_ORDERS.refund_policy_id` | Hai chỗ cho cùng ý nghĩa "chính sách áp cho đơn"; đơn tour không được snapshot | `ORDERS.refund_policy_id` (snapshot lúc đặt); `TRIPS.refund_policy_id` chỉ là mặc định (§6.4) | 🟠 |
| D10 | `EQUIPMENT_ASSETS.asset_status = 'Rented'` vs assignment chưa trả | Trạng thái "đang cho thuê" có 2 nguồn | `asset_status` chỉ lưu tình trạng vật lý (Available/Maintenance/Retired/Lost); "đang thuê" suy ra từ assignment | 🟢 |
| D11 | `NOTIFICATIONS.is_read`, `PARTICIPANT_POST_READS.is_acknowledged` | Boolean làm mất thời điểm | Đổi thành `read_at` / `acknowledged_at` (NULL = chưa) | 🟢 |
| D12 | `PROFILES.customer_code` + `staff_code` | Luôn có 1 cột NULL; Guide không có mã | Gộp thành `user_code` (UNIQUE), hoặc bỏ hẳn nếu chỉ để hiển thị (Q-A7) | 🟠 |
| D13 | `PROFILES.experience_years` | Giá trị suy ra theo thời gian, sẽ cũ đi theo năm | Đổi thành `career_start_date`, hoặc chấp nhận cập nhật tay | 🟢 |

### 5.2 Snapshot có chủ đích: giữ, nhưng phải ghi rõ

| Cột | Vì sao giữ |
|---|---|
| `BOOKING_PARTICIPANTS.full_name / email / phone` (trùng USERS khi `linked_user_id` có giá trị) | Participant có thể không có tài khoản; tên trên danh sách/bảo hiểm cần cố định tại thời điểm đặt. **Đề xuất:** `full_name` NOT NULL (snapshot), `email`/`phone` nullable, chỉ điền khi không có tài khoản (Q-B6) |
| `APPLIED_PROMOTIONS.discount_amount`, `gift_quantity` | Số tiền thực tế đã giảm; tier có thể bị sửa sau. Nên thêm `gift_product_variant_id` snapshot |
| `ORDERS.total_amount` | Số tiền đã chốt với khách. Nên thêm `subtotal_amount`, `discount_amount` để đối soát được |
| `ACCESS_TOKENS.user_id` | Thu hồi theo user không cần join (§2.6) |
| `TRIPS.end_datetime` vs `TOURS.duration_days` | `duration_days` là giá trị mặc định; `end_datetime` là thực tế của chuyến |
| `TRIPS.total_slots` vs `Σ TRIP_GROUPS.max_participants` | Khác nghĩa: sức chứa bán ra và sức chứa từng nhóm. Kiểm `Σ max_participants ≥ total_slots` ở Service |

### 5.3 Trùng tên nhưng hợp lý (pattern chung)

`status` (19 bảng), `description` (9), `name` (8), `created_at` (9), `note` (4), `display_order` (2), `slug` (2), `address` (2): đây là pattern, **không phải** duplicate. Vấn đề của nhóm này là **đặt tên không đồng nhất** (§7.3) và **kiểu dữ liệu** (§7.5).

### 5.4 Đánh giá chuẩn hoá

- **1NF**: đạt, trừ `POSTS.category` (giá trị tự do), `AUDIT_LOGS.details`, `SYSTEM_CONFIGURATIONS.config_value` (đa kiểu) và `address` (một chuỗi gộp).
- **2NF**: đạt, vì mọi bảng dùng khoá đại diện. Rủi ro chuyển sang chỗ khoá nghiệp vụ không có UNIQUE (§7.4).
- **3NF**: vi phạm tập trung ở D1, D2, D3, D5, D6.

---

## 6. Phát hiện theo từng nhóm bảng

### 6.1 Định danh và nội dung

| Bảng | Phát hiện | Đề xuất | Mức |
|---|---|---|---|
| USERS | Thiếu UNIQUE `email`, thiếu `is_deleted` | Filtered UNIQUE `(email) WHERE is_deleted = 0`; chuẩn hoá lowercase; `status` (Active/Locked/…) tách biệt với `is_deleted` | 🔴 |
| USERS | Chưa có `email_confirmed_at`, `last_login_at` | Thêm nếu có xác thực email (Q-A5) | 🟢 |
| ROLES | `status` vô nghĩa với 4 role seed cố định; thiếu UNIQUE `role_name` | Bỏ `status`; UNIQUE `role_name` | 🟠 |
| PROFILES | `user_id` không UNIQUE nên thành 1-N, trong khi sơ đồ vẽ 1-1 | UNIQUE `(user_id)`, hoặc dùng `user_id` làm PK+FK | 🔴 |
| PROFILES | Trộn cột của nhiều role (D12, D13); thiếu audit | Xem §5.1; nếu Guide có nhiều thuộc tính riêng thì tách `GUIDE_PROFILES` (Q-A7) | 🟠 |
| POSTS | `category` dư thừa (D4); thiếu UNIQUE `slug`; thiếu `updated_at`, `is_deleted` | Bỏ `category`; filtered UNIQUE `slug`; CHECK `status <> 'Published' OR published_at IS NOT NULL` | 🔴 |
| POSTS | `(ADMIN)` mâu thuẫn với Conceptual (Staff) | Bỏ chú thích khỏi tên cột; kiểm quyền bằng policy (Q-C1) | 🟠 |
| TAGS / SOFT_SKILLS | Thiếu UNIQUE `name`/`slug`, thiếu audit; hai bảng gần giống nhau | UNIQUE; gộp theo §3.2 (Q-C3) | 🟠 |
| POST_TAGS / POST_SOFT_SKILLS | Surrogate PK cho phép gắn trùng tag | UNIQUE `(post_id, tag_id)` (xem §7.4) | 🔴 |
| TOUR_POSTS | Thiếu UNIQUE cặp; PARTICIPANT_POST_READS ngầm hiểu có "bài bắt buộc đọc" nhưng không có chỗ đánh dấu | UNIQUE `(tour_id, post_id)`; thêm `is_required` (Q-C4) | 🟠 |
| PARTICIPANT_POST_READS | Đọc lại sẽ sinh dòng trùng | UNIQUE `(participant_id, post_id)`; `acknowledged_at` thay cho `is_acknowledged` | 🔴 |
| CATEGORIES | Dùng chung cho TOURS + PRODUCTS + PRODUCT_BUNDLES mà không có cột phân loại, nên tour có thể gắn danh mục "Lều" | Thêm `category_type` (TOUR/PRODUCT) + UNIQUE `(category_type, category_name)`, hoặc tách 2 bảng (Q-C5) | 🔴 |
| CATEGORIES | `admin_id` lệch quy ước (`created_by_user_id`), không có edge | Đổi tên + vẽ edge, hoặc bỏ và dùng AUDIT_LOGS | 🟠 |
| LOCATIONS | Tour chỉ có 1 location (điểm đi hay điểm đến?); `lat/long` chưa có precision | `decimal(9,6)` + CHECK phạm vi; `TOUR_LOCATIONS` nếu tour nhiều điểm (Q-B8) | 🟠 |
| REVIEWS | Không gắn booking nên **review khống** được (chưa đi vẫn review); không UNIQUE nên review trùng | Thêm `booking_order_id` (hoặc `participant_id`) + UNIQUE; Service kiểm trip đã hoàn thành (Q-C6) | 🔴 |
| REVIEWS | `rating` không giới hạn | CHECK `rating BETWEEN 1 AND 5` | 🟠 |
| NOTIFICATIONS | Không biết thông báo trỏ tới đối tượng nào | Thêm `related_entity_type`, `related_entity_id`; index `(recipient_user_id, read_at, created_at)` | 🟠 |
| AUDIT_LOGS | `details` là string không cấu trúc | `nvarchar(max)` JSON + CHECK `ISJSON`, hoặc `old_values`/`new_values`; `actor_user_id` nullable (job hệ thống). Bảng chỉ ghi thêm, không có `is_deleted` | 🟠 |
| SYSTEM_CONFIGURATIONS | Thiếu UNIQUE `config_key`, nên đọc ra giá trị ngẫu nhiên khi trùng key | UNIQUE `config_key`; thêm `value_type`, `description`; không lưu secret | 🔴 |

### 6.2 Tour và chuyến đi

| Bảng | Phát hiện | Đề xuất | Mức |
|---|---|---|---|
| TOURS / TRIPS / TRIP_BOOKING_ORDERS | **Không có cột giá nào** trong cả luồng đặt tour | `TOURS.base_price` (mặc định) → `TRIPS.price_per_person` (giá thực của chuyến) → `TRIP_BOOKING_ORDERS.participant_count`, `unit_price` (snapshot). Nếu có giá người lớn/trẻ em thì đặt giá ở từng participant (Q-B1) | 🔴 |
| TRIPS / TRIP_GROUPS | Sức chứa nằm ở 2 nơi; slot còn trống không có công thức; nguy cơ overbooking khi 2 người đặt cùng lúc | Slot còn trống = `total_slots − COUNT(participant hiệu lực)`, không lưu thành cột; thêm `rowversion` hoặc khoá `UPDLOCK` khi đặt (Q-B2) | 🔴 |
| TRIP_PROVIDED_ITEMS | `allocated_quantity` là số nhập tay, không truy được tới EQUIPMENT_ASSETS, nên 1 lều có thể vừa cho thuê vừa cấp cho chuyến | Tổng quát hoá RENTAL_ASSET_ASSIGNMENTS thành `ASSET_ASSIGNMENTS` (thêm `trip_provided_item_id`, CHECK đúng 1 nguồn). Chỉ cần 1 chỗ để kiểm asset bận (Q-B4) | 🔴 |
| TRIPS | `tour_id` có thể lệch rule (D6); thiếu CHECK thời gian | FK ghép; CHECK `end_datetime > start_datetime`; filtered UNIQUE `(recurrence_rule_id, start_datetime)` chống job sinh trùng chuyến | 🟠 |
| TOURS | `duration_days` kiểu `double` (sai số làm tròn) | `int` (+ `duration_nights int`), hoặc `duration_minutes` nếu có tour nửa ngày (Q-B9) | 🟠 |
| TOUR_RECURRENCE_RULES | Không đủ dữ liệu để sinh chuyến (giờ khởi hành, thứ trong tuần, sức chứa, giá mặc định) | Thêm `start_time`, `days_of_week`, `default_total_slots`, `default_price`; CHECK `interval_value ≥ 1`. Nếu chuyến luôn tạo tay thì có thể bỏ bảng (Q-B3) | 🟠 |
| TOUR_PROVIDED_ITEMS | Không có số lượng mẫu nên `required_quantity` không có nguồn | Thêm `quantity_per_participant`; D7 | 🟠 |
| BOOKING_PARTICIPANTS | Participant có thể nằm ở nhóm của **trip khác** với booking; cùng 1 user có thể bị thêm 2 lần vào một chuyến | Thêm `trip_id` + FK ghép tới TRIP_BOOKING_ORDERS và TRIP_GROUPS; filtered UNIQUE `(trip_id, linked_user_id)` (hoặc kiểm ở Service) | 🟠 |
| TRIP_GROUPS | Thiếu UNIQUE `(trip_id, group_name)`; chưa rõ `guide_user_id` có nullable không, và mỗi nhóm có nhiều guide không | UNIQUE; CHECK `max_participants > 0` (Q-B7) | 🟠 |
| TRIP_ACTIVITIES | Lịch trình lặp lại ở mọi chuyến phải nhập lại; trang tour không có lịch trình để hiển thị | Thêm `TOUR_ACTIVITY_TEMPLATES` để copy sang khi tạo chuyến (Q-B5) | 🟠 |
| ACTIVITY_ATTENDANCES | Điểm danh trùng; không ghi người điểm danh | UNIQUE `(activity_id, participant_id)`; thêm `checked_by_user_id` | 🔴 |
| TRIP_BOOKING_ORDERS | `booking_code` không UNIQUE | Chuyển lên `ORDERS.order_code` UNIQUE (D8) | 🔴 |
| TOUR_PREPARATION_ITEMS | — | Giữ riêng (§3.2); thêm `is_mandatory`, UNIQUE `(tour_id, item_name)` | 🟢 |

### 6.3 Sản phẩm, cho thuê và đơn hàng

| Bảng | Phát hiện | Đề xuất | Mức |
|---|---|---|---|
| RENTAL_ORDER_ITEMS | `product_id` sai cấp: giá (RENTAL_PRICE_TIERS) và asset đều ở cấp variant. Bundle không thuê được | `product_id` → `product_variant_id` (nullable) + `bundle_id` (nullable) + **CHECK XOR**; CHECK `quantity > 0` (Q-D2) | 🔴 |
| RENTAL_ASSET_ASSIGNMENTS | Một asset có thể có 2 assignment chưa trả | Filtered UNIQUE `(equipment_asset_id) WHERE returned_at IS NULL`; CHECK `(status='Returned') = (returned_at IS NOT NULL)`; `return_condition` dạng enum | 🔴 |
| RENTAL_PRICE_TIERS | Có cả `product_variant_id` lẫn `bundle_id` mà không có CHECK; khoảng ngày có thể chồng lấn | CHECK XOR; CHECK `max_days IS NULL OR max_days ≥ min_days`; filtered UNIQUE theo `(variant, min_days)` và `(bundle, min_days)` | 🔴 |
| PRODUCT_VARIANTS | D1; thiếu UNIQUE `sku` | Bỏ `quantity_on_hand`; UNIQUE `sku`, `(product_id, variant_name)`; thêm `size`/`color` nếu FE cần lọc (Q-D3) | 🔴 |
| PRODUCT_VARIANT_IMAGES | `is_primary` không ràng buộc (0 hoặc nhiều ảnh chính); ảnh nhân bản ở mọi variant | Filtered UNIQUE `(product_variant_id) WHERE is_primary = 1`; cân nhắc đổi thành `PRODUCT_IMAGES` (thêm `product_id`, variant nullable) (Q-D3) | 🟠 |
| EQUIPMENT_ASSETS | Thiếu UNIQUE `asset_code`; D10 | UNIQUE; chỉ thanh lý bằng `Retired`, không xoá cứng | 🟠 |
| PRODUCT_BUNDLE_ITEMS | Cùng variant có thể lặp nhiều dòng; `quantity ≤ 0` vẫn hợp lệ | UNIQUE `(bundle_id, product_variant_id)`; CHECK `quantity > 0` | 🟠 |
| PRODUCTS | Thiếu audit, thiếu người tạo (Conceptual có `Admins → Products`) | Thêm `created_at`, `updated_at`, `is_deleted`, `created_by_user_id` | 🟢 |
| ORDERS | Chỉ có `total_amount`, không đối soát được với khuyến mãi | Thêm `subtotal_amount`, `discount_amount`; CHECK `total = subtotal − discount` | 🔴 |
| ORDERS | `order_type` có thể lệch với bảng subtype; `status` gộp vòng đời đơn và thanh toán; thiếu thời điểm huỷ (cần để tính hoàn tiền) | CHECK `order_type`; thêm `cancelled_at`, `cancel_reason`; cân nhắc tách `payment_status` (Q-D7) | 🟠 |
| RENTAL_ORDERS | Không có CHECK ngày; lúc khách đặt chưa có nhân viên xử lý, nhưng ERD không ghi `handled_by_user_id` có được NULL không | CHECK `rental_end_date ≥ rental_start_date`; khai báo rõ `handled_by_user_id` nullable | 🟠 |
| RENTAL_ORDERS | Không có tiền cọc, phí trễ hạn hay phí hư hỏng | `deposit_amount` và/hoặc bảng `RENTAL_CHARGES` (Q-D5) | 🟠 |
| TRANSACTIONS | `provider_transaction_id` không UNIQUE, nên webhook gọi lại sẽ ghi trùng giao dịch | Filtered UNIQUE `(payment_method, provider_transaction_id)`; CHECK `amount > 0`; CHECK `(transaction_type='Refund') = (original_transaction_id IS NOT NULL)`; thêm `processed_at` | 🔴 |

### 6.4 Hoàn tiền và khuyến mãi

| Bảng | Phát hiện | Đề xuất | Mức |
|---|---|---|---|
| REFUND_POLICIES | Đơn tour không giữ chính sách đã áp; admin sửa `TRIPS.refund_policy_id` thì điều kiện hoàn của đơn cũ đổi theo | `ORDERS.refund_policy_id` (snapshot), bỏ ở RENTAL_ORDERS; policy đã được dùng thì chỉ tạo phiên bản mới, không sửa tại chỗ (Q-D4) | 🟠 |
| REFUND_POLICY_RULES | `min/max_hours_before` có thể chồng lấn hoặc để hở; `refund_percentage` không giới hạn | Dùng **một ngưỡng** `min_hours_before` + UNIQUE `(policy, min_hours_before)`. Rule áp dụng là rule có ngưỡng lớn nhất mà vẫn ≤ số giờ còn lại, nên không thể chồng lấn. CHECK `0..100` | 🔴 |
| REFUND_POLICY_RULES | Đơn thuê chỉ có `rental_start_date` (kiểu date), nên "số giờ trước" không xác định được | Chốt mốc giờ cho đơn thuê, ví dụ 00:00 hoặc giờ mở cửa (Q-D4) | 🔴 |
| PROMOTIONS | Không rõ áp cho trip, đơn thuê hay cả hai; không có mã khuyến mãi, giới hạn lượt dùng hay quy tắc cộng dồn | Thêm theo nghiệp vụ: `applies_to`, `promotion_code`, `max_uses`, `is_stackable`; CHECK `end_at > start_at` (Q-D6) | 🟠 |
| PROMOTION_TIERS | `discount_type` / `discount_value` / `gift_*` không ràng buộc chéo; chưa rõ `min/max_quantity` đếm gì | CHECK theo `discount_type` (Percent: 0..100, `gift` NULL; Gift: `gift_*` NOT NULL…); dùng một ngưỡng như REFUND_POLICY_RULES | 🟠 |
| APPLIED_PROMOTIONS | D2, D3 | `order_id` duy nhất; bỏ `promotion_id`; UNIQUE `(order_id, promotion_tier_id)`; thêm snapshot `gift_product_variant_id` | 🔴 |

---

## 7. Vấn đề xuyên suốt

### 7.1 Kiểu khoá: `bigint` hay `Guid` 🔴

ERD dùng `bigint` cho cả 112 cột khoá. `02-ARCHITECTURE.md` định nghĩa `BaseEntity.Id : Guid = Guid.NewGuid()` và `IGenericRepository.GetByIdAsync(Guid)`. **Phải chốt trước khi sinh entity** (Q-A1).
- `bigint`: index nhỏ và tuần tự, dễ đọc; nhưng lộ số lượng bản ghi qua API, và phải sửa lại BaseEntity/Repository.
- `Guid`: khớp kiến trúc hiện tại, không đoán được id; nên dùng `NEWSEQUENTIALID()` hoặc Guid v7 để tránh phân mảnh index.

Ngoài ra, hai bảng có PK chung với ORDERS (TRIP_BOOKING_ORDERS, RENTAL_ORDERS) không dùng được `Id = Guid.NewGuid()` của BaseEntity; cần map theo kiểu TPT.

### 7.2 Cột audit và soft delete 🔴

| | Hiện trạng |
|---|---|
| `created_at` | 9/44 bảng |
| `updated_at` | 5/44 bảng |
| `is_deleted` | **0/44** bảng |
| Người tạo | 5 bảng, đặt tên không đồng nhất (`created_by_user_id`, `admin_id`) |

`01-CONVENTIONS.md` §1.5 yêu cầu mọi entity nghiệp vụ có đủ 3 cột, và `GenericRepository.Remove` gán `IsDeleted`. **Đề xuất bộ cột chuẩn:**

| Nhóm bảng | Bộ cột |
|---|---|
| Thực thể nghiệp vụ, master, lookup | `created_at`, `updated_at`, `is_deleted` |
| Chỉ ghi thêm (AUDIT_LOGS, TRANSACTIONS, APPLIED_PROMOTIONS, 2 bảng token) | Chỉ `created_at`, **không** có `is_deleted`. Ghi ngoại lệ vào CONVENTIONS và dùng base class riêng (ví dụ `ImmutableEntity`) |
| Bảng nối thuần (POST_TAGS…) | `created_at`; xoá cứng theo bảng cha |

Bật soft delete thì **mọi UNIQUE phải là filtered** (`WHERE is_deleted = 0`), nếu không sẽ không tạo lại được bản ghi đã xoá mềm.

### 7.3 Đặt tên (vẫn giữ snake_case) 🟠

- **11 PK không theo dạng `<bảng số ít>_id`**: `image_id`, `activity_id`, `attendance_id`, `participant_id`, `recurrence_rule_id`, `preparation_item_id`, `asset_assignment_id`, `configuration_id`, `bundle_item_id`, `bundle_id`, `price_tier_id`. Đổi cho đúng, hoặc liệt kê thành ngoại lệ trong tài liệu.
- **FK khác tên PK đích**: `booking_order_id`, `rental_order_id` trỏ tới `order_id`.
- **Chú thích nằm trong tên cột**: `created_by_user_id (ADMIN)`, `managed_by_user_id (STAFF)`. Ràng buộc role thuộc Service, không thuộc tên cột.
- **`name` vs `xxx_name`**: `name` ở 8 bảng, còn `role_name`, `category_name`, `tour_name`, `group_name`, `item_name`, `variant_name` ở 7 bảng. Nên thống nhất dùng `name`.
- **`status` vs `xxx_status` vs `is_active`**: `attendance_status`, `asset_status` và `is_active` (TOUR_RECURRENCE_RULES) nên quy về `status`.
- **Thời gian**: `start_datetime` (TRIPS) vs `start_at` (PROMOTIONS) vs `start_date`. Quy ước: `*_at` cho datetime, `*_date` cho date.
- **Mapping EF Core**: dùng `UseSnakeCaseNamingConvention()` thì `Id` sẽ map thành `id`, không ra `user_id`. Muốn ra `user_id` phải cấu hình `HasColumnName` cho từng PK (Q-A2).

### 7.4 UNIQUE và bảng nối 🔴

ERD hiện **không khai báo UNIQUE nào**. Danh sách tối thiểu cần thêm:

`USERS.email` · `ROLES.role_name` · `PROFILES.user_id` · `POSTS.slug` · `TAGS.name/slug` · `SOFT_SKILLS.name` · `CATEGORIES(category_type, name)` · `PRODUCT_VARIANTS.sku` · `EQUIPMENT_ASSETS.asset_code` · `ORDERS.order_code` (hoặc `booking_code`) · `TRANSACTIONS(payment_method, provider_transaction_id)` · `SYSTEM_CONFIGURATIONS.config_key` · `REVIEWS(booking_order_id)` · `ACCESS_TOKENS.jti` · `REFRESH_TOKENS.token_hash`

Bảng nối: `POST_TAGS(post_id, tag_id)` · `POST_SOFT_SKILLS(post_id, soft_skill_id)` · `TOUR_POSTS(tour_id, post_id)` · `PARTICIPANT_POST_READS(participant_id, post_id)` · `ACTIVITY_ATTENDANCES(activity_id, participant_id)` · `PRODUCT_BUNDLE_ITEMS(bundle_id, product_variant_id)` · `TRIP_PROVIDED_ITEMS(trip_id, tour_provided_item_id)` · `TRIP_GROUPS(trip_id, group_name)`

**Chốt về PK của bảng nối** (hai sub-agent đề xuất khác nhau): **giữ surrogate PK + thêm UNIQUE composite**. Lý do: `GenericRepository<TEntity>` dựa trên một `Id` duy nhất. Chỉ POST_TAGS/POST_SOFT_SKILLS có thể dùng composite PK, và chỉ khi team chọn many-to-many ẩn (skip navigation) của EF Core (Q-A3).

### 7.5 Kiểu dữ liệu 🟠

| Vấn đề | Đề xuất |
|---|---|
| 32 cột trạng thái/loại kiểu string tự do | `varchar(30)` + `CHECK (... IN (...))`, map bằng `HasConversion<string>()`. **Không** tạo 20+ bảng lookup. Cần danh sách giá trị cho từng cột (Q-A4) |
| Tiền dùng `decimal` không có precision (EF Core sẽ cảnh báo) | **Chốt `decimal(18,0)`** cho số tiền VND (VND không có đơn vị lẻ, khớp với cổng thanh toán); làm tròn ở Service khi tính %. Phần trăm dùng `decimal(5,2)` + CHECK `0..100`. Hai sub-agent đề xuất khác nhau (18,2) và (18,0); mình chọn (18,0), team có thể đổi (Q-A8) |
| `datetime` | `datetime2(3)` lưu UTC (CONVENTIONS quy định ISO 8601 UTC) |
| `TOURS.duration_days double` | `int` (xem §6.2) |
| `latitude`/`longitude` | `decimal(9,6)` + CHECK phạm vi |
| `string` không có độ dài | `nvarchar(max)` cho content/details; `nvarchar(2000)` cho description/comment; `varchar(20..100)` cho mã, phone, slug, sku |
| ERD không ghi NULL/DEFAULT | Bổ sung data dictionary (cột Null như §2.1) cho mọi bảng |

---

## 8. Lộ trình sửa đề xuất

| Bước | Việc | Phụ thuộc câu hỏi |
|---|---|---|
| 1 | Chốt kiểu khoá, BaseEntity cho bảng nối/log, precision tiền, quy ước tên PK | Q-A1 → A4, A8 |
| 2 | Sửa sơ đồ: gắn 6 edge treo, sửa cardinality 0..N / 0..1, xoá hoặc vẽ lại 4 edge không có cột | Q-D2, D4 |
| 3 | Sửa 🔴 không phụ thuộc nghiệp vụ: UNIQUE (§7.4), audit (§7.2), D2/D3 APPLIED_PROMOTIONS, RENTAL_ASSET_ASSIGNMENTS, TRANSACTIONS, CHECK XOR | — |
| 4 | Sửa 🔴 phụ thuộc nghiệp vụ: giá tour, RENTAL_ORDER_ITEMS, CATEGORIES, REVIEWS, `quantity_on_hand`, REFUND_POLICY_RULES | Q-B1, D1, D2, C5, C6, D4 |
| 5 | Gộp (nếu chọn): SOFT_SKILLS → TAGS, PROFILES → USERS | Q-C3, A6 |
| 6 | Đồng bộ lại Conceptual (trang 2) và cập nhật `01-CONVENTIONS.md` (snake_case, ngoại lệ bảng chỉ ghi thêm, ngoại lệ trả refresh token) | — |

---

## 9. Câu hỏi cần team chốt

Đã gộp trùng lặp từ 5 báo cáo. Câu **in đậm** chặn việc sinh entity.

**A. Kỹ thuật chung**
- **A1. Khoá chính dùng `bigint` (theo ERD) hay `Guid` (theo BaseEntity)?**
- **A2. Tên cột vật lý là `user_id` (cần cấu hình `HasColumnName` cho từng PK) hay `id`? Có cập nhật `01-CONVENTIONS.md` §1.5 sang snake_case không?**
- **A3. Mọi bảng (kể cả bảng nối và log) đều kế thừa BaseEntity, hay cho phép base nhẹ hơn? Many-to-many dùng skip navigation hay entity riêng?**
- A4. Danh sách giá trị cho 32 cột status/type (status của từng bảng, `post_type`, `payment_method`, `transaction_type`, `frequency`…)?
- A5. Có xác thực email, quên mật khẩu, đăng nhập Google/Facebook không?
- A6. PROFILES giữ riêng 1-1 hay gộp vào USERS?
- A7. `customer_code`/`staff_code` dùng để làm gì? Guide có cần mã riêng không? Guide có thuộc tính riêng nào (chứng chỉ, ngôn ngữ…)?
- A8. Tiền VND lưu `decimal(18,0)` (đề xuất) hay `(18,2)`? Có chuyến hoặc địa điểm ngoài múi giờ Việt Nam không?
- A9. Một user có thể giữ nhiều role cùng lúc (ví dụ Staff kiêm Guide) không?

**B. Tour và chuyến**
- **B1. Giá tour đặt ở đâu và tính thế nào: theo người hay theo đơn? Có giá người lớn/trẻ em không? Có tiền cọc không? Có giá theo mùa không?**
- B2. Nhóm (TRIP_GROUPS) được tạo trước khi bán hay chia sau khi khách đặt? Đơn chưa thanh toán có giữ chỗ không, giữ bao lâu? Chuyến có số khách tối thiểu không?
- B3. Có chuyến tạo tay (không qua rule) không? Rule có tự sinh chuyến không, sinh trước bao xa?
- B4. Đồ cấp cho chuyến có lấy từ cùng kho EQUIPMENT_ASSETS với đồ cho thuê không? Có món ngoài kho (nước, đồ ăn) không?
- B5. Lịch trình cố định theo tour hay mỗi chuyến tự lập? Hoạt động áp cho cả chuyến hay theo từng nhóm?
- B6. Participant có bắt buộc có tài khoản không? Hồ sơ tối thiểu cần gì (ngày sinh, CCCD, liên hệ khẩn cấp, sức khoẻ)?
- B7. Nhóm có thể chưa có guide không? Một nhóm có nhiều guide không?
- B8. `LOCATIONS` là điểm xuất phát hay điểm đến? Tour có nhiều điểm không? Có cần ảnh tour không?
- B9. Có tour nửa ngày hoặc dưới 1 ngày không? Có hiển thị "N ngày M đêm" không?

**C. Nội dung và danh mục**
- C1. Ai được tạo bài viết: Admin (theo Logical) hay Staff (theo Conceptual)?
- C2. `POSTS.category` khác gì `post_type` và TAGS? Bỏ được không?
- C3. Soft skill chỉ là nhãn bài viết (gộp vào TAGS được) hay có nghiệp vụ riêng?
- C4. Có bài bắt buộc đọc/xác nhận trước chuyến đi không?
- C5. Danh mục tour và danh mục sản phẩm dùng chung một cây hay hai bộ riêng? Có danh mục cha-con không?
- C6. Ai được review: người đặt hay từng participant? Điều kiện: trip đã hoàn thành, đã thanh toán? Có kiểm duyệt review không?

**D. Thuê đồ, thanh toán và khuyến mãi**
- **D1. Mọi variant đều quản lý theo từng asset không (để bỏ `quantity_on_hand`)?**
- **D2. Dòng đơn thuê trỏ tới variant, bundle hay cả hai? Bundle tính giá cả gói hay cộng từng món? Khách có chọn size cho từng món trong bundle không?**
- D3. Variant khác nhau ở size hay màu? Ảnh gắn theo product hay variant? FE có cần lọc theo size/màu không?
- D4. Chính sách hoàn tiền áp theo thời điểm đặt (snapshot) hay thời điểm huỷ? Admin có sửa policy đang được dùng không? Mốc giờ của đơn thuê là mấy giờ? Có cần lưu rule đã áp vào TRANSACTIONS không?
- D5. Có tiền cọc, phí trễ hạn, phí hư hỏng/mất đồ không?
- D6. Khuyến mãi áp theo đơn hay theo dòng? Áp cho trip, đơn thuê hay cả hai? Có mã nhập, giới hạn lượt dùng, cộng dồn không? `min/max_quantity` đếm gì (số món, số người hay số ngày)? Quà tặng có phải đồ cho thuê (cần gán asset) không?
- D7. Máy trạng thái của đơn tour và đơn thuê? Có tách `payment_status` không? Có thanh toán từng phần không? Có khách thuê tại quầy không có tài khoản không?
- D8. Danh sách phương thức và cổng thanh toán? Asset được gán lúc đặt hay lúc khách đến nhận đồ?

**T. Token**
- T1. Thời hạn: access token 15 phút, refresh token 7–30 ngày? Refresh token hết hạn kiểu trượt (sliding) hay tuyệt đối (absolute)?
- T2. Có cho thời gian ân hạn (grace window) khi refresh đồng thời (nhiều tab) không? Đề xuất: không, coi là reuse.
- T3. Đổi mật khẩu có đăng xuất cả phiên hiện tại không? Admin đổi role có thu hồi token của user đó không (vì role nằm trong JWT)?
- T4. Có cần endpoint "đăng xuất mọi thiết bị" hoặc giới hạn số phiên mỗi user không?
- T5. Refresh token trả trong response body hay cookie HttpOnly?
- T6. Giữ token đã hết hạn/thu hồi bao lâu trước khi xoá (đề xuất 30 ngày)? Có cache `jti` không?

---

## 10. Phương pháp

- **Phân công**: 5 sub-agent làm song song: (1) thiết kế bảng token; (2) nhóm định danh và nội dung (16 bảng); (3) nhóm tour và chuyến (11 bảng); (4) nhóm sản phẩm, thuê, đơn hàng, khuyến mãi (17 bảng); (5) review xuyên suốt (FK ↔ edge, cột trùng, audit, đặt tên, kiểu dữ liệu, đếm bằng script).
- **Kiểm chứng của agent chính** trên file `.drawio` gốc (sub-agent chỉ đọc bản JSON, đã mất style):
  - 6 edge treo: xác nhận bằng toạ độ, không còn là suy đoán.
  - "Refunded by" là nhãn quan hệ, **không phải** cột lạc. Đã loại bỏ phát hiện mức 🔴 của sub-agent.
  - Cardinality 69/72 là `1..N`: phát hiện mới, sub-agent không thấy được.
  - Mâu thuẫn `Guid` trong `02-ARCHITECTURE.md`: đã xác nhận.
- **Điểm sub-agent đề xuất khác nhau và quyết định của agent chính**:
  - Precision tiền: chọn `(18,0)`.
  - PK bảng nối: chọn surrogate + UNIQUE.
  - REFRESH_TOKENS: dùng bản thiết kế chi tiết của agent (1) thay cho bản rút gọn của agent (2).
- **File kèm theo**:
  - `Logical_ERD_v2.drawio`: bản sao file gốc, thêm 2 bảng token; trang Conceptual giữ nguyên từng byte.
  - `Logical_ERD_v2.md`: bản text sinh tự động từ `.drawio` (46 bảng, 76 quan hệ, có cardinality).
