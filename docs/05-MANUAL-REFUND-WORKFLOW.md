# Manual Refund Workflow

## 1. Phạm vi

Luồng này áp dụng cho booking chuyến đi đã thanh toán nhưng chưa khởi hành, và (từ 2026-11-14) cho đơn thuê: huỷ đơn đã thanh toán, khách không đến nhận, hoàn cọc sau khi trả đồ — xem [`06-RENTAL-DEPOSIT-FLOW.md`](06-RENTAL-DEPOSIT-FLOW.md). Hồ sơ đơn thuê có thể bắt đầu ở `AwaitingBankInfo` (chưa có STK, khách nhập sau qua `POST /refund-cases/{id}/bank-info` rồi mới sang `Requested`); file Excel có thêm cột "Loại đơn" và "Nguồn hoàn". RenTrekk chưa tự động chuyển tiền hoàn qua SePay. Admin xuất hồ sơ cho kế toán chuyển khoản thủ công và ghi nhận kết quả.

## 2. Luồng khách hàng

1. Gọi `GET /api/v1/trip-bookings/{orderId}/cancellation-preview`.
2. Hệ thống tính số tiền hoàn theo chính sách gắn với đơn và thời điểm gọi.
3. Khách xem trường hợp áp dụng, số tiền đã trả và số tiền dự kiến hoàn.
4. Nếu số tiền hoàn lớn hơn `0`, khách nhập ngân hàng, số tài khoản, chủ tài khoản và chấp thuận xử lý dữ liệu.
5. Gọi `POST /api/v1/trip-bookings/{orderId}/cancel` với `expectedRefundAmount` từ preview.
6. Hệ thống hủy booking, tạo Payment loại `Refund` và tạo `RefundCase` trạng thái `Requested` trong cùng transaction.
7. Khách theo dõi bằng `GET /api/v1/refund-cases/{refundCaseId}`.

Nếu số tiền đã đổi giữa preview và lúc hủy, API trả `409 REFUND_AMOUNT_CHANGED`. FE phải tải preview mới và yêu cầu khách xác nhận lại.

## 3. Luồng Admin và kế toán

1. Admin xem hàng đợi bằng `GET /api/v1/admin/refund-cases`.
2. Admin yêu cầu OTP bằng `POST /api/v1/admin/refund-cases/export/verification-codes`.
3. Admin gửi OTP vào `POST /api/v1/admin/refund-cases/export` để tải Excel.
4. Hồ sơ được xuất chuyển từ `Requested` sang `Approved`.
5. Kế toán chuyển khoản theo Excel.
6. Admin gọi `complete` với mã giao dịch, hoặc `mark-failed` kèm lý do.
7. Hồ sơ sai điều kiện có thể bị `reject` khi chưa xuất.

## 4. Máy trạng thái

- `Requested`: chờ Admin xuất cho kế toán.
- `Approved`: đã xuất; kế toán đang xử lý.
- `Completed`: chuyển tiền thành công.
- `Rejected`: Admin từ chối trước khi xuất.
- `Failed`: chuyển khoản thất bại.
- `Cancelled`: hồ sơ bị hủy bởi quy trình hệ thống.

Khi `Completed`, Payment Refund thành `Succeeded`. `orders.payment_status` chuyển thành `Refunded` hoặc `PartiallyRefunded` theo tổng tiền đã hoàn.

## 5. Bảo mật dữ liệu

- Số tài khoản và tên chủ tài khoản được mã hóa AES-256-GCM trước khi lưu.
- Khóa lấy từ `RefundDataEncryptionKey`; không commit giá trị thật.
- API khách chỉ trả số tài khoản đã che.
- Chỉ endpoint export của Admin giải mã dữ liệu.
- Export cần OTP dùng một lần, có hết hạn và giới hạn số lần thử.
- Endpoint nhạy cảm bị rate limit theo tài khoản.
- File Excel trả `Cache-Control: no-store`.
- Background service xóa payload nhạy cảm sau thời hạn cấu hình.
- Bảng Supabase bật RLS và thu hồi quyền trực tiếp của role `anon` và `authenticated`.

## 6. Triển khai

Chạy migration `database/migrations/20261102_refund_cases.sql`.

Tạo khóa mã hóa:

```bash
openssl rand -base64 32
```

Đặt kết quả vào secret `RefundDataEncryptionKey` của môi trường chạy. Không đổi khóa khi còn hồ sơ chưa purge; dữ liệu cũ sẽ không giải mã được.

## 7. Giới hạn hiện tại

- Hệ thống không tự động chuyển tiền qua SePay.
- Kế toán vẫn chuyển khoản ngoài hệ thống.
- Trạng thái `Completed` dựa trên xác nhận của Admin và mã giao dịch đã nhập.
