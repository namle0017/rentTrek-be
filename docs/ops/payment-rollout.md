# Triển khai collection + manual refund

## Phạm vi nhánh

Base `afed26b` (`feature/sepay-payment`), engine thu tiền được sửa theo invariant của A; refund hoàn toàn do Staff chuyển tay. Không merge/push/deploy tự động. Repo BE chỉ cung cấp API và tài liệu tích hợp FE.

## Trước migration

1. Backup DB và giữ bản sao dữ liệu đã che thông tin để dry-run.
2. Xác nhận schema B và các migration hiện có đã áp dụng, đặc biệt `20261026_add_participant_invitations.sql` cung cấp `orders.idempotency_key`. Giữ migration invitation/version hiện hành của B; không đưa contract token cũ từ A vào.
3. Nếu DB từng chạy nhánh A: kiểm tra riêng `received_amount`, các index và refund/provider đang chờ. New migration sẽ từ chối kiểu/nullability không tương thích. Không mặc định snapshot A/B thay thế được nhau.
4. Kiểm tra intent Pending không có provider ID bị trùng:

```sql
SELECT order_id, count(*)
FROM payments
WHERE payment_type='Collection' AND status='Pending' AND provider_transaction_id IS NULL
GROUP BY order_id HAVING count(*) > 1;
```

Đối soát nếu có kết quả. Các Pending chứa provider transaction ID cũ có thể là tiền đã nhận cần review; không xóa/đổi thành Failed chỉ để qua index.

5. Dừng executor refund provider cũ. Đối soát tất cả lệnh Pending/unknown và chứng từ chi liên quan trước khi cho staff hoàn tay; không chi lại khoản provider có thể đã chuyển. Legacy pending refund vẫn chiếm budget cho đến khi được giải quyết đúng.

## Thực thi schema

- DB đã có schema: chạy một lần `database/migrations/20261101_payment_ledger_manual_refunds.sql` bằng quy trình DB migration của đội. Script có transaction, preflight và unique constraints; lỗi thì rollback, không tự xóa dữ liệu xung đột.
- DB mới: canonical `docs/database/supabase_schema.sql` đã có ledger extension. Không chạy lại migration ledger sau snapshot này.
- Cần chạy lịch sử migration của B theo hướng dẫn dự án cho các chức năng ngoài payment. Snapshot và script ledger không thay thế audit toàn bộ schema auth/invitation/catalog.
- Các cột/key/received cache được bổ sung, giữ Paid/Pending/history. Không tự tạo bank_transactions hoặc Done refunds cho dữ liệu cũ.
- Trước/sau migration, đối chiếu số dòng payment, amount/status, tổng refund succeeded; legacy aggregate Paid thiếu receipt phải có kế hoạch đối soát/replay webhook giao dịch thật trước refund.

## Cấu hình

- SePay BankName/AccountNumber/AccountName chính xác, AuthMode ApiKey hoặc Hmac, secret tương ứng. Đảm bảo BankName khớp gateway nhận từ SePay; nếu dùng SubAccount thì cấu hình rõ.
- `Payments__KeyRingPath`: đường dẫn volume bền vững, có quyền đọc/ghi, dùng chung cho mọi instance. Không dùng filesystem tạm của container; bảo vệ/backup key ring và giữ application name `RenTrek.Payments`. Mất key sẽ không đọc được recipient/payload cũ.
- Production không khởi động nếu thiếu KeyRingPath. Development mặc định `.payment-keys` đã được gitignore. Render blueprint yêu cầu nhập đường dẫn; đội triển khai tự gắn volume phù hợp, không tự cấp tài nguyên trả phí qua nhánh này.
- Không cần API token/capability hoàn tiền SePay. Backend không có action chuyển tiền hoặc worker thực thi refund.
- CORS/header cho phép `Idempotency-Key`; FE create payment/refund/Done cần key ổn định. Các consumer cũ chưa gửi key phải cập nhật cùng đợt.

## Bật ứng dụng và kiểm chứng

1. Schema trước app; thay webhook URL/auth đúng môi trường, không trộn sandbox/live.
2. `/health` là liveness; `/health/ready` kiểm tra schema payment và data protection. Render dùng readiness để không coi thiếu migration là sẵn sàng.
3. Worker mỗi khoảng 5 giây: receipts/reconciliation/expiry, batch 50 và scope riêng. Receipt lỗi có retry/backoff, quá ngưỡng vào review. Approved refunds không tự Done hoặc nhả budget.
4. Trên DB test/sandbox riêng, kiểm tra create→webhook→poll; thiếu/đủ/dư/muộn, customer submit→staff approve/reject/done, replay và role isolation. Dùng chứng từ giả cho test Done; không chuyển tiền thật để kiểm tra API.
5. Kiểm tra màn khách/staff theo hai guide trước mở nghiệp vụ. Approved chưa là đã hoàn; staff kiểm tra bank snapshot và reference trước chuyển.

## Rollback và quan sát

- Giữ ledger/request/history và key ring, không down-migrate xóa dữ liệu tiền.
- App cũ không hiểu ledger/idempotency/manual request: không mặc định rollback binary an toàn. Có thể cần maintenance/forward fix, dừng mutation/worker không tương thích trước đổi app.
- Theo dõi receipt Received/Retry quá lâu, Unmatched/Rejected/conflict, hold chưa được giải phóng, request PendingReview/Approved tồn đọng và lỗi readiness; staff xem queue có filter trạng thái.
- Approved tồn đọng không tự coi là chuyển thất bại vì tiền có thể đã đi ngoài hệ thống. Kiểm tra reference/chứng từ trước mọi thao tác nghiệp vụ tiếp theo.

## Kiểm tra phát triển

Chạy tuần tự trong worktree:

```powershell
dotnet build RenTrek.sln
dotnet test tests/RenTrek.BLL.Tests
dotnet test tests/RenTrek.API.Tests
dotnet test tests/RenTrek.IntegrationTests
```

Integration dùng Docker/PostgreSQL Testcontainers riêng, gồm HTTP contract, transaction/unique constraints, concurrency và migration baseline có dữ liệu. Không kết nối DB thật hoặc chi tiền. Chín test PaymentService dùng fake cũ đã chuyển sang các ca PostgreSQL/HTTP, đồng thời thay assertion “chuyển thiếu thì kẹt NeedsReconciliation” bằng luật cộng dồn trong hold.
