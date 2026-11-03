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
- [8. Trip Groups](#8-trip-groups)
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
| 1.5 | Auth | `POST` | `/auth/verify-email` | Public | Xác thực email và tự gán chuyến đã thanh toán | ✅ Done |
| 1.6 | Auth | `POST` | `/auth/resend-verification-email` | User | Gửi lại email xác thực | ✅ Done |
| 2.1 | Users | `GET` | `/users/me` | User | Lấy thông tin cá nhân | ⬜ TODO |
| 2.2 | Users | `PUT` | `/users/me` | User | Cập nhật thông tin cá nhân | ⬜ TODO |
| 2.3 | Users | `GET` | `/users` | Admin | Danh sách user | ⬜ TODO |
| 2.4 | Users | `GET` | `/users/me/orders` | User | Đơn hàng của tôi (đơn thuê và booking chuyến đi) | 🔁 Changed |
| 3.1 | Trip Bookings | `POST` | `/trip-bookings` | User | Tạo booking nhóm và giữ chỗ (hỗ trợ đặt giùm, gán theo email) | 🔁 Changed |
| 3.2 | Trip Bookings | `GET` | `/trip-bookings/{orderId}` | User | Xem booking theo phạm vi quyền | 🔁 Changed |
| 3.3 | Trip Bookings | `PUT` | `/trip-bookings/{orderId}/participants/{participantId}` | User | Buyer sửa họ tên, email, số điện thoại người đi trước khởi hành | ✅ Done |
| 3.4 | Trip Bookings | `GET`, `POST` | `/trip-bookings/{orderId}/invite-link`, `/invite-link/rotate` | User | Lấy hoặc đổi link xem chuyến chỉ đọc | 🔁 Changed |
| 3.5 | Trip Bookings | `POST` | `/trip-bookings/participant-invitations/preview` | Public | Xem danh sách họ tên đoàn và lịch hoạt động bằng link | 🔁 Changed |
| 3.6 | Trip Bookings | `POST` | `/trip-bookings/{orderId}/cancel` | User | Hủy toàn bộ booking trước khởi hành, giải phóng chỗ group và giữ lịch sử | ✅ Done |
| 4.1 | Tours | `GET` | `/tours` | Public | Lấy danh sách tour kèm bộ lọc & phân trang | ✅ Done |
| 4.2 | Tours | `GET` | `/tours/{id}` | Public | Lấy thông tin chi tiết một tour | ✅ Done |
| 4.3 | Tours | `PUT` | `/tours/{id}/group-settings` | Admin | Cấu hình `groupMaxParticipants` cho phiên bản Tour | ✅ Done |
| 4.4 | Tours | `GET`, `POST` | `/tours/{id}/recurrence-rules` | Admin | Danh sách và tạo quy tắc lịch lặp kèm `defaultGroupCount` | ✅ Done |
| 5.1 | Products | `GET` | `/products` | Public | Danh sách sản phẩm thuê / bán | ✅ Done |
| 5.2 | Products | `GET` | `/products/{id}` | Public | Chi tiết sản phẩm, biến thể, ảnh, giá | ✅ Done |
| 5.3 | Products | `GET` | `/products/categories` | Public | Danh mục lọc sản phẩm | ✅ Done |
| 6.1 | Product Bundles | `GET` | `/bundles` | Public | Danh sách combo thiết bị, kèm khả dụng theo khoảng thời gian | 🟨 In progress |
| 6.2 | Product Bundles | `GET` | `/bundles/{bundleId}` | Public | Chi tiết combo, thành phần, bậc giá, khả dụng | 🟨 In progress |
| 7.1 | Rental Orders | `POST` | `/rental-availability` | Public | Kiểm tra khả dụng nhiều dòng | 🟨 In progress |
| 7.2 | Rental Orders | `POST` | `/rental-orders/quote` | User | Báo giá đơn thuê | 🟨 In progress |
| 7.3 | Rental Orders | `POST` | `/rental-orders` | Customer | Tạo đơn thuê và giữ chỗ (Idempotency-Key) | 🟨 In progress |
| 7.4 | Rental Orders | `GET` | `/rental-orders/{orderId}` | Customer | Chi tiết đơn thuê của tôi | 🟨 In progress |
| 8.1 | Trip Groups | `GET` | `/trips/{tripId}/grouping` | Staff/Admin/Guide | Snapshot chia đoàn theo quyền | ✅ Done |
| 8.2 | Trip Groups | `PUT` | `/trips/{tripId}/groups/{groupId}/guide` | Staff/Admin | Gán hoặc bỏ guide khi còn Draft | ✅ Done |
| 8.3 | Trip Groups | `PUT` | `/trips/{tripId}/bookings/{bookingOrderId}/group` | Staff/Admin | Chuyển nguyên booking sang group | ✅ Done |
| 8.4 | Trip Groups | `POST` | `/trips/{tripId}/bookings/{bookingOrderId}/split` | Staff/Admin | Tách booking có lý do | ✅ Done |
| 8.4b | Trip Groups | `POST` | `/trips/{tripId}/grouping/recalculate-provided-items` | Staff/Admin | Tính lại nhu cầu đồ cấp chuyến (`PER_GROUP` / `PER_PARTICIPANT`) | ✅ Done |
| 8.5 | Trip Groups | `POST` | `/trips/{tripId}/grouping/publish` | Staff/Admin | Chốt toàn bộ group của Trip | ✅ Done |
| 8.6 | Trip Groups | `GET` | `/trip-bookings/{orderId}/grouping` | User | Group Published trong phạm vi booking | ✅ Done |
| 8.6b | Trip Groups | `GET` | `/guide/trip-groups`, `/guide/trip-groups/{groupId}` | Guide | Danh sách và chi tiết group được giao cho hướng dẫn viên | ✅ Done |
| 8.0b | Trips (Admin) | `GET`, `POST`, `PUT`, `PATCH` | `/trips/{tripId}`, `/trips`, `/trips/{tripId}/status`, `/trips/generate-recurring` | Admin/Staff | Quản trị Trip, đồng bộ số group `Draft`, mở bán và sinh chuyến từ lịch lặp | ✅ Done |
| 9.1 | Admin Catalog | `GET` | `/admin/tours` | Admin/Staff | Danh sách phiên bản Tour mới nhất cho O05 | ✅ Done |
| 9.2 | Admin Catalog | `GET` | `/admin/products` | Admin/Staff | Danh sách sản phẩm và số lượng tài sản cho O16 | ✅ Done |

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

### 1.5. Xác thực email

`POST /auth/verify-email` · **Auth:** Public

Xác thực email tài khoản bằng token HMAC gửi trong link `/verify-email#t=<token>` sau khi đăng ký (hạn 24 giờ). Khi xác thực thành công, BE gán `emailConfirmedAt` và tự động gắn `linkedUserId` cho mọi chỗ chưa liên kết có cùng email trong các đơn `Confirmed` / `Paid` chưa khởi hành (`trip.startAt > now`). Các response Auth (`user`) và `GET /users/me` đều có trường `emailConfirmed: bool`.

**Request body**

```json
{
  "token": "<token>"
}
```

**Response `200 OK`** — `data = null`, message `Xác thực email thành công`.

**Lỗi:** `400` thiếu token, token không hợp lệ hoặc đã hết hạn; `403` tài khoản không hoạt động; `404` không tìm thấy tài khoản.

---

### 1.6. Gửi lại email xác thực

`POST /auth/resend-verification-email` · **Auth:** User

Gửi lại link xác thực email cho tài khoản đang đăng nhập. Nếu tài khoản đã xác thực email rồi, API vẫn trả `200 OK` an toàn mà không gửi lại.

**Response `200 OK`** — `data = null`, message `Đã gửi lại email xác thực`.

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

Danh sách đơn của user đang đăng nhập, gồm cả đơn thuê (`Rental`) và booking chuyến đi (`TripBooking`). Bao gồm đơn do chính user tạo (`CustomerUserId`) và các chuyến đi mà user được gán chỗ (`LinkedUserId`) khi tài khoản đang hoạt động và **đã xác thực email**. Với chuyến đi xem dưới tư cách người đi cùng (không phải chủ đơn), `totalAmount = 0`, `totalPayable = 0`, `paymentStatus = ""`, `holdExpiresAt = null`. `totalPayable` luôn bằng `totalAmount` (không có cọc).

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

Đơn đặt chuyến đi cho cá nhân hoặc nhóm theo cơ chế **gán theo email, không cần bước mời/claim**:

- Người đặt (buyer) phải có tài khoản đang hoạt động. Mặc định `buyerParticipates = true` (buyer là participant đầu tiên); nếu `buyerParticipates = false` (**đặt giùm**) thì buyer không phải participant, không tốn chỗ và không tính tiền nhưng vẫn là chủ đơn (`CustomerUserId`) để thanh toán, xem và quản lý đơn.
- Giá và số chỗ do server tính theo tổng số participant thực tế đi. Đơn giữ chỗ 15 phút khi chưa thanh toán.
- `linkedUserId` của người đi cùng chỉ được tự động gán khi đơn `Confirmed` / `Paid`, chuyến chưa khởi hành (`trip.startAt > now`) và tài khoản trùng email **đã xác thực email** (xảy ra tại thời điểm webhook SePay xác nhận thanh toán hoặc khi người đó xác thực email sau đó). Sau khi thanh toán thành công, BE gửi email thông báo cho các người đi cùng có khai email.

### 3.1. Tạo booking chuyến đi

`POST /trip-bookings` · **Auth:** User

**Header:** `Idempotency-Key` bắt buộc, tối đa 100 ký tự. Cùng user và cùng payload trả lại booking cũ. Dùng cùng key cho payload khác trả `409`.

**Request body**

```json
{
  "tripId": "00000000-0000-0000-0000-000000000000",
  "contactPhone": "0901234567",
  "buyerParticipates": true,
  "note": "Đi cùng nhóm bạn",
  "additionalParticipants": [
    {
      "fullName": "Nguyễn Văn B",
      "email": "b@example.com",
      "phone": "0900000000",
      "dateOfBirth": "2000-01-01",
      "emergencyContactName": "Nguyễn Văn C",
      "emergencyContactPhone": "0911111111"
    },
    {
      "fullName": "Bé Minh"
    }
  ]
}
```

- `contactPhone`: bắt buộc (tối đa 20 ký tự). Nếu `buyerParticipates = true`, chỗ của buyer dùng `contactPhone` làm `phone`.
- `buyerParticipates`: mặc định `true`. Khi `false` (đặt giùm), `additionalParticipants` bắt buộc có ít nhất 1 người, và một người đi cùng được phép dùng trùng email với buyer.
- `additionalParticipants`: tối đa 20 người đi cùng. `fullName` bắt buộc; `email` và `phone` đều **tuỳ chọn** (trẻ em / người lớn tuổi có thể để trống).
- Nếu người đi cùng có `phone` thì lưu vào `phone`; nếu bỏ trống `phone` (và không truyền `emergencyContactPhone`) thì BE tự gán `emergencyContactName` = tên buyer và `emergencyContactPhone` = `contactPhone`.
- Các chỗ có khai `email` được `trim` + chuyển chữ thường và không được trùng nhau trong cùng đơn (không trùng email buyer khi `buyerParticipates = true`).
- Kiểm tra trạng thái trip và sức chứa trong transaction có khóa hàng.

**Response:** `201 Created`, `data` là `TripBookingResponse`.

**Lỗi:** `400` thiếu `contactPhone`, đặt giùm không có người đi cùng, định dạng email sai hoặc thiếu/sai `Idempotency-Key`; `403` account không hoạt động; `404` không có account/trip; `409` hết chỗ, trip đóng, email trùng, hoặc key dùng cho payload khác.

### 3.2. Xem booking

`GET /trip-bookings/{orderId}` · **Auth:** User

- **Buyer** (kể cả đặt giùm): nhận toàn bộ tiền, `paymentStatus`, `holdExpiresAt` và toàn bộ `participants`; `canManage = true`.
- **Participant đã liên kết** (tài khoản đang hoạt động và **đã xác thực email**): chỉ nhận trip, trạng thái booking và đúng participant của chính mình. Các trường tiền bằng `0`, `paymentStatus` rỗng, `holdExpiresAt = null`, `canManage = false`.
- User ngoài booking hoặc tài khoản chưa xác thực email nhận `404` để không lộ booking tồn tại.

**Response `200 OK`**

```json
{
  "success": true,
  "statusCode": 200,
  "message": "Lấy booking thành công",
  "data": {
    "orderId": "00000000-0000-0000-0000-000000000000",
    "orderCode": "RT-20261030-ABC",
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
    "holdExpiresAt": "2026-10-30T10:15:00Z",
    "canManage": true,
    "participants": [
      {
        "participantId": "11111111-1111-1111-1111-111111111111",
        "fullName": "Nguyễn Văn A",
        "email": "a@example.com",
        "phone": "0901234567",
        "linkedUserId": "22222222-2222-2222-2222-222222222222",
        "status": "Registered"
      }
    ]
  },
  "errors": null,
  "timestamp": "..."
}
```

### 3.3. Sửa thông tin người tham gia

`PUT /trip-bookings/{orderId}/participants/{participantId}` · **Auth:** Buyer

Người đặt sửa `fullName`, `email`, `phone` của một chỗ trước khi chuyến khởi hành (`now < trip.startAt`).

```json
{
  "fullName": "Nguyễn Văn B",
  "email": "b.new@example.com",
  "phone": "0988888888"
}
```

- `fullName` bắt buộc; `email` và `phone` tuỳ chọn.
- Nếu đổi sang `email` khác với email cũ của chỗ đó: BE xoá `linkedUserId` cũ ngay lập tức (tài khoản cũ mất quyền xem chuyến); nếu đơn đã `Confirmed` / `Paid` và email mới khớp tài khoản hoạt động đã xác thực email thì BE tự gán `linkedUserId` sang tài khoản mới (trừ khi tài khoản đó đã giữ một chỗ khác trong cùng đơn).
- Nếu bỏ trống `phone`, BE tự điền `emergencyContactName` / `emergencyContactPhone` theo thông tin của buyer.

**Response:** `200 OK`, `data` là `TripBookingResponse` đầy đủ của buyer.

**Lỗi:** `400` thiếu họ tên hoặc email sai định dạng; `404` không phải booking của buyer hoặc không tìm thấy participant; `409` chuyến đã khởi hành hoặc email trùng chỗ khác trong cùng đơn.

### 3.4. Link xem chuyến cho nhóm

Cần **Auth:** Buyer, đơn `Confirmed`, payment `Paid`, tổng payment `Succeeded` đủ tiền và chuyến **chưa kết thúc** (`now < trip.endAt`; vẫn lấy và đổi được link khi chuyến đang diễn ra sau `trip.startAt`).

- `GET /trip-bookings/{orderId}/invite-link`: trả `{ url, expiresAt, totalSeats }`. Token là HMAC-SHA256 từ `orderId` và `invite_version`, không lưu trong DB, nằm sau `#t=` trên URL. Link hết hạn tại `trip.endAt`.
- `POST /trip-bookings/{orderId}/invite-link/rotate`: tăng `invite_version`, làm link cũ hết hiệu lực ngay. Trả cùng cấu trúc `InviteLinkResponse`.

**Lỗi:** `404` không phải booking của buyer; `409` đơn chưa thanh toán đủ hoặc chuyến đã kết thúc.

### 3.5. Xem thông tin chuyến và lịch hoạt động bằng link

`POST /trip-bookings/participant-invitations/preview` · **Auth:** Public

Trang chỉ đọc cho nhóm đi cùng (không cần đăng nhập, không gán tài khoản, không hiển thị giá tiền, thanh toán, email, số điện thoại hay ngày sinh).

**Request body**

```json
{
  "orderId": "00000000-0000-0000-0000-000000000000",
  "token": "<token>"
}
```

**Response `200 OK`**

```json
{
  "orderId": "00000000-0000-0000-0000-000000000000",
  "tourName": "Tà Năng - Phan Dũng 2N1Đ",
  "tripStartAt": "2026-12-06T23:00:00Z",
  "tripEndAt": "2026-12-08T11:00:00Z",
  "buyerName": "Nguyễn Văn A",
  "expiresAt": "2026-12-08T11:00:00Z",
  "participantNames": ["Nguyễn Văn A", "Nguyễn Văn B"],
  "activities": [
    {
      "activityId": "11111111-1111-1111-1111-111111111111",
      "dayNumber": 1,
      "dayTitle": "Ngày 1",
      "startTime": "06:00:00",
      "durationMinutes": 90,
      "displayOrder": 1,
      "name": "Tập trung đoàn",
      "description": null,
      "scheduledStartAt": "2026-12-07T06:00:00+07:00",
      "scheduledEndAt": "2026-12-07T07:30:00+07:00",
      "isCurrent": false
    }
  ]
}
```

- `activities` xếp theo `dayNumber` rồi `displayOrder`; `scheduledStartAt` / `scheduledEndAt` tính từ ngày của `tripStartAt` theo giờ Việt Nam (`UTC+7`, `dayNumber = 1` là ngày khởi hành) cộng `startTime` và `durationMinutes`.
- Khi chuyến đang diễn ra (`tripStartAt <= now < tripEndAt`), hoạt động có `scheduledStartAt <= now < scheduledEndAt` được đánh dấu `isCurrent = true` (theo lịch dự kiến).
- Link sai, đã đổi (`rotate`), đơn chưa `Paid` hoặc đã qua `tripEndAt` đều trả `404`.

### 3.6. Hủy booking chuyến đi

`GET /trip-bookings/{orderId}/cancellation-preview` · **Auth:** Buyer — xem trước số tiền hoàn.

`POST /trip-bookings/{orderId}/cancel` · **Auth:** Buyer hoặc Staff/Admin (Staff/Admin không hủy được đơn phát sinh hoàn tiền)

Hủy toàn bộ booking chuyến đi trước giờ khởi hành (`now < trip.startAt`). Quy trình hoàn tiền xem [`05-MANUAL-REFUND-WORKFLOW.md`](05-MANUAL-REFUND-WORKFLOW.md).

```json
{
  "reason": "Khách bận lịch đột xuất",
  "expectedRefundAmount": 1600000,
  "bankAccount": { "bankCode": "VCB", "accountNumber": "0123456789", "accountHolder": "NGUYEN VAN A" },
  "consentVersion": "refund-bank-data-v1",
  "consentAccepted": true
}
```

- Đặt `order.status = "Cancelled"` và đóng các hold chưa hết hạn. Đơn đã `Paid` được hoàn tiền thì tạo Payment Refund `PendingApproval` + RefundCase; `paymentStatus` giữ `"Paid"` tới khi tiền hoàn thành công.
- Số tiền lệch preview trả `409 REFUND_AMOUNT_CHANGED`. `bankAccount` và consent chỉ bắt buộc khi số tiền hoàn lớn hơn 0.
- Giữ nguyên `participant.tripGroupId` để truy vết lịch sử/audit, nhưng participant của đơn hủy không còn tính vào sức chứa group hiện tại và không xuất hiện trên manifest Staff, màn Guide hay màn Customer.
- Không tự cân bằng lại các booking khác; đánh dấu `trip.providedItemsNeedsRecalc = true` và tăng `trip.rowVersion`.
- **Response:** `200 OK`, `data` là `TripBookingResponse`.
- **Lỗi:** `404` không tìm thấy hoặc không thuộc quyền; `409` chuyến đã khởi hành hoặc trạng thái đơn không cho hủy (`Completed` / `Expired`).

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

**Response 200:** `ApiResponse<PagedResponse<ProductResponse>>`, ví dụ `data`:

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

## 8. Trip Groups

> **Trạng thái:** ✅ Done — BE đã triển khai đầy đủ các endpoint TripGroup, quản trị cấu hình group trên Tour/Trip/Lịch lặp, tính lại đồ cấp chuyến và màn Guide trên Swagger.

### 8.0. Trường cấu hình trên màn Admin

Các DTO quản trị Tour/Trip/lịch lặp bổ sung:

| Field | Nằm trên | Quy tắc |
|---|---|---|
| `groupMaxParticipants` | Tour version | Số nguyên dương; là sức chứa chuẩn của mỗi group, được chụp vào `TripGroup.MaxParticipants` khi tạo Trip |
| `groupCount` | Request tạo/cập nhật Trip `Draft` | Số nguyên dương; BE tạo đúng số group cùng transaction với Trip |
| `defaultGroupCount` | Lịch lặp | Số nguyên dương; Trip sinh tự động dùng giá trị này |

`totalSlots` của Trip vẫn độc lập. FE hiển thị:

```text
standardCapacity = groupCount * groupMaxParticipants
operationalCapacity = groupCount * (groupMaxParticipants + overflowSlots)
```

Admin được lưu Trip `Draft` khi `operationalCapacity < totalSlots`, nhưng nút mở bán phải hiển thị blocker do BE trả. Sau khi Trip đã `Open`, `groupCount` chỉ đọc; FE không hiển thị thao tác thêm hoặc xóa group.

**Các API quản trị tương ứng:**

- `PUT /tours/{id}/group-settings` (`Admin`): `{ "groupMaxParticipants": 12 }` → cập nhật sức chứa chuẩn mỗi group trên Tour version.
- `GET /tours/{id}/recurrence-rules` & `POST /tours/{id}/recurrence-rules` (`Admin`): cấu hình lịch lặp kèm `defaultGroupCount > 0`.
- `POST /trips` (`Admin`), `PUT /trips/{tripId}` (`Admin`), `GET /trips/{tripId}` (`Staff/Admin`): tạo/sửa Trip `Draft` kèm `groupCount` (tự tạo/đồng bộ các group `Draft` trong cùng transaction; sau khi `Open` sửa `groupCount` trả `409 TRIP_GROUP_COUNT_LOCKED`).
- `PATCH /trips/{tripId}/status` (`Staff/Admin`): `{ "status": "Open" | "ClosedForBooking" }` → mở bán kiểm tra `operationalCapacity >= totalSlots` (`409 TRIP_OPERATIONAL_CAPACITY_INSUFFICIENT`).
- `POST /trips/generate-recurring` (`Admin`): `{ "recurrenceRuleId"?, "fromUtc", "toUtc" }` → sinh Trip và group idempotent theo `defaultGroupCount`; rule cũ thiếu `defaultGroupCount` không sinh Trip và trả `warnings`.

### 8.1. Lấy snapshot chia đoàn

`GET /trips/{tripId}/grouping` · **Auth:** Staff/Admin/Guide

- Staff/Admin nhận mọi group và mục `unassignedBookings`.
- Guide nhận group được gán cho chính họ, kể cả `Draft`; không nhận group hoặc booking ngoài phạm vi.
- Booking hủy chỉ còn trong audit, không xuất hiện trong response.

**Response `200 OK`**

```json
{
  "success": true,
  "statusCode": 200,
  "message": "Lấy thông tin chia đoàn thành công",
  "data": {
    "tripId": "00000000-0000-0000-0000-000000000000",
    "tripStatus": "ClosedForBooking",
    "groupingStatus": "Draft",
    "totalSlots": 30,
    "groupMaxParticipants": 12,
    "overflowSlots": 2,
    "standardCapacity": 24,
    "operationalCapacity": 28,
    "assignedParticipants": 20,
    "unassignedParticipants": 3,
    "rowVersion": 7,
    "providedItemsNeedsRecalc": false,
    "canPublish": false,
    "canPublish": false,
    "publishBlockers": [
      {
        "code": "UNASSIGNED_PARTICIPANTS",
        "message": "Còn 3 người chưa được xếp nhóm"
      }
    ],
    "groups": [
      {
        "tripGroupId": "11111111-1111-1111-1111-111111111111",
        "groupName": "Nhóm 1",
        "status": "Draft",
        "guide": {
          "userId": "22222222-2222-2222-2222-222222222222",
          "fullName": "Trần Minh Guide"
        },
        "maxParticipants": 12,
        "overflowSlots": 2,
        "participantCount": 11,
        "remainingStandardSlots": 1,
        "remainingOverflowSlots": 2,
        "isOverflowing": false,
        "bookings": [
          {
            "bookingOrderId": "33333333-3333-3333-3333-333333333333",
            "orderCode": "RT-20261031-001",
            "participantCount": 3,
            "isSplit": false,
            "participants": [
              {
                "participantId": "44444444-4444-4444-4444-444444444444",
                "fullName": "Nguyễn Văn A"
              }
            ]
          }
        ]
      }
    ],
    "unassignedBookings": [
      {
        "bookingOrderId": "55555555-5555-5555-5555-555555555555",
        "orderCode": "RT-20261031-002",
        "participantCount": 3,
        "participants": [
          {
            "participantId": "66666666-6666-6666-6666-666666666666",
            "fullName": "Lê Văn B"
          }
        ]
      }
    ]
  },
  "errors": null,
  "timestamp": "..."
}
```

FE dùng `publishBlockers[].code` để khóa nút **Chốt**, không tự suy luận chỉ từ số đếm.

### 8.2. Gán hoặc bỏ guide

`PUT /trips/{tripId}/groups/{groupId}/guide` · **Auth:** Staff/Admin

```json
{
  "guideUserId": "22222222-2222-2222-2222-222222222222",
  "expectedTripRowVersion": 7
}
```

Gửi `guideUserId: null` để bỏ guide khi group còn `Draft`. Một guide chỉ phụ trách một group trong cùng Trip và không được có Trip khác giao thời gian. Thành công trả `200` với `data` là snapshot mục 8.1 mới nhất.

### 8.3. Chuyển nguyên booking

`PUT /trips/{tripId}/bookings/{bookingOrderId}/group` · **Auth:** Staff/Admin

```json
{
  "targetGroupId": "11111111-1111-1111-1111-111111111111",
  "expectedTripRowVersion": 8
}
```

- Chuyển mọi participant hợp lệ của booking sang cùng group.
- Staff được dùng overflow, nhưng tổng người không vượt `maxParticipants + overflowSlots`.
- Chỉ áp dụng khi group còn `Draft`. Thành công trả snapshot mới nhất.

### 8.4. Tách booking

`POST /trips/{tripId}/bookings/{bookingOrderId}/split` · **Auth:** Staff/Admin

```json
{
  "reason": "Không còn group nào đủ chỗ cho toàn bộ booking",
  "expectedTripRowVersion": 9,
  "assignments": [
    {
      "tripGroupId": "11111111-1111-1111-1111-111111111111",
      "participantIds": ["44444444-4444-4444-4444-444444444444"]
    },
    {
      "tripGroupId": "77777777-7777-7777-7777-777777777777",
      "participantIds": ["88888888-8888-8888-8888-888888888888"]
    }
  ]
}
```

`reason` bắt buộc. Mỗi participant hợp lệ của booking phải xuất hiện đúng một lần; mọi group phải thuộc cùng Trip và còn trong giới hạn gồm overflow. Thành công trả snapshot mới nhất.

### 8.4b. Tính lại nhu cầu đồ cấp chuyến

`POST /trips/{tripId}/grouping/recalculate-provided-items` · **Auth:** Staff/Admin

Khi thay đổi số group, chuyển/tách booking hoặc hủy booking, BE đặt `providedItemsNeedsRecalc = true` và thêm blocker `EQUIPMENT_REQUIREMENTS_STALE` vào `publishBlockers`. Staff gọi endpoint này để tính lại nhu cầu đồ cấp chuyến (`PER_GROUP` theo số group có người đi và `PER_PARTICIPANT` theo tổng số participant hợp lệ) và xóa cờ stale.

**Response `200 OK`** trả `RecalculateTripProvidedItemsResponse` gồm `{ tripId, groupCount, activeGroupCount, activeParticipantCount, providedItemsNeedsRecalc: false, rowVersion, items: [{ productVariantId, sku, variantName, scope, quantityPerUnit, requiredQuantity }] }`. Snapshot mục 8.1 cũng trả mảng `providedItems` hiện hành.

### 8.5. Chốt group

`POST /trips/{tripId}/grouping/publish` · **Auth:** Staff/Admin

```json
{
  "expectedTripRowVersion": 10
}
```

Chỉ thành công khi:

- Trip là `ClosedForBooking` và không còn hold thanh toán hợp lệ.
- Không còn participant hợp lệ chưa xếp.
- Mọi group có người đều có guide.
- Nhu cầu đồ cấp chuyến đã được cập nhật.

Thành công đổi group sang `Published` và trả snapshot mới nhất. Group `Published` không nhận auto-assign, chuyển hoặc tách bằng API thường.

### 8.6. Khách xem group của booking

`GET /trip-bookings/{orderId}/grouping` · **Auth:** User

Trước khi chốt, API trả `200` với `groupingStatus = "Pending"` và `groups = []`. Sau khi chốt:

- Chủ Order thấy participant thuộc chính booking của họ, có thể nằm ở nhiều group nếu Staff đã tách.
- Người đi cùng đã liên kết chỉ thấy participant gắn với chính tài khoản họ.
- Không trả participant của booking khác.

**API bổ sung cho màn danh sách của Guide (`8.6b`):**

- `GET /guide/trip-groups` · **Auth:** Guide (`TourGuide` / `Guide`) → danh sách tất cả group (cả `Draft` và `Published`) đang được gán cho guide đăng nhập.
- `GET /guide/trip-groups/{groupId}` · **Auth:** Guide → chi tiết 1 group được giao kèm danh sách participant hợp lệ (bị đổi guide trả `404`).

```json
{
  "success": true,
  "statusCode": 200,
  "message": "Lấy nhóm chuyến đi thành công",
  "data": {
    "tripId": "00000000-0000-0000-0000-000000000000",
    "groupingStatus": "Published",
    "groups": [
      {
        "tripGroupId": "11111111-1111-1111-1111-111111111111",
        "groupName": "Nhóm 1",
        "guide": {
          "userId": "22222222-2222-2222-2222-222222222222",
          "fullName": "Trần Minh Guide"
        },
        "participants": [
          {
            "participantId": "44444444-4444-4444-4444-444444444444",
            "fullName": "Nguyễn Văn A"
          }
        ]
      }
    ]
  },
  "errors": null,
  "timestamp": "..."
}
```

### 8.7. Lỗi nghiệp vụ của TripGroup

| `errors.code` | HTTP | Khi nào | Hành vi FE |
|---|---|---|---|
| `TRIP_GROUP_COUNT_LOCKED` | 409 | Sửa số group sau khi Trip đã `Open` | Tải lại Trip; chuyển form sang chỉ đọc |
| `TRIP_OPERATIONAL_CAPACITY_INSUFFICIENT` | 409 | Mở bán khi tổng sức chứa gồm overflow nhỏ hơn `totalSlots` | Hiển thị blocker trên form Trip |
| `GROUP_CAPACITY_EXCEEDED` | 409 | Chuyển/tách làm group vượt cả overflow | Giữ nguyên UI; hiển thị sức chứa mới nhất |
| `GROUP_WRONG_TRIP` | 400 | Group hoặc participant không thuộc Trip | Tải lại snapshot; báo dữ liệu không hợp lệ |
| `GUIDE_SCHEDULE_CONFLICT` | 409 | Guide trùng group hoặc trùng thời gian Trip | Giữ modal; yêu cầu chọn guide khác |
| `TRIP_GROUPING_LOCKED` | 409 | Sửa group đã `Published` | Tải lại snapshot; khóa thao tác |
| `TRIP_NOT_CLOSED_FOR_BOOKING` | 409 | Chốt khi Trip chưa đóng bán | Hiển thị trong `publishBlockers` |
| `ACTIVE_PAYMENT_HOLDS` | 409 | Chốt khi còn hold hợp lệ | Hiển thị trong `publishBlockers` |
| `UNASSIGNED_PARTICIPANTS` | 409 | Chốt khi còn người chưa xếp | Cuộn/focus tới mục **Chưa xếp** |
| `GROUP_GUIDE_REQUIRED` | 409 | Group có người nhưng chưa có guide | Đánh dấu group thiếu guide |
| `EQUIPMENT_REQUIREMENTS_STALE` | 409 | Đồ cấp chưa được tính lại | Liên kết tới checklist chuẩn bị |
| `CONCURRENCY_CONFLICT` | 409 | `expectedTripRowVersion` cũ | Tải snapshot mới; không tự gửi lại mutation |

---

## 9. Admin Catalog

### 9.1. Danh sách Tour vận hành

`GET /admin/tours` · **Auth:** Admin / Staff

Query: `pageNumber`, `pageSize`, `search`, `status` (`Draft`, `Published`, `Archived`), `categoryId`, `locationId`.
Mặc định chỉ trả phiên bản mới nhất của mỗi `tourFamilyId`. Dữ liệu `Active` cũ được trả thành `Published`; `Expired` được trả thành `Archived`.

```json
{
  "items": [
    {
      "tourId": "44444444-5555-6666-7777-888888888888",
      "tourFamilyId": "44444444-0000-0000-0000-888888888888",
      "name": "Chinh phục Tà Năng - Phan Dũng",
      "categoryName": "Trekking Núi cao",
      "mainLocationName": "Lâm Đồng - Bình Thuận",
      "durationDays": 3,
      "durationNights": 2,
      "basePrice": 3200000,
      "version": 2,
      "isItineraryLocked": true,
      "status": "Published",
      "primaryImageUrl": "https://cdn.example.com/tours/cover.jpg",
      "upcomingTripsCount": 5
    }
  ],
  "pageNumber": 1,
  "pageSize": 10,
  "totalItems": 1,
  "totalPages": 1,
  "hasPrevious": false,
  "hasNext": false
}
```

### 9.2. Danh sách sản phẩm vận hành

`GET /admin/products` · **Auth:** Admin / Staff

Query: `pageNumber`, `pageSize`, `search` (tên hoặc SKU), `status` (`Active`, `Inactive`), `categoryId`.
`availableAssetsCount` chỉ đếm asset chưa xóa có `assetStatus = Available`.

```json
{
  "items": [
    {
      "productId": "99990000-1111-2222-3333-444455556666",
      "name": "Lều cắm trại chống nước 4 người",
      "categoryName": "Lều trại",
      "primaryImageUrl": "https://cdn.example.com/products/tent.jpg",
      "variantsCount": 3,
      "totalAssetsCount": 20,
      "availableAssetsCount": 14,
      "status": "Active",
      "createdAt": "2026-04-10T00:00:00Z"
    }
  ],
  "pageNumber": 1,
  "pageSize": 10,
  "totalItems": 1,
  "totalPages": 1,
  "hasPrevious": false,
  "hasNext": false
}
```

---

## Phụ lục: Enum

Enum được serialize dạng **string**.

| Enum | Giá trị | Mô tả |
|---|---|---|
| `UserRole` | `TODO` | TODO |
| `OrderType` | `Rental`, `TripBooking` | Loại đơn: thuê thiết bị / booking chuyến đi |
| `OrderStatus` | `Draft`, `PendingPayment`, `Confirmed`, `InFulfillment`, `Completed`, `Expired`, `Cancelled` | Trạng thái đơn. Đơn thuê mới tạo là `PendingPayment` |
| `OrderPaymentStatus` | `Unpaid`, `Paid`, `PartiallyRefunded`, `Refunded`, `NeedsReconciliation` | Trạng thái thanh toán của đơn (tách riêng với `status`) |
| `TripGroupStatus` | `Draft`, `Published` | Group nháp nội bộ / đã chốt cho khách |
| `GroupingStatus` | `Draft`, `Published`, `Pending` | Snapshot Staff/Guide dùng `Draft`/`Published`; response khách dùng `Pending` trước khi chốt |
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

FE xử lý theo `errors.code`, không so khớp `message`. Lỗi validation theo field (400) chỉ có key theo field, không có `code`. Các mã dưới đây được dùng bởi `/bundles`, `/rental-availability`, `/rental-orders*`; lỗi TripGroup nằm tại [mục 8.7](#87-lỗi-nghiệp-vụ-của-tripgroup).

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
| 2026-10-31 | Antigravity | `/trips/{tripId}/grouping*`, `/trips/{tripId}/groups/*`, `/trips/{tripId}/bookings/*`, `/trip-bookings/{orderId}/grouping`, `/trip-bookings/{orderId}/cancel`, `/tours/{id}/group-settings`, `/tours/{id}/recurrence-rules`, `/trips*`, `/guide/trip-groups*` | Hoàn thiện triển khai BE cho Chia TripGroup (Section 7): migration DB, tự xếp sau webhook SePay, quản trị cấu hình group/lịch lặp, snapshot Staff/Guide, gán guide chống trùng lịch, chuyển/tách booking, tính lại đồ cấp chuyến, chốt group, hủy booking giữ lịch sử và lọc hiển thị theo quyền |
| 2026-10-31 | Codex | `/trips/{tripId}/grouping*`, `/trips/{tripId}/groups/*`, `/trips/{tripId}/bookings/*`, `/trip-bookings/{orderId}/grouping` | Thêm contract dự kiến cho FE: cấu hình số group, snapshot Staff/Guide, gán guide, chuyển/tách booking, chốt và Customer xem group Published theo quyền |
| 2026-10-30 | — | `/auth/verify-email`, `/auth/resend-verification-email`, `/users/me/orders`, `/trip-bookings*` | Chuyển đặt chuyến nhóm sang gán theo email (`contactPhone`, `buyerParticipates` đặt giùm, `email`/`phone` tuỳ chọn, tự động gán `linkedUserId` khi webhook SePay hoặc xác thực email, sửa participant `PUT /{orderId}/participants/{participantId}`, link chỉ đọc `preview` kèm `participantNames` & `activities` đến `tripEndAt`; gỡ `claim`, `participant-invitations` và `DELETE .../link`) |
| 2026-10-27 | — | `/bundles*`, `/rental-availability`, `/rental-orders*`, `/users/me/orders` | Thêm danh sách / chi tiết combo, kiểm tra khả dụng, báo giá, tạo (Idempotency-Key trên `orders.idempotency_key`) và xem đơn thuê, đơn hàng của tôi; thêm enum `OrderType`, `OrderStatus`, `OrderPaymentStatus`, `AssignmentStatus`, `RentalItemType` và bảng mã lỗi nghiệp vụ `errors.code` |
| 2026-10-26 | Codex | `/trip-bookings/*` | Thêm tạo booking nhóm, xem theo quyền, tạo và nhận lời mời participant |
| 2026-10-06 | — | — | Khởi tạo skeleton |
| 2026-10-23 | — | Products | Thêm API danh sách, chi tiết và danh mục cho sản phẩm thuê / bán |
