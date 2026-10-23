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
- [3. Products](#3-products)
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
| 3.1 | Products | `GET` | `/products` | Public | Danh sách sản phẩm thuê / bán | ✅ Done |
| 3.2 | Products | `GET` | `/products/{id}` | Public | Chi tiết sản phẩm, biến thể, ảnh, giá | ✅ Done |
| 3.3 | Products | `GET` | `/products/categories` | Public | Danh mục lọc sản phẩm | ✅ Done |

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

## 3. Products

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

### 3.1. Danh sách sản phẩm

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

### 3.2. Chi tiết sản phẩm

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

### 3.3. Danh mục sản phẩm

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
| 2026-10-06 | — | — | Khởi tạo skeleton |
| 2026-10-23 | — | Products | Thêm API danh sách, chi tiết và danh mục cho sản phẩm thuê / bán |
