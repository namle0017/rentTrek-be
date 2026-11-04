# Kết quả triển khai CI — 02/11/2026

Đã tạo bộ kiểm tra thực thi trong working tree tại branch `chore/setup-ci-cd`, dựa trên
HEAD `9f955b6`. **Toàn bộ CI local trả exit 1**, đúng với các lỗi hiện tại được phát hiện.
Chưa commit, push, merge, deploy hoặc thay đổi cấu hình GitHub từ xa.
Hướng dẫn vận hành và branch protection: [README.md](README.md).

## Phạm vi phân tích và thiết kế

- Solution ban đầu: API, BLL, DAL và hai project xUnit, tất cả target `net8.0`.
  Baseline ban đầu chạy **83 BLL + 5 API = 88 tests PASS**.
- Đã đối chiếu `01-CONVENTIONS`, `02-ARCHITECTURE`, hai API contracts, các SQL snapshots,
  ERD draw.io (Logical ERD/Conceptual), project/package/test/configuration/container files.
  Không có README/AGENTS hay CI workflow ở code checkout ban đầu; không có
  `Directory.Build.props/targets`, `.editorconfig` hoặc `global.json` ban đầu.
- Runtime thực tế: PostgreSQL/Supabase, Npgsql EF provider 8.0.11, EF Design/JWT bearer
  8.0.13, AutoMapper 16.2.0, FluentValidation 12.1.1. Không thay package nghiệp vụ.
- SDK được chọn là bản .NET 8 đang có trên máy, **8.0.424**, không nâng sang .NET 9/10.
  Máy mặc định chọn SDK 10 và `dotnet --info` gặp lỗi workload installer; pin .NET 8 đã
  cho phép restore/build/test/publish bình thường.
- Giữ mô hình N-Layer. Thêm NetArchTest + Roslyn trong project test riêng; Cecil là
  dependency bắc cầu của NetArchTest, dùng bổ sung kiểm tra metadata async/lambda.
- Workflow có Quality, Architecture, Security, Tests, Build, Docker và aggregate
  `CI Required`; cùng scripts chạy Windows PowerShell 7 và Ubuntu GitHub Actions.

## Bảng xác minh thực tế

| Validation | Tool | Status | Details |
|---|---|---|---|
| Restore | .NET SDK 8.0.424, NuGet lockfiles | PASS | Restore locked mode toàn solution thành công; audit High được gate riêng ở Security |
| Code Formatting / Style | dotnet format | FAIL | Exit 2; report có 99 mục tương ứng 76 file duy nhất cần thay đổi format/style/analyzer; có file xuất hiện nhiều lần |
| Unused usings | Roslyn IDE0005 / dotnet format style | FAIL | Exit 2; có using thừa trong source hiện tại |
| Static Analysis / compile | SDK Roslyn/.NET recommended analyzers | PASS | Release và Debug build thành công; lần full build có 159 warning, 0 error; không tuyên bố source sạch warning |
| No new warnings | SARIF warning ratchet | PASS | 115 signature warning hiện có; warning probe mới bị từ chối, dữ liệu gốc khôi phục sau probe |
| Architecture | NetArchTest/Cecil/reflection/Roslyn | FAIL | Lần cuối: **12 tests, 9 PASS / 3 FAIL**, không skip |
| DTO conventions | Existing DtoNamingConventionTests | PASS | 1 test được chạy riêng, có TRX |
| PostgreSQL model | EF offline relational model | PASS | Xây model, kiểm tra bảng/cột snake_case và primary keys; không mở connection |
| EF package compatibility | Lockfile architecture test | PASS | EF/Npgsql provider và Design cùng major/minor 8.0; không yêu cầu mọi patch phải giống nhau |
| Secret history scan | Gitleaks 8.30.1 | FAIL | **39 candidates / 56 reachable commits** của các refs local; redaction 100% |
| Secret working-directory scan | Gitleaks 8.30.1 | FAIL | 3 findings tại `.env` local; file bị ignore không xuất hiện trong clean GitHub checkout |
| Tracked configuration | Test-Configuration.ps1 | PASS | Không còn JWT default có giá trị; không phát hiện các unsafe switches được cấu hình kiểm tra |
| Secret-rule regressions | Generated synthetic fixtures | PASS | Placeholder được chấp nhận; credential JSON/env bị từ chối; report không chứa giá trị synthetic |
| Validation guard regressions | PowerShell fixture checks | PASS | Zero tests, thiếu TRX và exit code native khác 0 đều bị từ chối |
| NuGet Security | NuGet Audit all dependencies | FAIL | Hai dependency bắc cầu High của BLL test project; NU1903 làm restore Security thất bại |
| Unit Tests | dotnet test / xUnit | PASS | **88/88**, không skip; chạy lại qua shared script, kiểm tra TRX thực sự có tests |
| Coverage | Coverlet collector | PASS | Xuất Cobertura và OpenCover; giữ code async trong coverage; không đặt threshold tuỳ ý |
| Database Integration Tests | — | NOT RUN | Không có project integration với database thật trong checkout hiện tại |
| Build | dotnet build --no-restore | PASS | Release và Debug; compile toàn solution kể cả architecture tests |
| Publish | dotnet publish --no-build --no-restore | PASS | API publish Release và Debug thành công, không cần application credentials |
| Docker Build | Docker CLI | NOT RUN | Đã gọi build, gate trả FAIL vì Docker Desktop Linux daemon không chạy; chưa thực sự build image |
| Workflow syntax/expressions | actionlint 1.7.7 | PASS | Exit 0; bổ sung parse XML/JSON/TOML và git diff --check |
| GitHub hosted run | GitHub Actions | NOT RUN | Files chưa được người dùng push; chưa chứng minh runtime Ubuntu/container trên runner |
| Branch protection | GitHub repository settings | NOT RUN | Có hướng dẫn admin yêu cầu `CI Required`; chưa kích hoạt từ xa |

Artifacts local nằm trong `artifacts/`: JSON kết quả theo phase, format report, redacted
security reports, dependency advisories, TRX, coverage, publish outputs. Đây là generated
outputs bị Git ignore. Lượt chạy đầy đủ nằm trong `validation-all.json`; test tương thích
EF được bổ sung sau lượt đó và chạy lại trong `final/architecture-final.trx` (12 tests).

## Nguyên nhân các checks đang FAIL và hướng xử lý

### Dependency API → DAL ngoài composition root

`src/RenTrek.API/Extensions/AuthenticationExtensions.cs:48` resolve `IUnitOfWork` và truy
cập `AccessTokens` để kiểm tra revocation. Metadata chứa cả `IUnitOfWork` và
`IAccessTokenRepository` ở API, trong khi `02-ARCHITECTURE` §4 chỉ cho API tham chiếu DAL
để đăng ký DI. Hướng xử lý: chuyển thao tác này thành interface/service ở BLL rồi gọi
interface từ API, đồng thời giữ nguyên hành vi kiểm tra token bị thu hồi. **Chưa tự
refactor**, vì sẽ chạm luồng authentication. Không tạo ngoại lệ để check PASS.

### Naming và cấu trúc source

- 17 method controller trả Task thiếu hậu tố Async, thuộc Auth, RentalOrders, Tours,
  TripBookings và Users. Ví dụ `AuthController.cs:26` (`Register`),
  `RentalOrdersController.cs:29` (`Quote`), `UsersController.cs:34` (`GetCurrentUser`).
  Hướng xử lý: rename có kiểm tra các references/tests và action routing metadata;
  giữ nguyên HTTP contracts. Đây là việc sửa source riêng, chưa thực hiện trong task CI.
- `src/RenTrek.DAL/Repositories/Interfaces/ITourRepository.cs:5` khai báo thêm
  `TourFilterParams` cùng file với interface. Cần tách record thành file đúng tên và
  chọn vị trí theo quy chuẩn DAL, không đổi sang BLL DTO làm DAL phụ thuộc ngược.
- Format/style và unused usings: xem `artifacts/format/format-report.json`.
  Các ví dụ using thừa: `Program.cs`, `UsersController.cs`, `PasswordHasher.cs`,
  `MyOrderMapper.cs` và mapping profiles. Chưa chạy formatter lên source nghiệp vụ;
  chỉ format các file architecture test vừa tạo. Với source scaffold, sửa template
  sinh code theo quy trình docs thay vì sửa tay hàng loạt.

### NuGet High advisories

`tests/RenTrek.BLL.Tests/RenTrek.BLL.Tests.csproj` có graph bắc cầu:

| Package | Version | Advisory |
|---|---|---|
| System.Net.Http | 4.3.0 | [GHSA-7jgj-8wvc-jh57](https://github.com/advisories/GHSA-7jgj-8wvc-jh57) |
| System.Text.RegularExpressions | 4.3.0 | [GHSA-cmhx-cq75-c4mj](https://github.com/advisories/GHSA-cmhx-cq75-c4mj) |

Hướng xử lý: xác định chain trong lockfile / `dotnet nuget why`, cập nhật test package
gốc hoặc constraint đã vá tương thích .NET 8, regenerate lockfile và chạy lại tests.
Không dùng `NoWarn`, bỏ transitive audit hay suppress advisory. Package upgrades chưa
được thực hiện tự động trong thay đổi CI này.

### Gitleaks findings cần phân loại

39 historical candidates gồm **27 trong documentation, 7 trong tests lịch sử, 5 trong
configuration**. Không được hiểu là 39 credential production đã được xác nhận: một số
giá trị là ví dụ realistic hoặc key test. Rule loại các placeholder rõ ràng theo giá trị,
không bỏ qua nguyên docs/tests/config. Cần xem metadata đã redacted, đối chiếu mục đích
sử dụng và xử lý từng candidate. Không tạo blanket allowlist hay baseline che findings.

Các configuration candidates liên quan `.env.example`, `appsettings.json`,
`appsettings.Development.example.json` và `appsettings.Development.json` ở các commits
cũ. JWT default hiện tại đã được xoá giá trị, nhưng lịch sử vẫn tồn tại. Với credential
thật từng publish: rotate/revoke trước, rồi phối hợp admin xử lý history nếu cần.

Ba finding ở working tree nằm tại `.env` dòng 1, 2, 5. Đây là cấu hình local bị ignore;
không in giá trị, không sửa/xóa `.env` của người dùng, không tải nó lên artifacts.
Clean GitHub runner không có `.env` này. Scanner local vẫn báo FAIL để developer thấy
rủi ro copy/commit nhầm; không thể tuyên bố scan local PASS khi findings còn đó.

## Khác biệt/mâu thuẫn tài liệu được giữ nguyên

1. `01-CONVENTIONS` §6.6 quy định **DB-first, không dùng migrations**; ví dụ module ở
   `02-ARCHITECTURE` vẫn hướng dẫn EF migrations. CI theo quy trình DB-first cụ thể và
   không áp dụng migration lên database. `Data/Migrations` không có migration C# hiện hữu.
2. `03.1-API-CONTRACT-RENTAL-CUSTOMER` ghi rõ draft/đề xuất và có tiền cọc/payment/refund
   chưa triển khai; `03-API-CONTRACT` §7 quy định tiền cọc bằng 0 trong luồng hiện hành.
   Không dùng CI để tự chọn hoặc thay đổi các business rules mâu thuẫn này.
3. Hai PostgreSQL SQL snapshots đều có 51 bảng nhưng khác `users`/`accounts` và quan hệ;
   `docs/database/seed_product_catalog.sql` lại chứa cú pháp SQL Server (`dbo`,
   `SYSUTCDATETIME`). Không thực thi bất kỳ snapshot/seed nào trong CI.
4. Code thực tế có BaseEntity/global soft-delete, cấu hình tên bảng/cột, seed hosted
   service và hand-written query helpers. Offline model PASS không đồng nghĩa schema
   Supabase đã khớp mọi snapshot hoặc database integration đã PASS.
5. `dotnet-tools.json` khai báo dotnet-ef 8.0.31, khác patch Design 8.0.13. CI không
   cần gọi EF CLI/scaffold; không tự chỉnh phiên bản tool hay connect DB để đồng bộ.

## Review và công việc tiếp theo

Đã có review độc lập. Đã sửa các khoảng trống phát hiện: zero-test PASS, bỏ sót namespace
con ở constructor checks, và coverage loại nhầm async state machines. Script regression
đã xác nhận zero/missing TRX và native failures bị từ chối; bộ tests/coverage được chạy lại.

Việc còn lại trước khi merge: sửa các vi phạm source trong PR có review riêng, phân loại
security findings và xử lý credential thật/advisories, bật Docker để xác minh image, push
thay đổi để chạy GitHub hosted CI, rồi nhờ admin bật branch protection theo README.
Không có production database, business logic hoặc file untracked có sẵn của người dùng
bị thay đổi trong quá trình triển khai.
