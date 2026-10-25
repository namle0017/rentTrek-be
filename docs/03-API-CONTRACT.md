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
| 3.1 | Trip Bookings | `POST` | `/trip-bookings` | User | Tạo booking và giữ chỗ | ✅ Done |
| 3.2 | Trip Bookings | `GET` | `/trip-bookings/{orderId}` | User | Xem booking theo phạm vi quyền | ✅ Done |
| 3.3 | Trip Bookings | `POST` | `/trip-bookings/{orderId}/participant-invitations` | User | Tạo lại lời mời sau thanh toán | ✅ Done |
| 3.4 | Trip Bookings | `POST` | `/trip-bookings/participant-invitations/claim` | User | Nhận vị trí participant bằng token | ✅ Done |
| 4.1 | Tours | `GET` | `/tours` | Public | Lấy danh sách tour kèm bộ lọc & phân trang | ✅ Done |
| 4.2 | Tours | `GET` | `/tours/{id}` | Public | Lấy thông tin chi tiết một tour | ✅ Done |
| 5.1 | Products | `GET` | `/products` | Public | Danh sách sản phẩm thuê / bán | ✅ Done |
| 5.2 | Products | `GET` | `/products/{id}` | Public | Chi tiết sản phẩm, biến thể, ảnh, giá | ✅ Done |
| 5.3 | Products | `GET` | `/products/categories` | Public | Danh mục lọc sản phẩm | ✅ Done |

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
và biến thể có ít nhất một giá thuê hoặc giá bán. Sản phẩm không có biến thể đủ điều kiện không xuất hiện.
Ảnh và bảng giá đã soft delete không xuất hiện.

**Mô hình:** theo các bảng categories → products → product_variants → images / rental_price_tiers
trong `database/rentrek_schema.sql`, chuyển sang SQL Server theo code hiện tại.
ID dùng `long` như các entity hiện có. Bổ sung `SalePrice` nullable ở biến thể vì schema tham khảo
chưa có giá bán. Không triển khai bundle, đơn hàng hoặc kiểm tra tồn kho theo ngày trong task này.
Giá là số VND; `null` nghĩa là không cung cấp hình thức đó, giá `0` vẫn hợp lệ.
`pricePerDay` là mức giá thuê thấp nhất trong các bậc giá và biến thể công khai;
`salePrice` là giá bán thấp nhất. Đây là giá **từ**, không phải báo giá cho số ngày thuê cụ thể.
`isRentable` / `isForSale` mô tả hình thức cung cấp, không khẳng định còn hàng.
Ngày giờ trả ISO 8601 UTC. FE gọi qua Axios instance dùng chung và React Query.

### 5.1. Danh sách sản phẩm

`GET /products` · **Auth:** Public

| Query | Kiểu | Mặc định | Validation / ý nghĩa |
|---|---|---|---|
| `pageNumber` | int | 1 | Dùng PagingRequest chung: nhỏ hơn 1 về 1; offset vượt Int32 trả 400 |
| `pageSize` | int | 10 | Dùng PagingRequest chung: tối đa 100, nhỏ hơn 1 về 10 |
| `search` | string | null | Tối đa 200 ký tự, trim, tìm tên hoặc mô tả |
| `categoryId` | long? | null | ID dương |
| `offeringType` | string? | null | `Rental`, `Sale`, `Both`; Both = sản phẩm có cả thuê và bán |
| `sortBy` | string | createdAt | `createdAt`, `name`, `pricePerDay`, `salePrice` |
| `sortOrder` | string | desc | `asc`, `desc` |

Sắp xếp ổn định theo ID khi bằng giá trị; giá null luôn xếp cuối.
Lọc theo hình thức ở cấp sản phẩm; response vẫn hiển thị tất cả hình thức sản phẩm cung cấp.
Trang vượt tổng số trang trả items rỗng. Dữ liệu rỗng trả 200, không trả 404.
Query sai trả 400 + `errors` theo field.

**Response 200:** `ApiResponse<PagedResult<ProductResponse>>`, ví dụ `data`:

```json
{
  "items": [{
    "id": 1,
    "name": "Lều cắm trại 2 người",
    "categoryId": 1,
    "categoryName": "Lều cắm trại",
    "thumbnailUrl": "/images/products/tent.svg",
    "isRentable": true,
    "isForSale": true,
    "pricePerDay": 90000,
    "salePrice": 1800000,
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

`GET /products/{id}` · **Auth:** Public · `id`: long

**Response 200:** `ApiResponse<ProductDetailResponse>` gồm toàn bộ field ProductResponse và:

```json
{
  "description": "Lều chống nước, phù hợp cho hai người.",
  "variants": [{
    "id": 1,
    "sku": "TENT-2-GREEN",
    "name": "2 người / xanh lá",
    "size": "2 người",
    "color": "Xanh lá",
    "salePrice": 1800000,
    "images": [{ "id": 1, "imageUrl": "/images/products/tent.svg", "isPrimary": true, "displayOrder": 0 }],
    "rentalPriceTiers": [{ "id": 1, "minDays": 1, "maxDays": 3, "dailyPrice": 120000 }]
  }]
}
```

Biến thể được sắp theo ID, bậc giá theo minDays rồi ID. `maxDays = null` nghĩa là không giới hạn trên.
ID không tồn tại, không dương hoặc sản phẩm không công khai trả 404 với wrapper chuẩn.
ID không đúng định dạng số trả 400 theo model binding chuẩn.

### 5.3. Danh mục sản phẩm

`GET /products/categories` · **Auth:** Public

**Response 200:** `ApiResponse<IReadOnlyList<ProductCategoryResponse>>`;
`data = [{ "id": 1, "name": "Lều cắm trại" }]`, sắp theo tên rồi ID.
Chỉ trả danh mục Active, chưa xoá và có sản phẩm công khai đủ điều kiện.

**Dữ liệu local:** sau `dotnet ef database update`, có thể chạy
`docs/database/seed_product_catalog.sql` trên SQL Server `RenTrekDb`.
Script chỉ thêm dữ liệu mẫu nếu bảng Products chưa có dữ liệu; không seed tự động khi khởi động API.

**Kiểm tra local:** chạy `./tests/product-catalog.smoke.ps1 -WithDatabaseFixtures` tại thư mục BE.
Script kiểm tra API và tạo fixture tạm trong database local, sau đó dọn fixture trong `finally`.
Tại FE chạy `node --use-system-ca --test tests/product-catalog.render.test.mjs` để kiểm tra
Axios + React render bằng dữ liệu thật (cần BE đang chạy, chứng chỉ dev đã trust, Node 24+).
Đây là kiểm tra render trong Node; không thay thế kiểm tra trực quan desktop/mobile trên trình duyệt.

---

## Phụ lục: Enum

Enum được serialize dạng **string**.

| Enum | Giá trị | Mô tả |
|---|---|---|
| `UserRole` | `TODO` | TODO |
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
| 2026-10-26 | Codex | `/trip-bookings/*` | Thêm tạo booking nhóm, xem theo quyền, tạo và nhận lời mời participant |
| 2026-10-06 | — | — | Khởi tạo skeleton |
| 2026-10-23 | — | Products | Thêm API danh sách, chi tiết và danh mục cho sản phẩm thuê / bán |
