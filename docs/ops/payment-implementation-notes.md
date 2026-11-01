# Bàn giao implementation payment — 01/11/2026

Branch: `codex/payment-unified`. Base: `afed26b` từ `feature/sepay-payment`. Core implementation: `2b68d07`. Không merge/push/deploy hoặc chuyển tiền thật.

## Kết quả

- Thu bằng QR tài khoản cố định, header idempotency có lưu DB, account/bank validation, API key hoặc HMAC, durable inbox và ledger từng giao dịch.
- Thu thiếu cộng dồn trong hold; QR refresh theo remaining amount. Đúng/sau deadline vào đối soát, expiry trả tài nguyên một lần. Tiền dư/tiền đến muộn không bị ACK rồi mất dấu.
- Khách preview/gửi/xem refund request có bank snapshot được bảo vệ và policy snapshot. Staff list/detail/approve/reject/done; chỉ Done ghi tiền hoàn, có assignment/reference/time/audit.
- Hạn mức dựa trên tổng nguồn thu và policy, trừ cả đã hoàn và đang giữ. Thêm tiền thực nhận có thể tăng quyền lợi trong cap gốc; request đã gửi không tự đổi amount. Không áp tỷ lệ lặp trên remaining balance.
- Một request có thể dùng nhiều nguồn thu; reference ngân hàng không được dùng cho hai request Done. Worker không chi tiền, không tự Done hoặc nhả budget Approved.
- Giữ auth, booking/invitation, email và catalog B. Chỉ sửa adapter chi tiết rental cần thiết để hiển thị tiền thực nhận/hold/timeline đúng; bỏ `.ua` artifacts đã được đánh giá không thuộc runtime.

## Verification cuối

| Kiểm tra | Kết quả |
|---|---|
| `dotnet build RenTrek.sln --no-restore --verbosity quiet -warnaserror` | 0 warning, 0 error |
| BLL tests | 87 passed, 0 failed |
| API tests | 11 passed, 0 failed |
| PostgreSQL/HTTP integration tests | 61 passed, 0 failed |
| Tổng | 159 passed |
| `git diff --cached --check` trước commit core | Đạt |

NuGet restore từ cache local. PostgreSQL chạy trong Docker/Testcontainers riêng; migration chạy trên baseline SQL B có dữ liệu và prerequisite hiện có, không trên DB của người dùng. Không gọi SePay/SMTP hoặc thực hiện chuyển tiền. Chín test fake PaymentService cũ được chuyển sang coverage PostgreSQL/HTTP, không giữ các assertion trái luật partial/hold mới.

Các regression đã tái hiện rồi sửa: missing schema/unique constraint; replay sau Paid; sai account; partial/late/duplicate receipts; expiry race và tiền thiếu xuất hiện trong queue; Done concurrent/duplicate transfer rollback; policy cap không lặp; cap tăng đúng khi thêm nguồn tiền; legacy refund chặn accept; remaining QR; rental paidAmount và timeline; HTTP role/state contract. Fixture kiểm tra nhiều nguồn dùng policy 100% để bắt buộc chia trên cả nguồn 100.000 và 300.000, tránh giả định thứ tự UUID khi hai receipt có cùng timestamp.

## Các quyết định trong quá trình thực hiện

1. **QR B + manual refund:** theo plan đã được cho phép; không đem adapter VA/refund provider vào runtime. Chi phí nếu muốn đổi: cần một extension VA riêng.
2. **Partial cộng dồn, deadline nghiêm ngặt:** áp dụng mặc định trong plan; khác D8 của A. Giao dịch gửi trước deadline nhưng xử lý muộn phải staff review, đổi lại không xác nhận sau khi chỗ có thể được cấp lại.
3. **Tự review, không mở agent mới:** theo giới hạn trong kế hoạch. Chi phí: chưa có một reviewer độc lập; template code-review được dùng cho lượt tự rà soát, các lỗi quan trọng được kiểm tra RED→GREEN.
4. **Tái sử dụng row-lock entry point và generic UoW:** `IPaymentRepository.AcquireOrderLockAsync` hiện lấy row lock; services/worker/match dùng cùng invariant. Giữ những service nhỏ trực tiếp thay vì tạo thêm toàn bộ interface/repository dự kiến. Chi phí: nếu module lớn thêm, có thể cần tách interface chuyên biệt.
5. **Pin EF của test theo API 8.0.13:** đồng bộ Core/Relational với dependency API đang có, không nâng package runtime hàng loạt. Chi phí: khi nâng API cần cập nhật test references tương ứng.
6. **Prerequisite schema B:** migration yêu cầu `20261026_add_participant_invitations.sql`; canonical phục hồi `orders.idempotency_key` mà B đã dùng. Chi phí: deployment phải theo lịch sử migration hiện có và dry-run DB donor A riêng.
7. **Gộp integration tasks 3–8 trong một commit core:** các kiểu dữ liệu, DI và controller phụ thuộc nhau, tránh commit trung gian không build được. Chi phí: phạm vi bisect/review lớn hơn chia từng service; schema và startup validation vẫn có commit riêng.

## Giới hạn bàn giao

- Repo này là BE: hai màn khách/staff cần tích hợp theo [manual refund contract](../06-MANUAL-REFUND-WORKFLOW.md).
- Trước production phải chạy migration và cấu hình key ring bền vững/dùng chung; xem [rollout](payment-rollout.md). Key ring không được commit hoặc xóa khi redeploy.
- Done là xác nhận của staff sau chuyển khoản bên ngoài; backend không chứng minh ngân hàng đã chi tiền. Staff kiểm tra bank snapshot/reference/status trước thao tác.
- Undo approval/reassign không được mở trong scope hiện tại. Rule giờ trước dịch vụ và các reason hiện hỗ trợ được giữ theo contract; không bypass policy cho ngoại lệ chưa có rule.
- Legacy aggregate Paid thiếu giao dịch xác minh vẫn cần đối soát; không tự suy ra nguồn tiền. Provider refund cũ Pending/unknown cần giải quyết trước chuyển vận hành manual.

Không còn finding critical/important đã xác định trong lượt tự review mà chưa được xử lý. Đây là đánh giá của tác giả, không thay thế review độc lập trước khi merge.
