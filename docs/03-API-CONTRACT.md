# 03 — API Contract

> Hợp đồng API giữa Backend và Frontend của **renTrek**.
> Cấu trúc request/response chung tuân theo [`01-CONVENTIONS.md`](01-CONVENTIONS.md#4-cấu-trúc-response).
> Mỗi khi thêm / sửa API phải cập nhật file này và ghi vào [Changelog](#changelog).

| Thông tin | Giá trị |
|---|---|
| Version | `v1` |
| Base URL (Local) | `https://localhost:<port>/api/v1` |
| Base URL (Staging) | `TODO` |
| Base URL (Production) | `TODO` |
| Swagger | `https://localhost:<port>/swagger` |
| Authentication | JWT Bearer — header `Authorization: Bearer {accessToken}` |
| Content-Type | `application/json` |

## Mục lục

- [Quy ước chung](#quy-ước-chung)
- [Danh sách Endpoint](#danh-sách-endpoint)
- [1. Auth](#1-auth)
- [2. Users](#2-users)
- [3. Trip Bookings](#3-trip-bookings)
- [5. Products](#5-products)
- [6. Product Bundles](#6-product-bundles)
- [7. Rental Orders](#7-rental-orders)
- [Phụ lục: Enum](#phụ-lục-enum)
- [Phụ lục: Mã lỗi](#phụ-lục-mã-lỗi)
- [Template endpoint](#template-endpoint)
- [Changelog](#changelog)

---

## Quy ước chung

**Response wrapper** (áp dụng cho mọi API):

```json
{
  "success": true,
  "statusCode": 200,
  "message": "string",
  "data": {},
  "errors": null,
  "timestamp": "2026-10-06T08:30:00Z"
}
```

**Paged data** (`data` của API danh sách):

```json
{
  "items": [],
  "pageNumber": 1,
  "pageSize": 10,
  "totalItems": 0,
  "totalPages": 0,
  "hasPrevious": false,
  "hasNext": false
}
```

**Query chung cho API danh sách:** `pageNumber`, `pageSize`, `search`, `sortBy`, `sortOrder`.

**Ký hiệu cột Auth:** `Public` = không cần token · `User` = đã đăng nhập · `Admin` / `TODO_Role` = cần role tương ứng.

---

## Danh sách Endpoint

| # | Module | Method | Endpoint | Auth | Mô tả | Trạng thái |
|---|---|---|---|---|---|---|
| 1.1 | Auth | `POST` | `/auth/register` | Public | Đăng ký | ⬜ TODO |
| 1.2 | Auth | `POST` | `/auth/login` | Public | Đăng nhập | ⬜ TODO |
| 1.3 | Auth | `POST` | `/auth/refresh-token` | Public | Làm mới access token | ⬜ TODO |
| 1.4 | Auth | `POST` | `/auth/logout` | User | Đăng xuất | ⬜ TODO |
| 2.1 | Users | `GET` | `/users/me` | User | Lấy thông tin cá nhân | ⬜ TODO |
| 2.2 | Users | `PUT` | `/users/me` | User | Cập nhật thông tin cá nhân | ⬜ TODO |
| 2.3 | Users | `GET` | `/users` | Admin | Danh sách user | ⬜ TODO |
| 2.4 | Users | `GET` | `/users/me/orders` | User | Đơn hàng của tôi (đơn thuê và booking chuyến đi) | 🟨 In progress |
| 3.1 | Trip Bookings | `POST` | `/trip-bookings` | User | Tạo booking và giữ chỗ | ✅ Done |
| 3.2 | Trip Bookings | `GET` | `/trip-bookings/{orderId}` | User | Xem booking theo phạm vi quyền | ✅ Done |
| 3.3 | Trip Bookings | `POST` | `/trip-bookings/{orderId}/participant-invitations` | User | Tạo lại lời mời sau thanh toán | ✅ Done |
| 3.4 | Trip Bookings | `POST` | `/trip-bookings/participant-invitations/claim` | User | Nhận vị trí participant bằng token | ✅ Done |
| 4.1 | Tours | `GET` | `/tours` | Public | Lấy danh sách tour kèm bộ lọc & phân trang | ✅ Done |
| 4.2 | Tours | `GET` | `/tours/{id}` | Public | Lấy thông tin chi tiết một tour | ✅ Done |
| 5.1 | Products | `GET` | `/products` | Public | Danh sách sản phẩm thuê / bán | ✅ Done |
| 5.2 | Products | `GET` | `/products/{id}` | Public | Chi tiết sản phẩm, biến thể, ảnh, giá | ✅ Done |
| 5.3 | Products | `GET` | `/products/categories` | Public | Danh mục lọc sản phẩm | ✅ Done |
| 6.1 | Product Bundles | `GET` | `/bundles` | Public | Danh sách combo thiết bị, kèm khả dụng theo khoảng thời gian | 🟨 In progress |
| 6.2 | Product Bundles | `GET` | `/bundles/{bundleId}` | Public | Chi tiết combo, thành phần, bậc giá, khả dụng | 🟨 In progress |
| 7.1 | Rental Orders | `POST` | `/rental-availability` | Public | Kiểm tra khả dụng nhiều dòng | 🟨 In progress |
| 7.2 | Rental Orders | `POST` | `/rental-orders/quote` | User | Báo giá đơn thuê | 🟨 In progress |
| 7.3 | Rental Orders | `POST` | `/rental-orders` | Customer | Tạo đơn thuê và giữ chỗ (Idempotency-Key) | 🟨 In progress |
| 7.4 | Rental Orders | `GET` | `/rental-orders/{orderId}` | Customer | Chi tiết đơn thuê của tôi | 🟨 In progress |

> Trạng thái: ⬜ TODO · 🟨 In progress · ✅ Done · 🔁 Changed

---

## 1. Auth

### 1.1. Đăng ký

`POST /auth/register` · **Auth:** Public

**Request body**

| Field | Type | Required | Validation | Mô tả |
|---|---|---|---|---|
| `TODO` | `string` | ✔ | TODO | TODO |

```json
{
  "TODO": "TODO"
}
```

**Response `201 Created`**

```json
{
  "success": true,
  "statusCode": 201,
  "message": "TODO",
  "data": {
    "TODO": "TODO"
  },
  "errors": null,
  "timestamp": "..."
}
```

**Lỗi**

| Status | Khi nào | Message |
|---|---|---|
| 400 | TODO | TODO |
| 409 | TODO | TODO |

---

### 1.2. Đăng nhập

`POST /auth/login` · **Auth:** Public

**Request body**

| Field | Type | Required | Validation | Mô tả |
|---|---|---|---|---|
| `TODO` | `string` | ✔ | TODO | TODO |

```json
{
  "TODO": "TODO"
}
```

**Response `200 OK`**

```json
{
  "success": true,
  "statusCode": 200,
  "message": "TODO",
  "data": {
    "TODO": "TODO"
  },
  "errors": null,
  "timestamp": "..."
}
```

**Lỗi**

| Status | Khi nào | Message |
|---|---|---|
| 400 | TODO | TODO |
| 401 | TODO | TODO |

---

### 1.3. Làm mới token

`POST /auth/refresh-token` · **Auth:** Public

**Request body**

| Field | Type | Required | Validation | Mô tả |
|---|---|---|---|---|
| `TODO` | `string` | ✔ | TODO | TODO |

**Response `200 OK`** — TODO

**Lỗi**

| Status | Khi nào | Message |
|---|---|---|
| 401 | TODO | TODO |

---

### 1.4. Đăng xuất

`POST /auth/logout` · **Auth:** User

**Request body** — TODO

**Response `200 OK`** — TODO

**Lỗi**

| Status | Khi nào | Message |
|---|---|---|
| 401 | TODO | TODO |

---

## 2. Users

### 2.1. Lấy thông tin cá nhân

`GET /users/me` · **Auth:** User

**Response `200 OK`**

```json
{
  "success": true,
  "statusCode": 200,
  "message": "TODO",
  "data": {
    "TODO": "TODO"
  },
  "errors": null,
  "timestamp": "..."
}
```

**Lỗi**

| Status | Khi nào | Message |
|---|---|---|
| 401 | TODO | TODO |

---

### 2.2. Cập nhật thông tin cá nhân

`PUT /users/me` · **Auth:** User

**Request body**

| Field | Type | Required | Validation | Mô tả |
|---|---|---|---|---|
| `TODO` | `string` | ✔ | TODO | TODO |

**Response `200 OK`** — TODO

**Lỗi**

| Status | Khi nào | Message |
|---|---|---|
| 400 | TODO | TODO |
| 401 | TODO | TODO |

---

### 2.3. Danh sách user

`GET /users` · **Auth:** Admin

**Query params**

| Param | Type | Required | Default | Mô tả |
|---|---|---|---|---|
| `pageNumber` | `int` | ✘ | `1` | |
| `pageSize` | `int` | ✘ | `10` | |
| `search` | `string` | ✘ | | TODO |
| `TODO` | `TODO` | ✘ | | TODO |

**Response `200 OK`** — `data` là Paged data, mỗi item:

```json
{
  "TODO": "TODO"
}
```

**Lỗi**

| Status | Khi nào | Message |
|---|---|---|
| 401 | TODO | TODO |
| 403 | TODO | TODO |

---

### 2.4. Đơn hàng của tôi

`GET /users/me/orders` · **Auth:** User

Danh sách đơn của user đang đăng nhập, gồm cả đơn thuê (`Rental`) và booking chuyến đi (`TripBooking`). Chỉ trả đơn của chính user. `totalPayable` luôn bằng `totalAmount` (không có cọc).

**Query params**

| Param | Type | Required | Default | Mô tả |
|---|---|---|---|---|
| `pageNumber` | `int` | ✘ | `1` | Nhỏ hơn 1 về 1 |
| `pageSize` | `int` | ✘ | `10` | Tối đa 100, nhỏ hơn 1 về 10 |
| `search` | `string` | ✘ | | Tối đa 200 ký tự, tìm theo `orderCode` (không phân biệt hoa thường) |
| `orderType` | `string` | ✘ | | `Rental` hoặc `TripBooking` (không phân biệt hoa thường) |
| `status` | `string` | ✘ | | Tối đa 50 ký tự, khớp chính xác [`OrderStatus`](#phụ-lục-enum) |
| `sortBy` | `string` | ✘ | `createdAt` | `createdAt`, `serviceStartAt` |
| `sortOrder` | `string` | ✘ | `desc` | `asc`, `desc` |

**Response `200 OK`** — `data` là Paged data (message `Lấy danh sách đơn hàng thành công`), mỗi item:

```json
{
  "id": "6f1c2a9e-0000-0000-0000-0000000000f1",
  "orderCode": "RT-20261027-3F9A1C0B7D2E4A5B6C8D",
  "orderType": "Rental",
  "status": "PendingPayment",
  "paymentStatus": "Unpaid",
  "totalAmount": 540000,
  "totalPayable": 540000,
  "serviceStartAt": "2026-11-01T01:00:00+00:00",
  "serviceEndAt": "2026-11-04T01:00:00+00:00",
  "title": "Lều cắm trại 2 người — 2 người / xanh lá +1",
  "holdExpiresAt": "2026-10-27T10:15:00+00:00",
  "createdAt": "2026-10-27T10:00:00+00:00"
}
```

- `title`: đơn thuê = tên dòng đầu tiên, thêm ` +N` nếu còn N dòng khác (mặc định `Thuê thiết bị`); booking chuyến đi = tên tour (mặc định `Chuyến đi`).
- `serviceStartAt` / `serviceEndAt`: thời điểm nhận / trả (đơn thuê) hoặc giờ khởi hành / kết thúc chuyến; có thể `null`.
- `holdExpiresAt`: hạn giữ chỗ khi chưa thanh toán; `null` khi không còn giữ chỗ.

**Lỗi**

| Status | Khi nào | `errors.code` | Message |
|---|---|---|---|
| 400 | Query sai (`orderType`, `sortBy`, `sortOrder`, `search` quá dài, trang vượt giới hạn) | — (lỗi theo field) | Dữ liệu không hợp lệ |
| 401 | Chưa đăng nhập / token hết hạn | — | — |

---

## 3. Trip Bookings

Người tạo booking phải có tài khoản đang hoạt động và luôn là participant đầu tiên. `additionalParticipants` chỉ chứa người đi cùng. Giá lấy từ trip trên server. Booking giữ chỗ 15 phút khi chưa thanh toán.

### 3.1. Tạo booking chuyến đi

`POST /trip-bookings` · **Auth:** User

**Header:** `Idempotency-Key` bắt buộc, tối đa 100 ký tự. Cùng user và cùng payload trả lại booking cũ. Dùng cùng key cho payload khác trả `409`.

**Request body**

```json
{
  "tripId": "00000000-0000-0000-0000-000000000000",
  "note": "Đi cùng nhóm bạn",
  "additionalParticipants": [
    {
      "fullName": "Nguyễn Văn B",
      "email": "b@example.com",
      "phone": "0900000000",
      "dateOfBirth": "2000-01-01",
      "emergencyContactName": "Nguyễn Văn C",
      "emergencyContactPhone": "0911111111"
    }
  ]
}
```

- Tối đa 20 người đi cùng.
- Email được `trim` và chuyển chữ thường, không trùng trong cùng booking.
- Buyer tự lấy từ JWT/account, không nhập lại trong request.
- Kiểm tra trạng thái trip và sức chứa trong transaction có khóa hàng.

**Response:** `201 Created`, `data` là `TripBookingResponse`.

**Lỗi:** `400` dữ liệu/key sai; `403` account không hoạt động; `404` không có account/trip; `409` hết chỗ, trip đóng, email trùng, hoặc key dùng cho payload khác.

### 3.2. Xem booking

`GET /trip-bookings/{orderId}` · **Auth:** User

- Buyer nhận toàn bộ tiền, payment status, hold và participants; `canManage = true`.
- Participant đã liên kết chỉ nhận trip, trạng thái booking và participant của chính mình. Các trường tiền bằng `0`, `paymentStatus` rỗng, `holdExpiresAt = null`, `canManage = false`.
- User ngoài booking nhận `404` để không lộ booking tồn tại.

**Response `200 OK`**

```json
{
  "success": true,
  "statusCode": 200,
  "message": "Lấy booking thành công",
  "data": {
    "orderId": "00000000-0000-0000-0000-000000000000",
    "orderCode": "RT-20261026-ABC",
    "tripId": "00000000-0000-0000-0000-000000000000",
    "tripStartAt": "2026-12-01T00:00:00Z",
    "tripEndAt": "2026-12-03T00:00:00Z",
    "tripStatus": "Open",
    "participantCount": 2,
    "unitPrice": 1200000,
    "subtotalAmount": 2400000,
    "totalAmount": 2400000,
    "status": "PendingPayment",
    "paymentStatus": "Unpaid",
    "holdExpiresAt": "2026-10-26T10:15:00Z",
    "canManage": true,
    "participants": []
  },
  "errors": null,
  "timestamp": "..."
}
```

### 3.3. Tạo lời mời participant

`POST /trip-bookings/{orderId}/participant-invitations` · **Auth:** Buyer

Chỉ buyer được gọi sau khi order `Confirmed`, payment `Paid`, và tổng payment `Succeeded` đủ tiền. Lời mời cũ chưa dùng bị thu hồi. Mỗi participant chưa liên kết nhận token dùng một lần. Token hết hạn sau 7 ngày hoặc lúc trip bắt đầu, tùy mốc nào đến trước. API chỉ trả token thô lúc tạo; DB chỉ lưu SHA-256.

**Response `200 OK`:** `data` là mảng `{ participantId, fullName, email, token, expiresAt }`.

**Lỗi:** `404` không phải booking của buyer; `409` chưa thanh toán đủ hoặc trip đã bắt đầu.

### 3.4. Nhận lời mời participant

`POST /trip-bookings/participant-invitations/claim` · **Auth:** User

```json
{
  "token": "one-time-token"
}
```

Account phải đang hoạt động. Email account sau chuẩn hóa phải trùng email participant. Token phải chưa dùng, chưa thu hồi và chưa hết hạn. Thành công sẽ gắn `linkedUserId` và tiêu thụ token trong cùng transaction.

**Response:** `200 OK`, `data` là booking ở phạm vi participant.

**Lỗi:** `400` thiếu token; `403` account không hoạt động hoặc email không khớp; `404` token không tồn tại; `409` token vô hiệu hoặc booking chưa đủ điều kiện.

## 5. Products

Chỉ đọc danh mục công khai. Không thay đổi API tạo / sửa / xoá sản phẩm hoặc auth.
Chỉ trả sản phẩm, danh mục, biến thể có `status = Active`, chưa soft delete,
và biến thể có ít nhất một bậc giá thuê. Sản phẩm không có biến thể đủ điều kiện không xuất hiện.
Ảnh và bảng giá đã soft delete không xuất hiện.

**Mô hình:** bảng Supabase categories → products → product_variants → product_variant_images / rental_price_tiers.
ID là UUID. Chỉ cho thuê (PRD §2.5: bán đứt nằm ngoài phạm vi). Không triển khai bundle, đơn hàng
hoặc kiểm tra tồn kho theo ngày trong task này. Giá là số VND.
`pricePerDay` là mức giá thuê thấp nhất trong các bậc giá và biến thể công khai.
Đây là giá **từ**, không phải báo giá cho số ngày thuê cụ thể; `isRentable` không khẳng định còn hàng.
Ngày giờ trả ISO 8601 UTC. FE gọi qua Axios instance dùng chung và React Query.

### 5.1. Danh sách sản phẩm

`GET /products` · **Auth:** Public

| Query | Kiểu | Mặc định | Validation / ý nghĩa |
|---|---|---|---|
| `pageNumber` | int | 1 | Dùng PagingRequest chung: nhỏ hơn 1 về 1; offset vượt Int32 trả 400 |
| `pageSize` | int | 10 | Dùng PagingRequest chung: tối đa 100, nhỏ hơn 1 về 10 |
| `search` | string | null | Tối đa 200 ký tự, trim, tìm tên hoặc mô tả |
| `categoryId` | uuid? | null | UUID danh mục |
| `sortBy` | string | createdAt | `createdAt`, `name`, `pricePerDay` |
| `sortOrder` | string | desc | `asc`, `desc` |

Sắp xếp ổn định theo ID khi bằng giá trị; giá null luôn xếp cuối.
Trang vượt tổng số trang trả items rỗng. Dữ liệu rỗng trả 200, không trả 404.
Query sai trả 400 + `errors` theo field.

**Response 200:** `ApiResponse<PagedResult<ProductResponse>>`, ví dụ `data`:

```json
{
  "items": [{
    "id": "6f1c2a9e-0000-0000-0000-000000000001",
    "name": "Lều cắm trại 2 người",
    "categoryId": "6f1c2a9e-0000-0000-0000-0000000000c1",
    "categoryName": "Lều cắm trại",
    "thumbnailUrl": "/images/products/tent.svg",
    "isRentable": true,
    "pricePerDay": 90000,
    "createdAt": "2026-10-23T00:00:00Z"
  }],
  "pageNumber": 1,
  "pageSize": 10,
  "totalItems": 1,
  "totalPages": 1,
  "hasPrevious": false,
  "hasNext": false
}
```

Thumbnail: ảnh primary trước, sau đó displayOrder, ID; null nếu chưa có ảnh.

### 5.2. Chi tiết sản phẩm

`GET /products/{id}` · **Auth:** Public · `id`: uuid

**Response 200:** `ApiResponse<ProductDetailResponse>` gồm toàn bộ field ProductResponse và:

```json
{
  "description": "Lều chống nước, phù hợp cho hai người.",
  "variants": [{
    "id": "6f1c2a9e-0000-0000-0000-0000000000a1",
    "sku": "TENT-2-GREEN",
    "name": "2 người / xanh lá",
    "size": "2 người",
    "color": "Xanh lá",
    "images": [{ "id": 1, "imageUrl": "/images/products/tent.svg", "isPrimary": true, "displayOrder": 0 }],
    "rentalPriceTiers": [{ "id": 1, "minDays": 1, "maxDays": 3, "dailyPrice": 120000 }]
  }]
}
```

Biến thể được sắp theo ID, bậc giá theo minDays rồi ID. `maxDays = null` nghĩa là không giới hạn trên.
ID không tồn tại hoặc sản phẩm không công khai trả 404 với wrapper chuẩn.
ID không phải UUID không khớp route nên cũng trả 404.

### 5.3. Danh mục sản phẩm

`GET /products/categories` · **Auth:** Public

**Response 200:** `ApiResponse<IReadOnlyList<ProductCategoryResponse>>`;
`data = [{ "id": "6f1c2a9e-0000-0000-0000-0000000000c1", "name": "Lều cắm trại" }]`, sắp theo tên rồi ID.
Chỉ trả danh mục Active, chưa xoá và có sản phẩm công khai đủ điều kiện.

---

## 6. Product Bundles

Combo thiết bị cho thuê (một combo gồm nhiều biến thể sản phẩm với số lượng cố định mỗi combo). Chỉ đọc, **Public**. Chỉ trả combo `Active`, chưa soft delete. Giá combo là bậc giá thuê riêng của combo, không cộng dồn từ thành phần. Giá là số VND.

Khả dụng chỉ được tính khi truyền **cả** `from` và `to` (ISO 8601 UTC); thiếu một trong hai trả `400 INVALID_RENTAL_PERIOD`. Quy tắc khoảng thời gian xem [7.0](#70-quy-tắc-chung-của-luồng-thuê). Số bộ combo khả dụng = min theo từng thành phần của `floor(tồn khả dụng của biến thể / quantity mỗi combo)`; kết quả chỉ để tham khảo, được kiểm tra lại khi tạo đơn.

### 6.1. Danh sách combo

`GET /bundles` · **Auth:** Public

**Query params**

| Param | Type | Required | Default | Mô tả |
|---|---|---|---|---|
| `pageNumber` | `int` | ✘ | `1` | Nhỏ hơn 1 về 1; offset vượt Int32 trả 400 |
| `pageSize` | `int` | ✘ | `12` | Tối đa 100, nhỏ hơn 1 về 10 |
| `search` | `string` | ✘ | | Tối đa 200 ký tự, tìm theo tên combo hoặc tên sản phẩm / biến thể / SKU thành phần |
| `categoryId` | `guid` | ✘ | | Không được là `Guid.Empty` |
| `sortBy` | `string` | ✘ | `name` | `createdAt`, `name`, `priceFrom` |
| `sortOrder` | `string` | ✘ | `asc` | `asc`, `desc` |
| `from` | `datetime` | ✘ | | Bắt đầu thuê (UTC); phải đi cùng `to` |
| `to` | `datetime` | ✘ | | Kết thúc thuê (UTC); phải đi cùng `from` |
| `onlyAvailable` | `bool` | ✘ | `false` | `true` chỉ trả combo còn ít nhất 1 bộ trong `from`–`to`; **chỉ có tác dụng khi có `from` và `to`** |

**Response `200 OK`** — `data` là Paged data, mỗi item `ProductBundleResponse`:

```json
{
  "success": true,
  "statusCode": 200,
  "message": "Lấy danh sách combo thành công",
  "data": {
    "items": [
      {
        "id": "6f1c2a9e-0000-0000-0000-0000000000b1",
        "name": "Combo cắm trại 2 người",
        "category": { "id": "6f1c2a9e-0000-0000-0000-0000000000c1", "name": "Lều cắm trại" },
        "componentSummary": "Lều 2 người x1, Túi ngủ x2",
        "coverImageUrl": "/images/products/tent.svg",
        "priceFrom": 150000,
        "availability": {
          "from": "2026-11-01T01:00:00Z",
          "to": "2026-11-04T01:00:00Z",
          "availableQuantity": 3,
          "isAvailable": true
        }
      }
    ],
    "pageNumber": 1,
    "pageSize": 12,
    "totalItems": 1,
    "totalPages": 1,
    "hasPrevious": false,
    "hasNext": false
  },
  "errors": null,
  "timestamp": "2026-10-27T08:30:00Z"
}
```

- `priceFrom`: mức giá ngày thấp nhất trong các bậc giá của combo (giá **từ**).
- `availability`: `null` khi không truyền `from`/`to`. Trong danh sách không có `limitingComponent`.

**Lỗi**

| Status | Khi nào | `errors.code` | Message |
|---|---|---|---|
| 400 | Query sai (`sortBy`, `sortOrder`, `search`, `categoryId`) | — (lỗi theo field) | Dữ liệu không hợp lệ |
| 400 | Chỉ truyền một trong `from`/`to`; `to` <= `from`; `from` ở quá khứ; thuê quá 60 ngày | `INVALID_RENTAL_PERIOD` | Cần truyền đủ cả from và to / … |

### 6.2. Chi tiết combo

`GET /bundles/{bundleId}` · **Auth:** Public

**Path params**

| Param | Type | Mô tả |
|---|---|---|
| `bundleId` | `guid` | Id combo (không phải guid thì không khớp route, trả 404 mặc định) |

**Query params:** `from`, `to` (tùy chọn, quy tắc như [6.1](#61-danh-sách-combo)).

**Response `200 OK`**

```json
{
  "success": true,
  "statusCode": 200,
  "message": "Lấy combo thành công",
  "data": {
    "id": "6f1c2a9e-0000-0000-0000-0000000000b1",
    "name": "Combo cắm trại 2 người",
    "description": "Bộ lều và túi ngủ cho hai người.",
    "status": "Active",
    "components": [
      {
        "productVariantId": "6f1c2a9e-0000-0000-0000-0000000000a1",
        "productName": "Lều cắm trại 2 người",
        "variantName": "2 người / xanh lá",
        "sku": "TENT-2-GREEN",
        "quantity": 1,
        "primaryImageUrl": "/images/products/tent.svg",
        "availableQuantity": 3
      }
    ],
    "priceTiers": [
      { "id": 1, "minDays": 1, "maxDays": 3, "dailyPrice": 150000 },
      { "id": 2, "minDays": 4, "maxDays": null, "dailyPrice": 130000 }
    ],
    "availability": {
      "from": "2026-11-01T01:00:00Z",
      "to": "2026-11-04T01:00:00Z",
      "availableQuantity": 3,
      "isAvailable": true,
      "limitingComponent": "Lều cắm trại 2 người"
    }
  },
  "errors": null,
  "timestamp": "2026-10-27T08:30:00Z"
}
```

- `components[].quantity`: số lượng biến thể trong **một** combo.
- `components[].availableQuantity` và `availability`: `null` khi không truyền `from`/`to`. `availableQuantity` là tồn khả dụng của biến thể (trước khi chia cho `quantity`).
- `availability.limitingComponent`: tên sản phẩm thành phần giới hạn số bộ (bỏ khỏi JSON khi không có).
- `priceTiers` sắp theo `minDays`; `maxDays = null` là không giới hạn trên.

**Lỗi**

| Status | Khi nào | `errors.code` | Message |
|---|---|---|---|
| 400 | Chỉ truyền một trong `from`/`to`, hoặc khoảng thời gian không hợp lệ | `INVALID_RENTAL_PERIOD` | … |
| 404 | Combo không tồn tại hoặc ngừng kinh doanh | `BUNDLE_NOT_FOUND` | Combo không tồn tại hoặc đã ngừng kinh doanh |

---

## 7. Rental Orders

Luồng đặt thuê thiết bị: kiểm tra khả dụng → báo giá → tạo đơn → xem chi tiết đơn. Thanh toán, hủy đơn và hoàn tiền **chưa** thuộc phạm vi các endpoint dưới đây.

### 7.0. Quy tắc chung của luồng thuê

- **Mọi số tiền do server tính**; FE chỉ gửi biến thể / combo, số lượng, thời gian, mã khuyến mãi. Giá lấy từ bậc giá (`minDays`–`maxDays`) khớp với `billableDays`; `lineAmount = quantity × billableDays × dailyPrice`.
- **Không thu cọc:** `depositAmount` luôn `0`, `totalPayable = totalAmount = subtotalAmount − discountAmount`.
- **Khoảng thời gian thuê:** ISO 8601 UTC. `rentalEndAt` phải sau `rentalStartAt`; `rentalStartAt` không được ở quá khứ (cho phép lệch 5 phút); tối đa **60** ngày tính phí. `billableDays = ceil((rentalEndAt − rentalStartAt) / 24h)`, tối thiểu 1. Vi phạm trả `400 INVALID_RENTAL_PERIOD`.
- **Giỏ thuê (`items`):** 1–20 dòng; mỗi dòng có **đúng một** trong `productVariantId` hoặc `bundleId`; `quantity` từ 1 đến 20. Sai hình dạng dòng trả `400 INVALID_RENTAL_ITEM`; biến thể / combo không tồn tại hoặc ngừng kinh doanh trả `404`.
- **Khả dụng theo khoảng thời gian** (không theo tổng tồn kho): tài sản (`equipment_assets`) đang cho thuê được tính là rảnh khi không có phân bổ `Reserved` / `Issued` chồng lấn với `[rentalStartAt − cleaningBufferHours, rentalEndAt + cleaningBufferHours]`. **Cleaning buffer** (giờ vệ sinh giữa hai lượt thuê) lấy từ `system_configurations` key `rental.cleaningBufferHours` (mặc định `2`). Nhu cầu của các dòng cùng biến thể (kể cả thành phần combo) được cộng gộp trước khi so với tồn.
- **Giữ chỗ (hold):** đơn mới ở `PendingPayment` / `Unpaid`; `holdExpiresAt = now + holdMinutes`, `holdMinutes` lấy từ `system_configurations` key `rental.holdMinutes` (mặc định `15`). Tài sản được phân bổ (`Reserved`) ngay lúc tạo đơn.
- **Mã đơn:** `orderCode` dạng `RT-yyyyMMdd-<20 ký tự hex in hoa>`, ví dụ `RT-20261027-3F9A1C0B7D2E4A5B6C8D`.
- **Chính sách hoàn tiền:** báo giá trả chính sách thuê đang hiệu lực (`refundPolicy.version`); khi tạo đơn FE gửi lại `acceptedRefundPolicyVersion`, phải khớp phiên bản hiện hành.
- **Mã lỗi nghiệp vụ** nằm ở `errors.code` (mảng 1 phần tử), xem [Phụ lục: Mã lỗi](#phụ-lục-mã-lỗi). Lỗi validation theo field (400) chỉ có key theo tên field (camelCase), **không** có `code`.

### 7.1. Kiểm tra khả dụng

`POST /rental-availability` · **Auth:** Public

Kiểm tra nhiều dòng trong một lần gọi. Chỉ để tham khảo, kết quả cuối cùng được kiểm tra lại khi tạo đơn.

**Request body**

| Field | Type | Required | Validation | Mô tả |
|---|---|---|---|---|
| `rentalStartAt` | `datetime` | ✔ | Không rỗng; theo [7.0](#70-quy-tắc-chung-của-luồng-thuê) | Bắt đầu thuê (UTC) |
| `rentalEndAt` | `datetime` | ✔ | Không rỗng; sau `rentalStartAt` | Kết thúc thuê (UTC) |
| `items` | `array` | ✔ | 1–20 dòng | Giỏ thuê |
| `items[].productVariantId` | `guid?` | ✘ | Đúng một trong hai | Biến thể sản phẩm |
| `items[].bundleId` | `guid?` | ✘ | Đúng một trong hai | Combo |
| `items[].quantity` | `int` | ✔ | 1–20 | Số lượng |

```json
{
  "rentalStartAt": "2026-11-01T01:00:00Z",
  "rentalEndAt": "2026-11-04T01:00:00Z",
  "items": [
    { "productVariantId": "6f1c2a9e-0000-0000-0000-0000000000a1", "quantity": 2 },
    { "bundleId": "6f1c2a9e-0000-0000-0000-0000000000b1", "quantity": 1 }
  ]
}
```

**Response `200 OK`**

```json
{
  "success": true,
  "statusCode": 200,
  "message": "Kiểm tra khả dụng thành công",
  "data": {
    "billableDays": 3,
    "isAllAvailable": false,
    "items": [
      { "index": 0, "isAvailable": true, "availableQuantity": 5, "message": null },
      {
        "index": 1,
        "isAvailable": false,
        "availableQuantity": 0,
        "message": "Túi ngủ đã hết trong 01/11 – 04/11."
      }
    ]
  },
  "errors": null,
  "timestamp": "2026-10-27T08:30:00Z"
}
```

- `items[].index`: vị trí dòng trong request (bắt đầu từ 0). `availableQuantity`: số đơn vị (biến thể hoặc bộ combo) còn khả dụng. `message`: `null` khi đủ hàng; ngày trong message hiển thị theo giờ Việt Nam (UTC+7).

**Lỗi**

| Status | Khi nào | `errors.code` | Message |
|---|---|---|---|
| 400 | Thiếu / sai field, `items` rỗng hoặc quá 20 dòng, `quantity` ngoài 1–20 | — (lỗi theo field) | Dữ liệu không hợp lệ |
| 400 | Khoảng thời gian không hợp lệ | `INVALID_RENTAL_PERIOD` | Thời điểm trả phải sau thời điểm nhận / … |
| 400 | Dòng không có đúng một trong `productVariantId` / `bundleId` | `INVALID_RENTAL_ITEM` | Dòng N: phải có đúng một trong productVariantId hoặc bundleId |
| 404 | Biến thể không tồn tại hoặc ngừng kinh doanh | `ITEM_NOT_FOUND` | Thiết bị không tồn tại hoặc đã ngừng kinh doanh |
| 404 | Combo không tồn tại, ngừng kinh doanh hoặc chưa có thành phần | `BUNDLE_NOT_FOUND` | Combo không tồn tại hoặc đã ngừng kinh doanh |

### 7.2. Báo giá đơn thuê

`POST /rental-orders/quote` · **Auth:** User (mọi tài khoản đã đăng nhập)

Tính giá, khuyến mãi, khả dụng và chính sách hoàn tiền cho giỏ thuê mà **không** tạo đơn, không giữ chỗ. Dòng thiếu hàng **không** làm API lỗi: trả `isAllAvailable = false` và `lines[].isAvailable = false`. Mã khuyến mãi không áp dụng được cũng không làm API lỗi: trả `promotionError`.

**Request body**

| Field | Type | Required | Validation | Mô tả |
|---|---|---|---|---|
| `rentalStartAt` | `datetime` | ✔ | Không rỗng | Bắt đầu thuê (UTC) |
| `rentalEndAt` | `datetime` | ✔ | Không rỗng | Kết thúc thuê (UTC) |
| `items` | `array` | ✔ | 1–20 dòng | Như [7.1](#71-kiểm-tra-khả-dụng) |
| `promotionCode` | `string?` | ✘ | Tối đa 50 ký tự | Mã khuyến mãi (không phân biệt hoa thường) |

```json
{
  "rentalStartAt": "2026-11-01T01:00:00Z",
  "rentalEndAt": "2026-11-04T01:00:00Z",
  "items": [
    { "productVariantId": "6f1c2a9e-0000-0000-0000-0000000000a1", "quantity": 2 },
    { "bundleId": "6f1c2a9e-0000-0000-0000-0000000000b1", "quantity": 1 }
  ],
  "promotionCode": "WELCOME10"
}
```

**Response `200 OK`**

```json
{
  "success": true,
  "statusCode": 200,
  "message": "Báo giá đơn thuê thành công",
  "data": {
    "rentalStartAt": "2026-11-01T01:00:00Z",
    "rentalEndAt": "2026-11-04T01:00:00Z",
    "billableDays": 3,
    "lines": [
      {
        "index": 0,
        "itemType": "Variant",
        "productVariantId": "6f1c2a9e-0000-0000-0000-0000000000a1",
        "bundleId": null,
        "displayName": "Lều cắm trại 2 người — 2 người / xanh lá",
        "quantity": 2,
        "dailyPrice": 120000,
        "appliedTier": { "minDays": 1, "maxDays": 3 },
        "lineAmount": 720000,
        "isAvailable": true,
        "availabilityMessage": null,
        "components": []
      },
      {
        "index": 1,
        "itemType": "Bundle",
        "productVariantId": null,
        "bundleId": "6f1c2a9e-0000-0000-0000-0000000000b1",
        "displayName": "Combo cắm trại 2 người",
        "quantity": 1,
        "dailyPrice": 150000,
        "appliedTier": { "minDays": 1, "maxDays": 3 },
        "lineAmount": 450000,
        "isAvailable": true,
        "availabilityMessage": null,
        "components": [
          {
            "productVariantId": "6f1c2a9e-0000-0000-0000-0000000000a1",
            "displayName": "Lều cắm trại 2 người — 2 người / xanh lá",
            "quantityPerBundle": 1,
            "totalQuantity": 1
          }
        ]
      }
    ],
    "subtotalAmount": 1170000,
    "discountAmount": 117000,
    "totalAmount": 1053000,
    "depositAmount": 0,
    "totalPayable": 1053000,
    "promotion": {
      "code": "WELCOME10",
      "name": "Giảm 10% đơn đầu tiên",
      "discountAmount": 117000,
      "description": null
    },
    "promotionError": null,
    "refundPolicy": {
      "refundPolicyId": "6f1c2a9e-0000-0000-0000-0000000000d1",
      "name": "Chính sách hoàn tiền thuê thiết bị",
      "version": 1,
      "description": null,
      "rules": [
        { "minHoursBefore": 48, "refundPercentage": 100 },
        { "minHoursBefore": 24, "refundPercentage": 50 },
        { "minHoursBefore": 0, "refundPercentage": 0 }
      ]
    },
    "isAllAvailable": true,
    "holdMinutes": 15
  },
  "errors": null,
  "timestamp": "2026-10-27T08:30:00Z"
}
```

- `promotion` và `promotionError` loại trừ nhau (cái còn lại là `null`). `promotionError = { "code": "PROMOTION_INVALID" | "PROMOTION_EXHAUSTED", "message": "..." }`; khi có lỗi thì `discountAmount = 0`.
- `discountAmount` không vượt `subtotalAmount`.
- `refundPolicy` là `null` nếu chưa có chính sách thuê đang hiệu lực (khi đó tạo đơn trả `409 REFUND_POLICY_CHANGED`). `rules` sắp theo `minHoursBefore` giảm dần.
- `holdMinutes`: số phút giữ chỗ sẽ áp dụng khi tạo đơn.

**Lỗi**

| Status | Khi nào | `errors.code` | Message |
|---|---|---|---|
| 400 | Thiếu / sai field, `promotionCode` quá 50 ký tự | — (lỗi theo field) | Dữ liệu không hợp lệ |
| 400 | Khoảng thời gian không hợp lệ | `INVALID_RENTAL_PERIOD` | … |
| 400 | Dòng giỏ không hợp lệ | `INVALID_RENTAL_ITEM` | … |
| 400 | Biến thể / combo chưa có bậc giá cho số ngày thuê | `PRICE_TIER_NOT_FOUND` | '…' chưa có giá cho N ngày thuê |
| 401 | Chưa đăng nhập / token hết hạn | — | — |
| 404 | Biến thể / combo không tồn tại | `ITEM_NOT_FOUND` / `BUNDLE_NOT_FOUND` | … |

### 7.3. Tạo đơn thuê

`POST /rental-orders` · **Auth:** User có role `Customer`

Tạo đơn thuê, phân bổ tài sản (`Reserved`) và giữ chỗ `holdMinutes` phút. Server tính lại toàn bộ giá / khuyến mãi / khả dụng trong transaction có khóa (khóa theo biến thể và khuyến mãi) nên có thể khác báo giá trước đó.

**Header:** `Idempotency-Key` bắt buộc, tối đa 100 ký tự (sau trim). Giống [trip booking](#31-tạo-booking-chuyến-đi): cùng user, cùng key và cùng nội dung trả lại đơn cũ; dùng cùng key cho nội dung khác trả `409 IDEMPOTENCY_KEY_REUSED`. Idempotency lưu ở `orders.idempotency_key`, không có bảng riêng. Nội dung được so sánh gồm: thời gian thuê, tập hợp dòng (biến thể / combo + số lượng, không phụ thuộc thứ tự), `promotionCode`, `note` + `pickup`, `acceptedRefundPolicyVersion`. Gửi lại hợp lệ **trả `201 Created`** như lần đầu (cùng behavior với trip booking) kèm đơn đã tạo; kiểm tra này chạy trước kiểm tra thời gian nên vẫn hoạt động sau khi đã qua giờ nhận.

**Request body** — gồm toàn bộ field của [7.2](#72-báo-giá-đơn-thuê) và:

| Field | Type | Required | Validation | Mô tả |
|---|---|---|---|---|
| `acceptedRefundPolicyVersion` | `int` | ✔ | > 0; khớp phiên bản hiện hành | Phiên bản chính sách hoàn tiền khách đã đồng ý |
| `note` | `string?` | ✘ | Tối đa 1000 ký tự | Ghi chú của khách |
| `pickup` | `object?` | ✘ | | Thông tin nhận đồ |
| `pickup.receiverName` | `string?` | ✘ | Tối đa 255 ký tự | Tên người nhận |
| `pickup.receiverPhone` | `string?` | ✘ | SĐT di động VN: `0xxxxxxxxx`, `84xxxxxxxxx` hoặc `+84xxxxxxxxx` (đầu số 3/5/7/8/9) | SĐT người nhận |
| `pickup.method` | `string?` | ✘ | Chỉ hỗ trợ `Store` | Hình thức nhận đồ (nhận tại cửa hàng) |

```json
{
  "rentalStartAt": "2026-11-01T01:00:00Z",
  "rentalEndAt": "2026-11-04T01:00:00Z",
  "items": [
    { "productVariantId": "6f1c2a9e-0000-0000-0000-0000000000a1", "quantity": 2 }
  ],
  "promotionCode": "WELCOME10",
  "note": "Nhận đồ buổi sáng",
  "pickup": { "receiverName": "Nguyễn Văn A", "receiverPhone": "0900000000", "method": "Store" },
  "acceptedRefundPolicyVersion": 1
}
```

`orders.note` lưu `note` và, nếu có thông tin nhận đồ, thêm dòng `Nhận đồ: <tên> <sđt> (<method>)`; tối đa 1500 ký tự (cắt bớt).

**Response `201 Created`** — message `Tạo đơn thuê thành công`, `data` là `RentalOrderDetailResponse` (cùng shape với [7.4](#74-chi-tiết-đơn-thuê)). Đơn mới: `status = PendingPayment`, `paymentStatus = Unpaid`, `assets = []` (tài sản `Reserved` không hiển thị cho khách), `payments = []`, `allowedActions.canPay = true`.

**Lỗi**

| Status | Khi nào | `errors.code` | Message |
|---|---|---|---|
| 400 | Thiếu / sai field (kể cả `acceptedRefundPolicyVersion` <= 0, `note` quá dài, `pickup` sai) | — (lỗi theo field) | Dữ liệu không hợp lệ |
| 400 | Thiếu header `Idempotency-Key` | `IDEMPOTENCY_KEY_REQUIRED` | Header Idempotency-Key là bắt buộc |
| 400 | `Idempotency-Key` dài hơn 100 ký tự | `VALIDATION_ERROR` | Idempotency-Key không vượt quá 100 ký tự |
| 400 | Khoảng thời gian không hợp lệ | `INVALID_RENTAL_PERIOD` | … |
| 400 | Dòng giỏ không hợp lệ | `INVALID_RENTAL_ITEM` | … |
| 400 | Chưa có bậc giá cho số ngày thuê | `PRICE_TIER_NOT_FOUND` | … |
| 401 | Chưa đăng nhập / token hết hạn | — | — |
| 403 | Không có role `Customer` | — | — |
| 404 | Biến thể / combo không tồn tại | `ITEM_NOT_FOUND` / `BUNDLE_NOT_FOUND` | … |
| 409 | Không đủ thiết bị trong khoảng thời gian thuê; `errors["items[i].quantity"]` chứa message theo dòng | `RESOURCE_UNAVAILABLE` | Thiết bị không còn đủ trong khoảng thời gian đã chọn |
| 409 | Mã khuyến mãi không tồn tại / hết hiệu lực / không áp dụng / chưa đạt điều kiện; `errors.promotionCode` chứa message | `PROMOTION_INVALID` | … |
| 409 | Mã khuyến mãi hết lượt dùng (tổng hoặc theo user); `errors.promotionCode` chứa message | `PROMOTION_EXHAUSTED` | … |
| 409 | Chưa có chính sách hoàn tiền thuê hiệu lực, hoặc `acceptedRefundPolicyVersion` khác phiên bản hiện hành | `REFUND_POLICY_CHANGED` | Chính sách hoàn tiền đã được cập nhật, vui lòng xem lại và đồng ý với phiên bản mới |
| 409 | `Idempotency-Key` đã dùng cho nội dung khác | `IDEMPOTENCY_KEY_REUSED` | Idempotency-Key đã được dùng cho một yêu cầu khác |

### 7.4. Chi tiết đơn thuê

`GET /rental-orders/{orderId}` · **Auth:** User có role `Customer`

Chỉ chủ đơn xem được. Đơn của người khác, đơn không tồn tại hoặc không phải đơn thuê đều trả `404 ORDER_NOT_FOUND` (không lộ sự tồn tại của đơn).

**Path params**

| Param | Type | Mô tả |
|---|---|---|
| `orderId` | `guid` | Id đơn (`id` trả về khi tạo đơn) |

**Response `200 OK`**

```json
{
  "success": true,
  "statusCode": 200,
  "message": "Lấy chi tiết đơn thuê thành công",
  "data": {
    "id": "6f1c2a9e-0000-0000-0000-0000000000f1",
    "orderCode": "RT-20261027-3F9A1C0B7D2E4A5B6C8D",
    "orderType": "Rental",
    "status": "PendingPayment",
    "paymentStatus": "Unpaid",
    "createdAt": "2026-10-27T10:00:00Z",
    "holdExpiresAt": "2026-10-27T10:15:00Z",
    "rentalStartAt": "2026-11-01T01:00:00Z",
    "rentalEndAt": "2026-11-04T01:00:00Z",
    "billableDays": 3,
    "note": "Nhận đồ buổi sáng\nNhận đồ: Nguyễn Văn A 0900000000 (Store)",
    "cancelledAt": null,
    "cancelReason": null,
    "handledBy": null,
    "refundPolicy": {
      "refundPolicyId": "6f1c2a9e-0000-0000-0000-0000000000d1",
      "name": "Chính sách hoàn tiền thuê thiết bị",
      "version": 1
    },
    "lines": [
      {
        "id": "6f1c2a9e-0000-0000-0000-0000000000e1",
        "itemType": "Variant",
        "displayName": "Lều cắm trại 2 người — 2 người / xanh lá",
        "sku": "TENT-2-GREEN",
        "quantity": 2,
        "dailyPrice": 120000,
        "billableDays": 3,
        "lineAmount": 720000,
        "bundleComponents": []
      }
    ],
    "assets": [],
    "amounts": {
      "subtotalAmount": 720000,
      "discountAmount": 72000,
      "totalAmount": 648000,
      "depositAmount": 0,
      "totalPayable": 648000,
      "paidAmount": 0,
      "refundedAmount": 0
    },
    "charges": [],
    "payments": [],
    "timeline": [
      { "event": "Created", "at": "2026-10-27T10:00:00Z" }
    ],
    "allowedActions": {
      "canPay": true,
      "canCancel": true,
      "cancelBlockedReason": null
    }
  },
  "errors": null,
  "timestamp": "2026-10-27T10:00:01Z"
}
```

Quy tắc hiển thị:

- `lines[].itemType`: `Variant` hoặc `Bundle`. Dòng combo có `bundleComponents[] = { variantName, quantityPerBundle }` (thành phần được chốt tại thời điểm đặt); `lines` theo thứ tự giỏ.
- `assets`: chỉ gồm phân bổ `Issued`, `Returned`, `LostOrException`; phân bổ `Reserved` bị ẩn để không lộ mã tài sản. Mỗi phần tử: `assetCode`, `displayName`, `rentalOrderItemId`, `status` ([`AssignmentStatus`](#phụ-lục-enum)), `assignedAt`, `returnedAt`, `returnCondition`.
- `amounts`: `depositAmount` luôn `0`, `totalPayable = totalAmount`; `paidAmount` / `refundedAmount` là tổng payment `Collection` / `Refund` ở trạng thái `Succeeded`.
- `charges`: phí phát sinh, **không** gồm phí trạng thái `Draft` (nháp của nhân viên). Mỗi phần tử: `id`, `chargeType`, `amount`, `reason`, `assetCode`, `status`, `createdAt`.
- `payments`: mỗi phần tử `id`, `paymentType`, `purpose`, `amount`, `paymentMethod`, `status`, `createdAt`, `processedAt`.
- `timeline`: sự kiện dẫn xuất, sắp theo thời gian; `event` thuộc `Created`, `Paid`, `Issued`, `Returned`, `Cancelled`.
- `handledBy`: `{ "fullName": "..." }` của nhân viên xử lý, `null` nếu chưa có.
- `allowedActions.canPay`: `status = PendingPayment`, `paymentStatus = Unpaid` và chưa hết `holdExpiresAt`. `canCancel`: `status` là `PendingPayment` hoặc `Confirmed` và chưa giao thiết bị nào; nếu `false` thì `cancelBlockedReason` giải thích.

**Lỗi**

| Status | Khi nào | `errors.code` | Message |
|---|---|---|---|
| 401 | Chưa đăng nhập / token hết hạn | — | — |
| 403 | Không có role `Customer` | — | — |
| 404 | Đơn không tồn tại, không thuộc user, hoặc không phải đơn thuê | `ORDER_NOT_FOUND` | Không tìm thấy đơn hàng |

---

## Phụ lục: Enum

Enum được serialize dạng **string**.

| Enum | Giá trị | Mô tả |
|---|---|---|
| `UserRole` | `TODO` | TODO |
| `OrderType` | `Rental`, `TripBooking` | Loại đơn: thuê thiết bị / booking chuyến đi |
| `OrderStatus` | `Draft`, `PendingPayment`, `Confirmed`, `InFulfillment`, `Completed`, `Expired`, `Cancelled` | Trạng thái đơn. Đơn thuê mới tạo là `PendingPayment` |
| `OrderPaymentStatus` | `Unpaid`, `Paid`, `PartiallyRefunded`, `Refunded`, `NeedsReconciliation` | Trạng thái thanh toán của đơn (tách riêng với `status`) |
| `AssignmentStatus` | `Reserved`, `Issued`, `Returned`, `Cancelled`, `LostOrException` | Trạng thái phân bổ tài sản cho dòng thuê. Khách chỉ thấy `Issued`, `Returned`, `LostOrException` |
| `RentalItemType` | `Variant`, `Bundle` | Loại dòng trong giỏ / đơn thuê (`itemType`) |
| `TODO` | `TODO` | TODO |

---

## Phụ lục: Mã lỗi

| HTTP Status | Ý nghĩa chung | Ghi chú cho FE |
|---|---|---|
| 400 | Dữ liệu không hợp lệ | Hiển thị `errors` theo từng field |
| 401 | Chưa đăng nhập / token hết hạn | Gọi refresh token, thất bại thì về trang login |
| 403 | Không đủ quyền | TODO |
| 404 | Không tìm thấy | TODO |
| 409 | Dữ liệu bị trùng | TODO |
| 500 | Lỗi hệ thống | Hiển thị thông báo chung |

### Mã lỗi nghiệp vụ (`errors.code`)

Lỗi nghiệp vụ trả thêm key `code` trong `errors` (mảng 1 phần tử), ví dụ:

```json
{
  "success": false,
  "statusCode": 409,
  "message": "Thiết bị không còn đủ trong khoảng thời gian đã chọn",
  "data": null,
  "errors": {
    "items[0].quantity": ["Chỉ còn 1 Lều cắm trại 2 người — 2 người / xanh lá trong 01/11 – 04/11."],
    "code": ["RESOURCE_UNAVAILABLE"]
  },
  "timestamp": "..."
}
```

FE xử lý theo `errors.code`, không so khớp `message`. Lỗi validation theo field (400) chỉ có key theo field, không có `code`. Các mã dưới đây được dùng bởi `/bundles`, `/rental-availability`, `/rental-orders*`.

| `errors.code` | HTTP | Endpoint | Khi nào |
|---|---|---|---|
| `INVALID_RENTAL_PERIOD` | 400 | 6.1, 6.2, 7.1, 7.2, 7.3 | `to` <= `from`, `from` ở quá khứ (quá 5 phút), thuê quá 60 ngày, hoặc chỉ truyền một trong `from`/`to` (bundles) |
| `INVALID_RENTAL_ITEM` | 400 | 7.1, 7.2, 7.3 | Giỏ rỗng, dòng không có đúng một trong `productVariantId`/`bundleId`, `quantity` <= 0 |
| `PRICE_TIER_NOT_FOUND` | 400 | 7.2, 7.3 | Biến thể / combo chưa có bậc giá cho số ngày thuê |
| `IDEMPOTENCY_KEY_REQUIRED` | 400 | 7.3 | Thiếu header `Idempotency-Key` |
| `VALIDATION_ERROR` | 400 | 7.3 | `Idempotency-Key` dài hơn 100 ký tự |
| `ITEM_NOT_FOUND` | 404 | 7.1, 7.2, 7.3 | Biến thể không tồn tại / ngừng kinh doanh (hoặc combo có thành phần ngừng kinh doanh) |
| `BUNDLE_NOT_FOUND` | 404 | 6.2, 7.1, 7.2, 7.3 | Combo không tồn tại / ngừng kinh doanh / chưa có thành phần |
| `ORDER_NOT_FOUND` | 404 | 7.4 | Đơn không tồn tại, không thuộc user, hoặc không phải đơn thuê |
| `RESOURCE_UNAVAILABLE` | 409 | 7.3 | Không đủ thiết bị trong khoảng thời gian thuê |
| `PROMOTION_INVALID` | 409 (7.3) | 7.2, 7.3 | Mã khuyến mãi không tồn tại, hết hiệu lực, không áp dụng cho đơn thuê, hoặc chưa đạt điều kiện. Ở 7.2 nằm trong `data.promotionError.code`, không làm API lỗi |
| `PROMOTION_EXHAUSTED` | 409 (7.3) | 7.2, 7.3 | Mã hết lượt dùng (tổng hoặc theo user). Ở 7.2 nằm trong `data.promotionError.code` |
| `REFUND_POLICY_CHANGED` | 409 | 7.3 | Chưa có chính sách hoàn tiền thuê hiệu lực, hoặc `acceptedRefundPolicyVersion` khác phiên bản hiện hành |
| `IDEMPOTENCY_KEY_REUSED` | 409 | 7.3 | `Idempotency-Key` đã dùng cho nội dung khác |

> Các lỗi `401` / `403` (thiếu token, sai role `Customer`) do middleware xác thực trả về, không có `errors.code`.

---

## Template endpoint

Copy khối dưới đây khi thêm API mới:

````markdown
### X.Y. <Tên API>

`<METHOD> /<resource>/{id}` · **Auth:** <Public | User | Admin>

<Mô tả ngắn: API dùng để làm gì, business rule quan trọng>

**Path params**

| Param | Type | Mô tả |
|---|---|---|
| `id` | `guid` | |

**Query params**

| Param | Type | Required | Default | Mô tả |
|---|---|---|---|---|
| | | | | |

**Request body**

| Field | Type | Required | Validation | Mô tả |
|---|---|---|---|---|
| | | | | |

```json
{
}
```

**Response `200 OK`**

```json
{
  "success": true,
  "statusCode": 200,
  "message": "",
  "data": {
  },
  "errors": null,
  "timestamp": "..."
}
```

**Lỗi**

| Status | Khi nào | Message |
|---|---|---|
| | | |
````

---

## Changelog

| Ngày | Người sửa | Endpoint | Thay đổi |
|---|---|---|---|
| 2026-10-27 | — | `/bundles*`, `/rental-availability`, `/rental-orders*`, `/users/me/orders` | Thêm danh sách / chi tiết combo, kiểm tra khả dụng, báo giá, tạo (Idempotency-Key trên `orders.idempotency_key`) và xem đơn thuê, đơn hàng của tôi; thêm enum `OrderType`, `OrderStatus`, `OrderPaymentStatus`, `AssignmentStatus`, `RentalItemType` và bảng mã lỗi nghiệp vụ `errors.code` |
| 2026-10-26 | Codex | `/trip-bookings/*` | Thêm tạo booking nhóm, xem theo quyền, tạo và nhận lời mời participant |
| 2026-10-06 | — | — | Khởi tạo skeleton |
| 2026-10-23 | — | Products | Thêm API danh sách, chi tiết và danh mục cho sản phẩm thuê / bán |
