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
| 1.1 | Auth | `POST` | `/auth/register` | Public | Đăng ký tài khoản Customer mới | ✅ Done |
| 1.2 | Auth | `POST` | `/auth/login` | Public | Đăng nhập hệ thống (cấp Access + Refresh Token) | ✅ Done |
| 1.3 | Auth | `POST` | `/auth/refresh-token` | Public | Xoay vòng Refresh Token (Token Rotation) | ✅ Done |
| 1.4 | Auth | `POST` | `/auth/logout` | User | Đăng xuất và thu hồi token tức thì | ✅ Done |
| 1.5 | Auth | `POST` | `/auth/forgot-password` | Public | Yêu cầu mã OTP khôi phục mật khẩu qua email | ✅ Done |
| 1.6 | Auth | `POST` | `/auth/reset-password` | Public | Đặt lại mật khẩu bằng mã OTP 6 số | ✅ Done |
| 1.7 | Auth | `POST` | `/auth/google` | Public | Đăng nhập / Đăng ký nhanh bằng Google OAuth 2.0 | ✅ Done |
| 2.1 | Users | `GET` | `/users/me` | User | Lấy thông tin cá nhân và hồ sơ chi tiết | ✅ Done |
| 2.2 | Users | `PUT` | `/users/me` | User | Cập nhật hồ sơ cá nhân (Họ tên, SĐT, Địa chỉ, Bio) | ✅ Done |
| 2.3 | Users | `POST` | `/users/me/avatar` | User | Tải lên ảnh đại diện cá nhân (Multipart Form) | ✅ Done |
| 2.4 | Users | `PUT` | `/users/me/password` | User | Đổi mật khẩu (Thu hồi phiên thiết bị khác - Phương án A) | ✅ Done |
| 2.5 | Users | `GET` | `/users/me/sessions` | User | Danh sách phiên thiết bị đang hoạt động | ✅ Done |
| 2.6 | Users | `DELETE` | `/users/me/sessions/{familyId}` | User | Thu hồi một phiên thiết bị chỉ định | ✅ Done |
| 2.7 | Users | `POST` | `/users/me/sessions/revoke-others` | User | Đăng xuất khỏi tất cả các thiết bị khác | ✅ Done |
| 2.8 | Users | `GET` | `/users` | Admin | Danh sách user | ⬜ TODO |
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
| `email` | `string` | ✔ | Email hợp lệ, max 255 | Email đăng nhập |
| `password` | `string` | ✔ | Min 8 ký tự, 1 hoa, 1 thường, 1 số, 1 ký tự đặc biệt | Mật khẩu |
| `confirmPassword` | `string` | ✔ | Trùng khớp với `password` | Xác nhận mật khẩu |
| `fullName` | `string` | ✔ | 2-100 ký tự | Họ và tên |
| `phone` | `string` | ✘ | 10 số đầu số VN hợp lệ | Số điện thoại |

```json
{
  "email": "trekker@rentrek.com",
  "password": "Trekker@2026",
  "confirmPassword": "Trekker@2026",
  "fullName": "Nguyễn Văn Trek",
  "phone": "0987654321"
}
```

**Response `201 Created`**

```json
{
  "success": true,
  "statusCode": 201,
  "message": "Đăng ký tài khoản thành công",
  "data": {
    "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "refreshToken": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "accessTokenExpiresAt": "2026-10-30T22:30:00Z",
    "user": {
      "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "email": "trekker@rentrek.com",
      "fullName": "Nguyễn Văn Trek",
      "role": "Customer",
      "avatarUrl": null
    }
  },
  "errors": null,
  "timestamp": "2026-10-30T22:00:00Z"
}
```

**Lỗi**

| Status | Khi nào | Message |
|---|---|---|
| 400 | Dữ liệu không hợp lệ (sai định dạng, mật khẩu yếu, confirm không khớp) | Dữ liệu không hợp lệ |
| 409 | Email đã được sử dụng | Email đã tồn tại trong hệ thống |

---

### 1.2. Đăng nhập

`POST /auth/login` · **Auth:** Public

**Request body**

| Field | Type | Required | Validation | Mô tả |
|---|---|---|---|---|
| `email` | `string` | ✔ | Email hợp lệ | Email đăng nhập |
| `password` | `string` | ✔ | Không để trống | Mật khẩu |

```json
{
  "email": "trekker@rentrek.com",
  "password": "Trekker@2026"
}
```

**Response `200 OK`**

```json
{
  "success": true,
  "statusCode": 200,
  "message": "Đăng nhập thành công",
  "data": {
    "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "refreshToken": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "accessTokenExpiresAt": "2026-10-30T22:30:00Z",
    "user": {
      "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "email": "trekker@rentrek.com",
      "fullName": "Nguyễn Văn Trek",
      "role": "Customer",
      "avatarUrl": null
    }
  },
  "errors": null,
  "timestamp": "2026-10-30T22:00:00Z"
}
```

**Lỗi**

| Status | Khi nào | Message |
|---|---|---|
| 400 | Thiếu thông tin đăng nhập | Dữ liệu không hợp lệ |
| 401 | Sai email hoặc mật khẩu | Email hoặc mật khẩu không chính xác |
| 403 | Tài khoản bị vô hiệu hóa | Tài khoản đã bị khóa |

---

### 1.3. Làm mới token (Token Rotation)

`POST /auth/refresh-token` · **Auth:** Public

**Request body**

| Field | Type | Required | Validation | Mô tả |
|---|---|---|---|---|
| `refreshToken` | `string` | ✔ | Không để trống | Refresh Token hiện tại |

```json
{
  "refreshToken": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```

**Response `200 OK`** — Trả về cặp Token mới (Access Token + Refresh Token mới nối tiếp Family).

```json
{
  "success": true,
  "statusCode": 200,
  "message": "Làm mới phiên đăng nhập thành công",
  "data": {
    "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "refreshToken": "4ab96g75-6828-5673-c4gd-3d074g77bgb7",
    "accessTokenExpiresAt": "2026-10-30T23:00:00Z",
    "user": {
      "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "email": "trekker@rentrek.com",
      "fullName": "Nguyễn Văn Trek",
      "role": "Customer",
      "avatarUrl": null
    }
  },
  "errors": null,
  "timestamp": "2026-10-30T22:30:00Z"
}
```

**Lỗi**

| Status | Khi nào | Message |
|---|---|---|
| 401 | Token không tồn tại, hết hạn hoặc bị thu hồi (Token Reuse Detection thu hồi toàn bộ Family) | Phiên đăng nhập không hợp lệ hoặc đã hết hạn |

---

### 1.4. Đăng xuất

`POST /auth/logout` · **Auth:** User

**Request body**

| Field | Type | Required | Validation | Mô tả |
|---|---|---|---|---|
| `refreshToken` | `string` | ✔ | Không để trống | Refresh Token cần thu hồi |

```json
{
  "refreshToken": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```

**Response `200 OK`**

```json
{
  "success": true,
  "statusCode": 200,
  "message": "Đăng xuất thành công",
  "data": null,
  "errors": null,
  "timestamp": "2026-10-30T22:30:00Z"
}
```

**Lỗi**

| Status | Khi nào | Message |
|---|---|---|
| 401 | Chưa đăng nhập hoặc Access Token không hợp lệ | Bạn chưa đăng nhập |

---

### 1.5. Yêu cầu OTP quên mật khẩu

`POST /auth/forgot-password` · **Auth:** Public

**Request body**

| Field | Type | Required | Validation | Mô tả |
|---|---|---|---|---|
| `email` | `string` | ✔ | Email hợp lệ | Email tài khoản cần đặt lại mật khẩu |

```json
{
  "email": "trekker@rentrek.com"
}
```

**Response `200 OK`** (Áp dụng Email Enumeration Prevention — luôn trả 200 OK).

```json
{
  "success": true,
  "statusCode": 200,
  "message": "Nếu email tồn tại trong hệ thống, mã xác thực OTP đã được gửi thành công",
  "data": null,
  "errors": null,
  "timestamp": "2026-10-30T22:35:00Z"
}
```

---

### 1.6. Đặt lại mật khẩu bằng OTP

`POST /auth/reset-password` · **Auth:** Public

**Request body**

| Field | Type | Required | Validation | Mô tả |
|---|---|---|---|---|
| `email` | `string` | ✔ | Email hợp lệ | Email tài khoản |
| `otp` | `string` | ✔ | 6 chữ số | Mã OTP nhận qua email (hạn 15 phút) |
| `newPassword` | `string` | ✔ | Min 8 ký tự, hoa, thường, số, ký tự đặc biệt | Mật khẩu mới |
| `confirmPassword` | `string` | ✔ | Khớp `newPassword` | Xác nhận mật khẩu mới |

```json
{
  "email": "trekker@rentrek.com",
  "otp": "123456",
  "newPassword": "NewTrekker@2026",
  "confirmPassword": "NewTrekker@2026"
}
```

**Response `200 OK`** (Sau khi thành công, toàn bộ phiên đăng nhập trên mọi thiết bị sẽ bị thu hồi).

```json
{
  "success": true,
  "statusCode": 200,
  "message": "Đặt lại mật khẩu thành công. Vui lòng đăng nhập bằng mật khẩu mới",
  "data": null,
  "errors": null,
  "timestamp": "2026-10-30T22:40:00Z"
}
```

**Lỗi**

| Status | Khi nào | Message |
|---|---|---|
| 400 | Sai mã OTP hoặc mã đã hết hạn (> 15 phút) / đã sử dụng | Mã xác thực OTP không chính xác hoặc đã hết hạn |

---

### 1.7. Đăng nhập bằng Google OAuth 2.0

`POST /auth/google` · **Auth:** Public

**Request body**

| Field | Type | Required | Validation | Mô tả |
|---|---|---|---|---|
| `idToken` | `string` | ✔ | Không để trống | Google ID Token (hỗ trợ Dev Mock Token) |

```json
{
  "idToken": "eyJhbGciOiJSUzI1NiIsImtpZCI6IjEyMzQ1NiJ9..."
}
```

**Response `200 OK`**

```json
{
  "success": true,
  "statusCode": 200,
  "message": "Đăng nhập Google thành công",
  "data": {
    "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "refreshToken": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "accessTokenExpiresAt": "2026-10-30T23:30:00Z",
    "user": {
      "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "email": "google.trekker@rentrek.com",
      "fullName": "Google Trekker",
      "role": "Customer",
      "avatarUrl": "https://lh3.googleusercontent.com/a/default-user"
    }
  },
  "errors": null,
  "timestamp": "2026-10-30T22:45:00Z"
}
```

---

## 2. Users

### 2.1. Lấy thông tin cá nhân

`GET /users/me` · **Auth:** User

**Response `200 OK`**

```json
{
  "success": true,
  "statusCode": 200,
  "message": "Lấy thông tin tài khoản thành công",
  "data": {
    "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "email": "trekker@rentrek.com",
    "fullName": "Nguyễn Văn Trek",
    "phone": "0987654321",
    "role": "Customer",
    "status": "Active",
    "avatarUrl": "/uploads/avatars/avatar-3fa85f64.webp",
    "address": "123 Đường Leo Núi, Phường Bến Nghé, Quận 1, TP.HCM",
    "bio": "Đam mê cắm trại cuối tuần và trekking khám phá",
    "dateOfBirth": "2000-01-15",
    "userCode": "KH-00101",
    "createdAt": "2026-10-23T19:00:00Z",
    "updatedAt": "2026-10-30T21:00:00Z"
  },
  "errors": null,
  "timestamp": "2026-10-30T22:00:00Z"
}
```

**Lỗi**

| Status | Khi nào | Message |
|---|---|---|
| 401 | Chưa đăng nhập hoặc token hết hạn | Phiên đăng nhập không hợp lệ hoặc đã hết hạn |
| 404 | User không còn tồn tại | Không tìm thấy thông tin tài khoản người dùng |

---

### 2.2. Cập nhật thông tin cá nhân

`PUT /users/me` · **Auth:** User

**Request body**

| Field | Type | Required | Validation | Mô tả |
|---|---|---|---|---|
| `fullName` | `string` | ✔ | 2-100 ký tự | Họ và tên |
| `phone` | `string` | ✘ | 10 số đầu số VN hợp lệ | Số điện thoại |
| `address` | `string` | ✘ | Max 500 ký tự | Địa chỉ |
| `bio` | `string` | ✘ | Max 1000 ký tự | Giới thiệu bản thân |
| `dateOfBirth` | `string` | ✘ | Định dạng DateOnly (YYYY-MM-DD) | Ngày tháng năm sinh |

```json
{
  "fullName": "Nguyễn Văn Trek (Updated)",
  "phone": "0912345678",
  "address": "456 Đường Trekking, Phường Bến Nghé, Quận 1, TP.HCM",
  "bio": "Chuyên gia cắm trại và sinh tồn vùng hoang dã",
  "dateOfBirth": "2000-01-15"
}
```

**Response `200 OK`**

```json
{
  "success": true,
  "statusCode": 200,
  "message": "Cập nhật thông tin cá nhân thành công",
  "data": {
    "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "email": "trekker@rentrek.com",
    "fullName": "Nguyễn Văn Trek (Updated)",
    "phone": "0912345678",
    "role": "Customer",
    "status": "Active",
    "avatarUrl": "/uploads/avatars/avatar-3fa85f64.webp",
    "address": "456 Đường Trekking, Phường Bến Nghé, Quận 1, TP.HCM",
    "bio": "Chuyên gia cắm trại và sinh tồn vùng hoang dã",
    "dateOfBirth": "2000-01-15",
    "userCode": "KH-00101",
    "createdAt": "2026-10-23T19:00:00Z",
    "updatedAt": "2026-10-30T22:15:00Z"
  },
  "errors": null,
  "timestamp": "2026-10-30T22:15:00Z"
}
```

---

### 2.3. Tải lên ảnh đại diện

`POST /users/me/avatar` · **Auth:** User · **Content-Type:** `multipart/form-data`

**Form data**

| Field | Type | Required | Validation | Mô tả |
|---|---|---|---|---|
| `file` | `file` | ✔ | Định dạng `.jpg`, `.jpeg`, `.png`, `.webp`; Tối đa 2MB | File ảnh avatar |

**Response `200 OK`**

```json
{
  "success": true,
  "statusCode": 200,
  "message": "Tải lên ảnh đại diện thành công",
  "data": {
    "avatarUrl": "/uploads/avatars/avatar-3fa85f64-5717-4562-b3fc-2c963f66afa6-a1b2c3d4.webp"
  },
  "errors": null,
  "timestamp": "2026-10-30T22:20:00Z"
}
```

**Lỗi**

| Status | Khi nào | Message |
|---|---|---|
| 400 | File rỗng, không đúng định dạng hoặc vượt quá 2MB | Dung lượng file ảnh vượt quá giới hạn cho phép (tối đa 2MB) |

---

### 2.4. Đổi mật khẩu

`PUT /users/me/password` · **Auth:** User

Áp dụng **Phương án A**: Tự động thu hồi phiên đăng nhập trên các thiết bị khác, giữ nguyên phiên thiết bị hiện tại.

**Request body**

| Field | Type | Required | Validation | Mô tả |
|---|---|---|---|---|
| `currentPassword` | `string` | ✔ | Khớp mật khẩu đang dùng | Mật khẩu hiện tại |
| `newPassword` | `string` | ✔ | Min 8 ký tự, hoa, thường, số, ký tự đặc biệt, khác mật khẩu cũ | Mật khẩu mới |
| `confirmNewPassword` | `string` | ✔ | Khớp `newPassword` | Xác nhận mật khẩu mới |

```json
{
  "currentPassword": "Trekker@2026",
  "newPassword": "NewStrongPass@2026!",
  "confirmNewPassword": "NewStrongPass@2026!"
}
```

**Response `200 OK`**

```json
{
  "success": true,
  "statusCode": 200,
  "message": "Đổi mật khẩu thành công. Các phiên đăng nhập trên thiết bị khác đã được đăng xuất",
  "data": null,
  "errors": null,
  "timestamp": "2026-10-30T22:25:00Z"
}
```

---

### 2.5. Danh sách phiên đăng nhập

`GET /users/me/sessions` · **Auth:** User

**Response `200 OK`** — Danh sách các phiên đăng nhập đang hoạt động theo Family:

```json
{
  "success": true,
  "statusCode": 200,
  "message": "Lấy danh sách phiên làm việc thành công",
  "data": [
    {
      "familyId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "deviceInfo": "Windows • Chrome",
      "ipAddress": "127.0.0.1",
      "createdAt": "2026-10-30T20:00:00Z",
      "lastUsedAt": "2026-10-30T22:30:00Z",
      "isCurrent": true
    },
    {
      "familyId": "7c8e9f0a-1b2c-3d4e-5f6a-7b8c9d0e1f2a",
      "deviceInfo": "iPhone • Mobile Safari",
      "ipAddress": "14.161.20.15",
      "createdAt": "2026-10-29T15:00:00Z",
      "lastUsedAt": "2026-10-30T10:00:00Z",
      "isCurrent": false
    }
  ],
  "errors": null,
  "timestamp": "2026-10-30T22:30:00Z"
}
```

---

### 2.6. Thu hồi một phiên thiết bị

`DELETE /users/me/sessions/{familyId}` · **Auth:** User

**Path params**

| Param | Type | Mô tả |
|---|---|---|
| `familyId` | `guid` | Định danh chuỗi phiên thiết bị cần thu hồi |

**Response `200 OK`**

```json
{
  "success": true,
  "statusCode": 200,
  "message": "Thu hồi phiên làm việc thành công",
  "data": null,
  "errors": null,
  "timestamp": "2026-10-30T22:35:00Z"
}
```

---

### 2.7. Đăng xuất tất cả thiết bị khác

`POST /users/me/sessions/revoke-others` · **Auth:** User

Thu hồi toàn bộ phiên đăng nhập ngoại trừ phiên `familyId` tương ứng với Access Token hiện tại.

**Response `200 OK`**

```json
{
  "success": true,
  "statusCode": 200,
  "message": "Đã đăng xuất khỏi tất cả các thiết bị khác",
  "data": null,
  "errors": null,
  "timestamp": "2026-10-30T22:35:00Z"
}
```

---

### 2.8. Danh sách user

`GET /users` · **Auth:** Admin

**Query params**

| Param | Type | Required | Default | Mô tả |
|---|---|---|---|---|
| `pageNumber` | `int` | ✘ | `1` | Số trang |
| `pageSize` | `int` | ✘ | `10` | Kích thước trang |
| `search` | `string` | ✘ | | Tìm theo email hoặc họ tên |

**Response `200 OK`** — `data` là Paged data:

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
| 2026-10-30 | Ông Gia Huy | `/auth/*`, `/users/*` | Hoàn thành toàn bộ API Auth, Session Security, Upload Avatar, Forgot Password OTP và Google OAuth 2.0 |
| 2026-10-26 | Codex | `/trip-bookings/*` | Thêm tạo booking nhóm, xem theo quyền, tạo và nhận lời mời participant |
| 2026-10-06 | — | — | Khởi tạo skeleton |
| 2026-10-23 | — | Products | Thêm API danh sách, chi tiết và danh mục cho sản phẩm thuê / bán |
