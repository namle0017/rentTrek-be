# 01 — Coding Conventions & Workflow

> Tài liệu quy định chung cho team Backend **renTrek** (ASP.NET Core Web API).
> Mọi thành viên **bắt buộc** tuân theo. Khi cần thay đổi rule, tạo PR sửa file này và thông báo cho cả team.

## Mục lục

1. [Naming Convention](#1-naming-convention)
2. [Flow xử lý Request → Response](#2-flow-xử-lý-request--response)
3. [Cấu trúc Request](#3-cấu-trúc-request)
4. [Cấu trúc Response](#4-cấu-trúc-response)
5. [Git: Branch, Commit, Push](#5-git-branch-commit-push)
6. [Quy trình bắt đầu code một tính năng](#6-quy-trình-bắt-đầu-code-một-tính-năng)
7. [Checklist trước khi tạo PR](#7-checklist-trước-khi-tạo-pr)

---

## 1. Naming Convention

### 1.1. C# code

| Thành phần | Quy tắc | Ví dụ |
|---|---|---|
| Namespace | `PascalCase`, theo cấu trúc thư mục | `RenTrek.BLL.Services.Implementations` |
| Class / Record / Struct | `PascalCase`, danh từ | `ProductService`, `BookingRepository` |
| Interface | Tiền tố `I` + `PascalCase` | `IProductService`, `IGenericRepository<T>` |
| Method | `PascalCase`, động từ | `GetByIdAsync`, `CreateBooking` |
| Method async | Hậu tố `Async` | `GetAllAsync`, `SaveChangesAsync` |
| Property (public) | `PascalCase` | `FullName`, `CreatedAt` |
| Field private | `_camelCase` | `_productRepository`, `_unitOfWork` |
| Biến local, parameter | `camelCase` | `productId`, `pageSize` |
| Constant | `PascalCase` | `DefaultPageSize`, `MaxUploadSizeMb` |
| Enum | `PascalCase`, **số ít** | `BookingStatus.Pending`, `UserRole.Admin` |
| Boolean | Tiền tố `Is` / `Has` / `Can` | `IsActive`, `HasPaid`, `CanCancel` |
| Generic type | Tiền tố `T` | `TEntity`, `TResponse` |

**Quy tắc chung:**

- Mỗi file chỉ chứa **1 class / interface / enum** chính; tên file trùng tên class.
- Không viết tắt khó hiểu (`usr`, `prd`, `qty`...). Chấp nhận các từ viết tắt phổ biến: `Id`, `Dto`, `Url`, `Api`.
- Từ viết tắt ≥ 3 ký tự viết như từ thường: `HttpClient`, `JsonOptions` (không phải `HTTPClient`).

### 1.2. Theo từng layer

| Loại | Pattern | Ví dụ |
|---|---|---|
| Controller | `{Resource số nhiều}Controller` | `ProductsController`, `BookingsController` |
| Service interface / impl | `I{Entity}Service` / `{Entity}Service` | `IProductService` / `ProductService` |
| Repository interface / impl | `I{Entity}Repository` / `{Entity}Repository` | `IProductRepository` / `ProductRepository` |
| Entity | `{Entity}` (số ít) | `Product`, `Booking`, `User` |
| Request DTO | `{Action}{Entity}Request` | `CreateProductRequest`, `UpdateProductRequest` |
| Query DTO (filter/paging) | `{Entity}QueryRequest` | `ProductQueryRequest` |
| Response DTO | `{Entity}Response` / `{Entity}DetailResponse` | `ProductResponse`, `ProductDetailResponse` |
| Validator (FluentValidation) | `{RequestName}Validator` | `CreateProductRequestValidator` |
| Custom Exception | `{Mô tả}Exception` | `NotFoundException`, `BadRequestException` |
| Middleware | `{Chức năng}Middleware` | `ExceptionHandlingMiddleware` |
| EF Configuration | `{Entity}Configuration` | `ProductConfiguration` |
| Mapping profile | `{Entity}Profile` / `{Entity}Mapper` | `ProductProfile` |

### 1.3. API Endpoint (URL)

- Prefix: `/api/v{version}/` → ví dụ `/api/v1/products`.
- Resource là **danh từ số nhiều**, **kebab-case**, chữ thường: `/api/v1/rental-orders`.
- **Không** dùng động từ trong URL (HTTP method đã thể hiện hành động).
  - ✅ `POST /api/v1/products` — ❌ `POST /api/v1/products/create`
- Resource lồng nhau tối đa **2 cấp**: `/api/v1/users/{userId}/bookings`.
- Hành động đặc biệt không thuộc CRUD dùng sub-resource động từ (ngoại lệ có kiểm soát):
  `POST /api/v1/bookings/{id}/cancel`, `POST /api/v1/auth/login`.
- Path param: `camelCase` trong route template → `{productId}` hoặc `{id}`.
- Query param: `camelCase` → `?pageNumber=1&pageSize=10&sortBy=createdAt&sortOrder=desc`.

| Method | Mục đích | Ví dụ |
|---|---|---|
| `GET` | Lấy danh sách / chi tiết | `GET /api/v1/products`, `GET /api/v1/products/{id}` |
| `POST` | Tạo mới | `POST /api/v1/products` |
| `PUT` | Cập nhật toàn bộ | `PUT /api/v1/products/{id}` |
| `PATCH` | Cập nhật một phần | `PATCH /api/v1/products/{id}/status` |
| `DELETE` | Xoá (soft delete mặc định) | `DELETE /api/v1/products/{id}` |

### 1.4. JSON

- Property trong JSON dùng **camelCase** (mặc định của `System.Text.Json` trong ASP.NET Core).
- Enum serialize dạng **string** (`"Pending"`), không dùng số.
- Ngày giờ: ISO 8601, **UTC** → `"2026-10-06T08:30:00Z"`.
- Tiền tệ: `decimal`, đơn vị VND, không format chuỗi ở backend.

### 1.5. Database

| Thành phần | Quy tắc | Ví dụ |
|---|---|---|
| Table | `PascalCase`, **số nhiều** | `Products`, `RentalOrders` |
| Column | `PascalCase` | `CreatedAt`, `UnitPrice` |
| Primary key | `Id` | `Id` |
| Foreign key | `{Entity}Id` | `UserId`, `ProductId` |
| Index | `IX_{Table}_{Column}` | `IX_Products_Name` |
| Unique | `UQ_{Table}_{Column}` | `UQ_Users_Email` |
| Migration | `PascalCase`, mô tả thay đổi | `AddBookingTable`, `AddPhoneToUser` |

Mọi entity nghiệp vụ có các cột audit: `CreatedAt`, `UpdatedAt`, `IsDeleted` (soft delete).

---

## 2. Flow xử lý Request → Response

```mermaid
sequenceDiagram
    autonumber
    participant C as Client
    participant M as Middlewares
    participant Ctl as Controller (API)
    participant S as Service (BLL)
    participant R as Repository (DAL)
    participant DB as Database

    C->>M: HTTP Request
    M->>M: Exception handler, CORS, Authentication, Authorization, Logging
    M->>Ctl: Request đã xác thực
    Ctl->>Ctl: Model binding + Validate input (DTO)
    alt Input không hợp lệ
        Ctl-->>C: 400 + ApiResponse (errors)
    end
    Ctl->>S: Gọi service với Request DTO
    S->>S: Validate nghiệp vụ, map DTO → Entity
    S->>R: Truy vấn / thao tác dữ liệu
    R->>DB: EF Core query
    DB-->>R: Data
    R-->>S: Entity
    S->>S: Map Entity → Response DTO
    S-->>Ctl: Response DTO (hoặc throw exception)
    Ctl-->>C: ApiResponse<T> + HTTP status code
    Note over M: Nếu có exception ở bất kỳ đâu → ExceptionHandlingMiddleware<br/>bắt và trả ApiResponse lỗi chuẩn
```

### 2.1. Thứ tự middleware trong `Program.cs`

```csharp
app.UseMiddleware<ExceptionHandlingMiddleware>(); // 1. Bắt mọi exception — luôn đặt đầu tiên
app.UseHttpsRedirection();                         // 2.
app.UseCors(CorsPolicyName);                       // 3.
app.UseAuthentication();                           // 4. Xác thực JWT
app.UseAuthorization();                            // 5. Kiểm tra role / policy
app.MapControllers();                              // 6.
```

### 2.2. Trách nhiệm từng bước

| Bước | Được làm | **Không** được làm |
|---|---|---|
| **Middleware** | Bắt exception, auth, logging, CORS | Chứa logic nghiệp vụ |
| **Controller** | Nhận request, validate format input, gọi Service, bọc kết quả vào `ApiResponse`, chọn HTTP status | Gọi Repository / DbContext trực tiếp, chứa business logic, `try/catch` nuốt lỗi |
| **Service** | Business rule, validate nghiệp vụ (tồn tại, trùng, quyền sở hữu...), mapping DTO ↔ Entity, quản lý transaction qua UnitOfWork | Trả `IActionResult`, phụ thuộc `HttpContext`, trả Entity ra ngoài |
| **Repository** | Truy vấn / ghi DB qua EF Core | Chứa business rule, trả DTO |

### 2.3. Xử lý lỗi

- Service **throw** custom exception khi vi phạm nghiệp vụ; **không** trả `null` hay `bool` để báo lỗi.
- `ExceptionHandlingMiddleware` map exception → HTTP status:

| Exception | HTTP Status | Khi nào |
|---|---|---|
| `BadRequestException` | 400 | Dữ liệu sai nghiệp vụ (ngày thuê kết thúc < ngày bắt đầu...) |
| `ValidationException` | 400 | Input không qua validator |
| `UnauthorizedException` | 401 | Chưa đăng nhập / token không hợp lệ |
| `ForbiddenException` | 403 | Không đủ quyền |
| `NotFoundException` | 404 | Không tìm thấy resource |
| `ConflictException` | 409 | Trùng dữ liệu (email đã tồn tại...) |
| `Exception` (còn lại) | 500 | Lỗi không mong muốn — **không** trả stack trace ra client ở môi trường Production |

---

## 3. Cấu trúc Request

### 3.1. Header

| Header | Bắt buộc | Giá trị |
|---|---|---|
| `Content-Type` | Có (với body) | `application/json` (hoặc `multipart/form-data` khi upload file) |
| `Authorization` | Với API cần đăng nhập | `Bearer {accessToken}` |
| `Accept-Language` | Không | `vi` / `en` (mặc định `vi`) |

### 3.2. Body (POST / PUT / PATCH)

- JSON, property `camelCase`.
- **Không** gửi các field do server quản lý: `id`, `createdAt`, `updatedAt`, `isDeleted`.
- `id` của resource lấy từ **path**, không lấy từ body khi update.

```json
POST /api/v1/products
{
  "name": "Lều cắm trại 2 người",
  "categoryId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "pricePerDay": 120000,
  "quantity": 10,
  "description": "Lều chống nước, nặng 2kg"
}
```

### 3.3. Query string cho danh sách (paging / filter / sort)

| Param | Kiểu | Mặc định | Ghi chú |
|---|---|---|---|
| `pageNumber` | int | `1` | ≥ 1 |
| `pageSize` | int | `10` | 1 – 100 |
| `search` | string | `null` | Tìm theo keyword |
| `sortBy` | string | `createdAt` | Tên field (camelCase) |
| `sortOrder` | string | `desc` | `asc` \| `desc` |
| *filter riêng* | tuỳ | — | Ví dụ: `categoryId`, `status`, `fromDate`, `toDate` |

```
GET /api/v1/products?pageNumber=1&pageSize=10&search=lều&sortBy=pricePerDay&sortOrder=asc&categoryId=3fa8...
```

Class base dùng chung:

```csharp
public class PagingRequest
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? Search { get; set; }
    public string SortBy { get; set; } = "createdAt";
    public string SortOrder { get; set; } = "desc";
}

public class ProductQueryRequest : PagingRequest
{
    public Guid? CategoryId { get; set; }
}
```

### 3.4. Validation

- Validate format input bằng **FluentValidation** (hoặc DataAnnotations nếu đơn giản) trên Request DTO.
- Validate nghiệp vụ (tồn tại, trùng lặp, trạng thái hợp lệ...) đặt trong **Service**.

---

## 4. Cấu trúc Response

**Mọi** response (thành công lẫn lỗi) đều bọc trong `ApiResponse<T>`.

### 4.1. Wrapper

```csharp
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public int StatusCode { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
    public IDictionary<string, string[]>? Errors { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public static ApiResponse<T> Ok(T data, string message = "Success", int statusCode = 200)
        => new() { Success = true, StatusCode = statusCode, Message = message, Data = data };

    public static ApiResponse<T> Fail(string message, int statusCode, IDictionary<string, string[]>? errors = null)
        => new() { Success = false, StatusCode = statusCode, Message = message, Errors = errors };
}
```

### 4.2. Thành công — 1 object

```json
HTTP 200 OK
{
  "success": true,
  "statusCode": 200,
  "message": "Lấy sản phẩm thành công",
  "data": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "name": "Lều cắm trại 2 người",
    "pricePerDay": 120000,
    "createdAt": "2026-10-06T08:30:00Z"
  },
  "errors": null,
  "timestamp": "2026-10-06T08:30:01Z"
}
```

### 4.3. Thành công — danh sách có phân trang

```json
HTTP 200 OK
{
  "success": true,
  "statusCode": 200,
  "message": "Lấy danh sách sản phẩm thành công",
  "data": {
    "items": [ { "id": "...", "name": "..." } ],
    "pageNumber": 1,
    "pageSize": 10,
    "totalItems": 42,
    "totalPages": 5,
    "hasPrevious": false,
    "hasNext": true
  },
  "errors": null,
  "timestamp": "2026-10-06T08:30:01Z"
}
```

```csharp
public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = [];
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalItems / (double)PageSize);
    public bool HasPrevious => PageNumber > 1;
    public bool HasNext => PageNumber < TotalPages;
}
```

### 4.4. Lỗi validation

```json
HTTP 400 Bad Request
{
  "success": false,
  "statusCode": 400,
  "message": "Dữ liệu không hợp lệ",
  "data": null,
  "errors": {
    "name": ["Tên sản phẩm không được để trống"],
    "pricePerDay": ["Giá thuê phải lớn hơn 0"]
  },
  "timestamp": "2026-10-06T08:30:01Z"
}
```

### 4.5. Lỗi nghiệp vụ / hệ thống

```json
HTTP 404 Not Found
{
  "success": false,
  "statusCode": 404,
  "message": "Không tìm thấy sản phẩm với id '3fa85f64-...'",
  "data": null,
  "errors": null,
  "timestamp": "2026-10-06T08:30:01Z"
}
```

### 4.6. Quy tắc HTTP status code

| Status | Dùng khi |
|---|---|
| `200 OK` | GET, PUT, PATCH, DELETE thành công |
| `201 Created` | POST tạo mới thành công (trả về resource vừa tạo) |
| `204 No Content` | *Không dùng* — luôn trả body `ApiResponse` để FE xử lý thống nhất |
| `400 Bad Request` | Input sai / vi phạm nghiệp vụ |
| `401 Unauthorized` | Chưa đăng nhập, token hết hạn / sai |
| `403 Forbidden` | Đã đăng nhập nhưng không đủ quyền |
| `404 Not Found` | Không tìm thấy resource |
| `409 Conflict` | Trùng dữ liệu |
| `500 Internal Server Error` | Lỗi server không mong muốn |

### 4.7. Quy tắc khác

- **Không bao giờ** trả Entity ra ngoài — luôn map sang Response DTO.
- Không trả các field nhạy cảm: `passwordHash`, `refreshToken`, ...
- `message` viết tiếng Việt, ngắn gọn, FE có thể hiển thị trực tiếp.

---

## 5. Git: Branch, Commit, Push

### 5.1. Các nhánh chính

| Nhánh | Mục đích | Quy tắc |
|---|---|---|
| `main` | Code ổn định, dùng để nộp / demo / deploy | **Cấm push trực tiếp**. Chỉ merge từ `develop` (hoặc `hotfix/*`) qua PR |
| `develop` | Nhánh tích hợp, code mới nhất đã review | **Cấm push trực tiếp**. Chỉ merge qua PR |

### 5.2. Nhánh làm việc

Tạo từ `develop`, đặt tên theo format:

```
<type>/<task-id>-<mo-ta-ngan-kebab-case>
```

| Type | Dùng cho | Ví dụ |
|---|---|---|
| `feature/` | Chức năng mới | `feature/RT-12-product-crud` |
| `fix/` | Sửa bug trên `develop` | `fix/RT-25-booking-date-validation` |
| `hotfix/` | Sửa bug gấp trên `main` (tạo từ `main`) | `hotfix/RT-30-login-500-error` |
| `refactor/` | Tái cấu trúc, không đổi hành vi | `refactor/RT-18-generic-repository` |
| `chore/` | Config, package, CI, build | `chore/update-efcore-package` |
| `docs/` | Tài liệu | `docs/api-contract-booking` |

- Chữ thường, không dấu, nối bằng `-`. Mô tả ≤ 5 từ.
- Không có task-id thì bỏ phần `RT-xx-`.
- Mỗi nhánh chỉ làm **1 task**. Xoá nhánh sau khi merge.

### 5.3. Commit message — Conventional Commits

```
<type>(<scope>): <subject>

[body — tuỳ chọn: giải thích "tại sao", không phải "làm gì"]

[footer — tuỳ chọn: Refs: RT-12 / BREAKING CHANGE: ...]
```

| Type | Ý nghĩa |
|---|---|
| `feat` | Thêm chức năng mới |
| `fix` | Sửa bug |
| `refactor` | Thay đổi code không đổi hành vi |
| `perf` | Cải thiện hiệu năng |
| `test` | Thêm / sửa test |
| `docs` | Tài liệu |
| `style` | Format code, không đổi logic |
| `chore` | Config, package, build, migration không kèm logic |
| `revert` | Revert commit trước |

`scope` = module bị ảnh hưởng: `auth`, `product`, `booking`, `user`, `db`, `config`...

**Quy tắc:**

- `subject` viết **tiếng Anh**, thể mệnh lệnh, chữ thường đầu câu, **không** dấu chấm cuối, ≤ 72 ký tự.
- Mỗi commit là **1 thay đổi logic** hoàn chỉnh và build được.
- Không commit: `bin/`, `obj/`, `.vs/`, `*.user`, `appsettings.Development.json` chứa secret, connection string thật.

**Ví dụ:**

```
feat(product): add pagination and search to product list
fix(booking): prevent end date earlier than start date
refactor(dal): extract generic repository
chore(db): add migration AddBookingTable
docs(api): update booking endpoints in api contract
```

❌ Ví dụ sai: `update`, `fix bug`, `Sửa lỗi`, `feat: Added Product API.`

### 5.4. Quy trình làm việc & push

```bash
# 1. Cập nhật develop mới nhất
git checkout develop
git pull origin develop

# 2. Tạo nhánh làm việc
git checkout -b feature/RT-12-product-crud

# 3. Code + commit nhỏ, thường xuyên
git add <files>
git commit -m "feat(product): add create product endpoint"

# 4. Trước khi push: cập nhật develop vào nhánh của mình
git fetch origin
git rebase origin/develop          # hoặc: git merge origin/develop
# → giải quyết conflict (nếu có), build & chạy lại

# 5. Build phải pass
dotnet build
dotnet test                        # nếu có test

# 6. Push
git push -u origin feature/RT-12-product-crud
```

7. Tạo **Pull Request** vào `develop`:
   - Title theo format commit: `feat(product): product CRUD`.
   - Mô tả: làm gì, cách test, ảnh chụp Swagger (nếu có), link task.
   - Cần **≥ 1 approve** từ thành viên khác.
8. Merge bằng **Squash and merge**, sau đó xoá nhánh.

**Cấm:**

- `git push --force` lên `main` / `develop`. Trên nhánh cá nhân chỉ dùng `--force-with-lease` khi đã rebase.
- Tự merge PR của mình khi chưa có review.
- Push code không build được.

---

## 6. Quy trình bắt đầu code một tính năng

> Phần này hướng dẫn từng bước: từ lúc nhận task → tạo file / folder → code → test → tạo PR.
> Ví dụ xuyên suốt: module **Category** (CRUD danh mục).
>
> ⚠️ Ví dụ dùng khoá `long Id` và tên cột PascalCase. Khi team chốt câu hỏi **A1** (`bigint` / `Guid`) và **A2** (snake_case) trong [`database/ERD_REVIEW.md`](database/ERD_REVIEW.md), làm theo quyết định đó.

### 6.1. Chuẩn bị môi trường (chỉ làm lần đầu)

| Công cụ | Phiên bản |
|---|---|
| .NET SDK | 8.0 trở lên (project target `net8.0`) |
| SQL Server | 2019+ / LocalDB / Docker |
| IDE | Visual Studio 2022 / Rider / VS Code + C# Dev Kit |
| EF Core CLI | `dotnet tool install --global dotnet-ef` |

```bash
# 1. Clone và chuyển sang develop
git clone <repo-url>
cd BE
git checkout develop

# 2. Restore package
dotnet restore

# 3. (Tuỳ chọn) Connection string riêng của máy mình — lưu bằng user-secrets, KHÔNG sửa appsettings.json
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=.;Database=RenTrekDb;Trusted_Connection=True;TrustServerCertificate=True" --project src/RenTrek.API

# 4. Tạo / cập nhật database theo migration hiện có
dotnet ef database update -p src/RenTrek.DAL -s src/RenTrek.API

# 5. Chạy API → mở https://localhost:7000/swagger
dotnet run --project src/RenTrek.API --launch-profile https
```

> `-p` (project) là nơi chứa `AppDbContext` và migration (**DAL**), `-s` (startup) là project chạy được và có `appsettings` (**API**).

### 6.2. Tổng quan các bước

Code theo thứ tự **từ dưới lên: DAL → BLL → API**. Layer trên phụ thuộc layer dưới, làm ngược sẽ không compile được.

```mermaid
flowchart TD
    A["1. Nhận task, đọc API contract + ERD"] --> B["2. Tạo nhánh từ develop"]
    B --> C["3. DAL: Entity, Configuration, DbSet"]
    C --> D["4. DAL: Tạo migration, update database"]
    D --> E["5. BLL: DTOs, Validator, Mapping"]
    E --> F["6. BLL: Service interface + implementation, đăng ký DI"]
    F --> G["7. API: Controller"]
    G --> H["8. Build, test qua Swagger"]
    H --> I["9. Cập nhật 03-API-CONTRACT.md"]
    I --> J["10. Commit, push, tạo PR"]
```

### 6.3. Danh sách file cần tạo / sửa

Ví dụ cho module **Category** (🆕 tạo mới · ✏️ sửa file có sẵn):

| # | Layer | File | |
|---|---|---|---|
| 1 | DAL | `src/RenTrek.DAL/Entities/Category.cs` | 🆕 |
| 2 | DAL | `src/RenTrek.DAL/Entities/Enums/CategoryStatus.cs` | 🆕 |
| 3 | DAL | `src/RenTrek.DAL/Data/Configurations/CategoryConfiguration.cs` | 🆕 |
| 4 | DAL | `src/RenTrek.DAL/Data/AppDbContext.cs` — thêm `DbSet` | ✏️ |
| 5 | DAL | `src/RenTrek.DAL/Data/Migrations/*_AddCategoryTable.cs` — do EF sinh | 🆕 |
| 6 | BLL | `src/RenTrek.BLL/DTOs/Category/CreateCategoryRequest.cs` | 🆕 |
| 7 | BLL | `src/RenTrek.BLL/DTOs/Category/UpdateCategoryRequest.cs` | 🆕 |
| 8 | BLL | `src/RenTrek.BLL/DTOs/Category/CategoryQueryRequest.cs` | 🆕 |
| 9 | BLL | `src/RenTrek.BLL/DTOs/Category/CategoryResponse.cs` | 🆕 |
| 10 | BLL | `src/RenTrek.BLL/Validators/Category/CreateCategoryRequestValidator.cs` | 🆕 |
| 11 | BLL | `src/RenTrek.BLL/Validators/Category/UpdateCategoryRequestValidator.cs` | 🆕 |
| 12 | BLL | `src/RenTrek.BLL/Mappings/CategoryProfile.cs` | 🆕 |
| 13 | BLL | `src/RenTrek.BLL/Services/Interfaces/ICategoryService.cs` | 🆕 |
| 14 | BLL | `src/RenTrek.BLL/Services/Implementations/CategoryService.cs` | 🆕 |
| 15 | BLL | `src/RenTrek.BLL/DependencyInjection.cs` — đăng ký service | ✏️ |
| 16 | API | `src/RenTrek.API/Controllers/CategoriesController.cs` | 🆕 |
| 17 | Docs | `docs/03-API-CONTRACT.md` | ✏️ |

**Quy tắc tạo file / folder:**

- DTO và Validator gom theo **thư mục module**: `DTOs/Category/`, `Validators/Category/`.
- Service, Repository chia theo `Interfaces/` và `Implementations/`.
- Namespace **khớp đường dẫn thư mục**: file `src/RenTrek.BLL/DTOs/Category/CategoryResponse.cs` → `namespace RenTrek.BLL.DTOs.Category;`.
- Dùng **file-scoped namespace** (`namespace X;`), mỗi file 1 class.
- Thư mục chỉ có `.gitkeep`: thêm file thật vào rồi thì xoá `.gitkeep`.

### 6.4. Bước 1–2: Nhận task và tạo nhánh

1. Đọc mô tả task, endpoint tương ứng trong [`03-API-CONTRACT.md`](03-API-CONTRACT.md) và các bảng liên quan trong [`database/Logical_ERD_v2.md`](database/Logical_ERD_v2.md).
2. Chưa có contract thì **viết contract trước** (URL, request, response, lỗi) và báo FE review, rồi mới code.
3. Tạo nhánh theo [§5.2](#52-nhánh-làm-việc):

```bash
git checkout develop
git pull origin develop
git checkout -b feature/RT-15-category-crud
```

### 6.5. Bước 3: DAL — Entity và Configuration

**Entity** — chỉ chứa property và navigation, **không** chứa logic, **không** dùng DataAnnotations (cấu hình bằng Fluent API).

```csharp
// src/RenTrek.DAL/Entities/Enums/CategoryStatus.cs
namespace RenTrek.DAL.Entities.Enums;

public enum CategoryStatus
{
    Active,
    Inactive
}
```

```csharp
// src/RenTrek.DAL/Entities/Category.cs
using RenTrek.DAL.Entities.Enums;

namespace RenTrek.DAL.Entities;

public class Category
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public CategoryStatus Status { get; set; } = CategoryStatus.Active;
}
```

**Configuration** — mỗi entity 1 file trong `Data/Configurations/`. `AppDbContext` đã gọi `ApplyConfigurationsFromAssembly` nên **tự nhận**, không cần đăng ký.

```csharp
// src/RenTrek.DAL/Data/Configurations/CategoryConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name).HasMaxLength(100).IsRequired();
        builder.Property(c => c.Description).HasMaxLength(500);
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(c => c.Name).IsUnique().HasDatabaseName("UQ_Categories_Name");
    }
}
```

**DbSet** — thêm vào `AppDbContext`:

```csharp
// src/RenTrek.DAL/Data/AppDbContext.cs
public DbSet<Category> Categories => Set<Category>();
```

> **Khi nào cần Repository riêng?** Chỉ khi có query đặc thù được dùng lại nhiều nơi hoặc phức tạp (join nhiều bảng, raw SQL...).
> CRUD thông thường dùng `_unitOfWork.Repository<Category>()` là đủ. Nếu tạo repository riêng: thêm `ICategoryRepository : IGenericRepository<Category>` + `CategoryRepository`, khai báo property trong `IUnitOfWork` / `UnitOfWork`, đăng ký trong `RenTrek.DAL/DependencyInjection.cs`.

### 6.6. Bước 4: DAL — Migration

```bash
# Tạo migration (tên PascalCase, mô tả thay đổi)
dotnet ef migrations add AddCategoryTable -p src/RenTrek.DAL -s src/RenTrek.API -o Data/Migrations

# Áp dụng vào database local
dotnet ef database update -p src/RenTrek.DAL -s src/RenTrek.API
```

- **Mở file migration vừa sinh và đọc lại** — chắc chắn chỉ có thay đổi của mình.
- Sai thì **chưa push** → xoá bằng `dotnet ef migrations remove -p src/RenTrek.DAL -s src/RenTrek.API`, sửa entity rồi tạo lại.
- Migration **đã merge vào `develop`** thì **không sửa / xoá** — tạo migration mới để sửa.
- Luôn `git pull` / rebase `develop` **trước khi** tạo migration để tránh conflict `AppDbContextModelSnapshot.cs`.

Commit gợi ý: `feat(category): add category entity and migration`

### 6.7. Bước 5: BLL — DTOs, Validator, Mapping

**DTO** — class đơn giản, chỉ chứa field FE cần gửi / nhận.

```csharp
// src/RenTrek.BLL/DTOs/Category/CreateCategoryRequest.cs
namespace RenTrek.BLL.DTOs.Category;

public class CreateCategoryRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}
```

```csharp
// src/RenTrek.BLL/DTOs/Category/UpdateCategoryRequest.cs
using RenTrek.DAL.Entities.Enums;

namespace RenTrek.BLL.DTOs.Category;

public class UpdateCategoryRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public CategoryStatus Status { get; set; }
}
```

```csharp
// src/RenTrek.BLL/DTOs/Category/CategoryQueryRequest.cs
using RenTrek.BLL.DTOs.Common;
using RenTrek.DAL.Entities.Enums;

namespace RenTrek.BLL.DTOs.Category;

public class CategoryQueryRequest : PagingRequest
{
    public CategoryStatus? Status { get; set; }
}
```

```csharp
// src/RenTrek.BLL/DTOs/Category/CategoryResponse.cs
using RenTrek.DAL.Entities.Enums;

namespace RenTrek.BLL.DTOs.Category;

public class CategoryResponse
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public CategoryStatus Status { get; set; }
}
```

**Validator** — kiểm tra **format** input. `ValidationFilter` ở API **tự động chạy** validator cho mọi request DTO và trả `400` chuẩn, controller **không cần** gọi validate. Validator đặt trong `RenTrek.BLL` sẽ được `AddValidatorsFromAssembly` tự đăng ký.

```csharp
// src/RenTrek.BLL/Validators/Category/CreateCategoryRequestValidator.cs
using FluentValidation;
using RenTrek.BLL.DTOs.Category;

namespace RenTrek.BLL.Validators.Category;

public class CreateCategoryRequestValidator : AbstractValidator<CreateCategoryRequest>
{
    public CreateCategoryRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên danh mục không được để trống")
            .MaximumLength(100).WithMessage("Tên danh mục tối đa 100 ký tự");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Mô tả tối đa 500 ký tự");
    }
}
```

> `UpdateCategoryRequestValidator` viết tương tự, thêm `RuleFor(x => x.Status).IsInEnum()`.

**Mapping** — mỗi module 1 `Profile`, AutoMapper tự quét trong `RenTrek.BLL`.

```csharp
// src/RenTrek.BLL/Mappings/CategoryProfile.cs
using AutoMapper;
using RenTrek.BLL.DTOs.Category;
using RenTrek.DAL.Entities;

namespace RenTrek.BLL.Mappings;

public class CategoryProfile : Profile
{
    public CategoryProfile()
    {
        CreateMap<Category, CategoryResponse>();
        CreateMap<CreateCategoryRequest, Category>();
        CreateMap<UpdateCategoryRequest, Category>();
    }
}
```

### 6.8. Bước 6: BLL — Service

**Interface** — định nghĩa trước, controller chỉ biết interface này.

```csharp
// src/RenTrek.BLL/Services/Interfaces/ICategoryService.cs
using RenTrek.BLL.DTOs.Category;
using RenTrek.BLL.DTOs.Common;

namespace RenTrek.BLL.Services.Interfaces;

public interface ICategoryService
{
    Task<PagedResult<CategoryResponse>> GetAllAsync(CategoryQueryRequest request, CancellationToken cancellationToken = default);
    Task<CategoryResponse> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<CategoryResponse> CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default);
    Task<CategoryResponse> UpdateAsync(long id, UpdateCategoryRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(long id, CancellationToken cancellationToken = default);
}
```

**Implementation** — nơi duy nhất chứa business logic. Vi phạm nghiệp vụ thì **throw** exception trong `RenTrek.BLL.Exceptions`.

```csharp
// src/RenTrek.BLL/Services/Implementations/CategoryService.cs
using AutoMapper;
using AutoMapper.QueryableExtensions;
using RenTrek.BLL.DTOs.Category;
using RenTrek.BLL.DTOs.Common;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Helpers;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Services.Implementations;

public class CategoryService : ICategoryService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public CategoryService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    private IGenericRepository<Category> Categories => _unitOfWork.Repository<Category>();

    public async Task<PagedResult<CategoryResponse>> GetAllAsync(CategoryQueryRequest request, CancellationToken cancellationToken = default)
    {
        var query = Categories.Query(); // AsNoTracking — chỉ đọc

        if (!string.IsNullOrWhiteSpace(request.Search))
            query = query.Where(c => c.Name.Contains(request.Search));

        if (request.Status.HasValue)
            query = query.Where(c => c.Status == request.Status.Value);

        query = request.SortBy.ToLowerInvariant() switch
        {
            "name" => request.IsDescending ? query.OrderByDescending(c => c.Name) : query.OrderBy(c => c.Name),
            _ => request.IsDescending ? query.OrderByDescending(c => c.Id) : query.OrderBy(c => c.Id)
        };

        return await query
            .ProjectTo<CategoryResponse>(_mapper.ConfigurationProvider) // chỉ SELECT các cột cần
            .ToPagedResultAsync(request, cancellationToken);
    }

    public async Task<CategoryResponse> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var category = await FindOrThrowAsync(id, cancellationToken);
        return _mapper.Map<CategoryResponse>(category);
    }

    public async Task<CategoryResponse> CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        if (await Categories.AnyAsync(c => c.Name == request.Name, cancellationToken))
            throw new ConflictException("Tên danh mục đã tồn tại");

        var category = _mapper.Map<Category>(request);
        await Categories.AddAsync(category, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<CategoryResponse>(category);
    }

    public async Task<CategoryResponse> UpdateAsync(long id, UpdateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var category = await FindOrThrowAsync(id, cancellationToken);

        if (await Categories.AnyAsync(c => c.Id != id && c.Name == request.Name, cancellationToken))
            throw new ConflictException("Tên danh mục đã tồn tại");

        _mapper.Map(request, category); // entity đang được track → chỉ cần SaveChanges
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<CategoryResponse>(category);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var category = await FindOrThrowAsync(id, cancellationToken);

        Categories.Remove(category);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<Category> FindOrThrowAsync(long id, CancellationToken cancellationToken)
        => await Categories.GetByIdAsync(id, cancellationToken)
           ?? throw new NotFoundException($"Không tìm thấy danh mục với id '{id}'");
}
```

**Đăng ký DI** — thêm vào `RenTrek.BLL/DependencyInjection.cs`:

```csharp
services.AddScoped<ICategoryService, CategoryService>();
```

> Thao tác ghi **nhiều bảng** cần all-or-nothing → bọc trong transaction:
>
> ```csharp
> await _unitOfWork.BeginTransactionAsync(cancellationToken);
> // ... AddAsync / Update nhiều repository ...
> await _unitOfWork.CommitAsync(cancellationToken); // tự SaveChanges + Commit, lỗi thì tự Rollback
> ```

Commit gợi ý: `feat(category): add category service`

### 6.9. Bước 7: API — Controller

Controller **mỏng**: nhận request → gọi service → bọc `ApiResponse` → chọn status code. Không `try/catch`, không validate thủ công.

```csharp
// src/RenTrek.API/Controllers/CategoriesController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenTrek.API.Common;
using RenTrek.BLL.DTOs.Category;
using RenTrek.BLL.DTOs.Common;
using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.API.Controllers;

[ApiController]
[Route("api/v1/categories")]
[Authorize(Roles = "Admin")]
public class CategoriesController : ControllerBase
{
    private readonly ICategoryService _categoryService;

    public CategoriesController(ICategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAll([FromQuery] CategoryQueryRequest request, CancellationToken cancellationToken)
    {
        var result = await _categoryService.GetAllAsync(request, cancellationToken);
        return Ok(ApiResponse<PagedResult<CategoryResponse>>.Ok(result, "Lấy danh sách danh mục thành công"));
    }

    [HttpGet("{id:long}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken)
    {
        var result = await _categoryService.GetByIdAsync(id, cancellationToken);
        return Ok(ApiResponse<CategoryResponse>.Ok(result, "Lấy danh mục thành công"));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCategoryRequest request, CancellationToken cancellationToken)
    {
        var result = await _categoryService.CreateAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<CategoryResponse>.Ok(result, "Tạo danh mục thành công", StatusCodes.Status201Created));
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateCategoryRequest request, CancellationToken cancellationToken)
    {
        var result = await _categoryService.UpdateAsync(id, request, cancellationToken);
        return Ok(ApiResponse<CategoryResponse>.Ok(result, "Cập nhật danh mục thành công"));
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await _categoryService.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Xoá danh mục thành công"));
    }
}
```

| Cần | Dùng |
|---|---|
| API public | `[AllowAnonymous]` |
| Chỉ cần đăng nhập | `[Authorize]` |
| Theo role | `[Authorize(Roles = "Admin")]`, nhiều role: `"Admin,Staff"` |
| Lấy id user hiện tại | `User.FindFirstValue(ClaimTypes.NameIdentifier)` ở controller rồi truyền xuống service — service **không** đọc `HttpContext` |

Commit gợi ý: `feat(category): add category endpoints`

### 6.10. Bước 8: Build và test

```bash
dotnet build
dotnet run --project src/RenTrek.API --launch-profile https
```

Mở `https://localhost:7000/swagger` và test **đủ các case**:

- [ ] Happy case của từng endpoint (200 / 201)
- [ ] Input sai → `400` với `errors` theo từng field
- [ ] Id không tồn tại → `404`
- [ ] Trùng dữ liệu → `409`
- [ ] Không có token → `401`; token sai role → `403` (bấm **Authorize** trên Swagger để nhập token)
- [ ] Danh sách: thử `pageNumber`, `pageSize`, `search`, `sortBy`, `sortOrder`
- [ ] Response đúng format `ApiResponse` và đúng contract

### 6.11. Bước 9–10: Cập nhật contract và tạo PR

1. Cập nhật [`03-API-CONTRACT.md`](03-API-CONTRACT.md): đổi trạng thái endpoint, điền request / response thực tế, ghi changelog.
2. Đi qua [Checklist trước khi tạo PR](#7-checklist-trước-khi-tạo-pr).
3. Rebase `develop`, push, tạo PR theo [§5.4](#54-quy-trình-làm-việc--push).

### 6.12. Lỗi thường gặp

| Triệu chứng | Nguyên nhân | Cách sửa |
|---|---|---|
| `Unable to resolve service for type 'ICategoryService'` | Quên đăng ký DI | Thêm `AddScoped` trong `RenTrek.BLL/DependencyInjection.cs` |
| Validator không chạy | Validator đặt ngoài `RenTrek.BLL` hoặc sai generic type | Đặt trong `RenTrek.BLL/Validators/...`, kế thừa `AbstractValidator<TênRequest>` |
| `AutoMapperMappingException: Missing type map` | Chưa `CreateMap` cho cặp type đó | Thêm vào `{Module}Profile` |
| Sửa entity rồi `SaveChanges` nhưng DB không đổi | Entity lấy từ `Query()` (AsNoTracking) | Lấy bằng `GetByIdAsync` hoặc `QueryTracking()` khi cần sửa |
| Migration rỗng / thiếu bảng | Chưa thêm `DbSet` hoặc Configuration sai namespace / assembly | Kiểm tra `AppDbContext` và file trong `Data/Configurations/` |
| Conflict ở `AppDbContextModelSnapshot.cs` | 2 người cùng tạo migration | `migrations remove` migration của mình → rebase `develop` → `migrations add` lại |
| `401` dù đã nhập token trên Swagger | Token hết hạn hoặc sai Issuer / Audience / SecretKey | Đăng nhập lại; kiểm tra cấu hình `Jwt` |
| Trả về `500` thay vì lỗi nghiệp vụ | Throw `Exception` thường | Throw exception trong `RenTrek.BLL.Exceptions` (`NotFoundException`, `ConflictException`...) |

---

## 7. Checklist trước khi tạo PR

- [ ] Tên nhánh, commit message đúng convention
- [ ] `dotnet build` không lỗi, không thêm warning mới
- [ ] Đã rebase/merge `develop` mới nhất, không còn conflict
- [ ] Controller không gọi Repository / DbContext trực tiếp
- [ ] Không trả Entity ra ngoài, response bọc trong `ApiResponse<T>`
- [ ] Không có secret / connection string thật trong code
- [ ] Đã test endpoint qua Swagger / Postman
- [ ] Đã cập nhật [`03-API-CONTRACT.md`](03-API-CONTRACT.md) nếu thêm / đổi API
- [ ] Có migration (nếu đổi DB) và migration chạy được
