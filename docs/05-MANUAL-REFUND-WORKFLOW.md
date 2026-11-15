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

1. Admin hoặc Staff xem hàng đợi bằng `GET /api/v1/admin/refund-cases`, đọc lý do huỷ / khiếu nại rồi `approve` (`Requested` → `Approved`) hoặc `reject` (bắt buộc lý do).
2. Admin yêu cầu OTP bằng `POST /api/v1/admin/refund-cases/export/verification-codes`.
3. Admin gửi OTP vào `POST /api/v1/admin/refund-cases/export` để tải Excel.
4. Mỗi lần xuất chỉ gồm hồ sơ `Approved` chưa có kết quả (cả hồ sơ đã gửi kế toán trước đó). Lần xuất đầu set `exported_at`. Hồ sơ `Requested` (chưa duyệt) và `AwaitingBankInfo` không được xuất. `reExport` bị bỏ qua.
5. Kế toán chuyển khoản theo Excel.
6. Admin ghi nhận kết quả theo một trong hai cách:
   - Từng hồ sơ: `complete` (multipart: mã giao dịch + 1-3 ảnh chứng từ bắt buộc) hoặc `mark-failed` kèm lý do ("Báo thất bại", xem mục 3.2).
   - Hàng loạt từ Excel (mục 3.1).
7. Hồ sơ sai điều kiện có thể bị `reject` khi chưa xuất.

### 3.1. Vòng Excel với kế toán

1. File xuất có thêm 4 cột cho kế toán điền: `Đã chuyển` (Có / Không), `Mã giao dịch`, `Lý do chưa chuyển`, `Hình ảnh minh chứng`, cùng cột ẩn `RefundCaseId` (GUID đầy đủ) và sheet `Hướng dẫn`.
2. Kế toán chuyển khoản, điền 4 cột, chèn 1-3 ảnh biên lai vào ô (hoặc đặt ảnh nổi lên đúng dòng), rồi gửi lại file.
3. Admin tải file lên `POST /api/v1/admin/refund-cases/import-result` với `dryRun=true` để xem trước từng dòng (hành động, lỗi, số ảnh).
4. Admin gửi lại với `dryRun=false` để áp dụng: dòng `Có` → `Completed` kèm ảnh, dòng `Không` → trả về `AwaitingBankInfo` kèm lý do (như "Báo thất bại"). Mỗi dòng độc lập; dòng lỗi được báo lại, dòng khác vẫn chạy.
5. Dòng `Có` bắt buộc có mã giao dịch (không trùng) và 1-3 ảnh JPG/PNG/WebP tối đa 5MB.

### 3.2. Báo thất bại → khách nhập lại STK

- `POST /api/v1/admin/refund-cases/{id}/mark-failed` (body `{ reason, expectedRowVersion }`) nhận hồ sơ `Requested`, `Approved` hoặc `AwaitingBankInfo` (nhắc lại khách).
- Hồ sơ chuyển về `AwaitingBankInfo`: xoá STK đã mã hoá, ngân hàng, 4 số cuối; lưu lý do (`failureReason`); payment Refund về `PendingApproval`; khách nhận thông báo `RefundBankInfoRequired` kèm lý do.
- Khách nhập STK mới qua `POST /api/v1/refund-cases/{id}/bank-info` (form trong thẻ "Tiến độ hoàn tiền" ở trang booking / đơn thuê) → hồ sơ về `Requested`, lý do cũ được xoá, chờ duyệt lại trước khi vào lần xuất Excel tiếp theo.

### 3.3. Từ chối và khiếu nại

- `reject` bắt buộc lý do, set `rejected_at`.
- Khách gửi lại (khiếu nại) qua `POST /api/v1/refund-cases/{id}/resubmit` kèm STK và `reason` (bắt buộc, tối đa 1000 ký tự). Chỉ 1 lần, trong 7 ngày kể từ `rejected_at`; quá hạn hoặc hết lượt ⇒ `409 REFUND_CASE_NOT_RESUBMITTABLE`. Thành công: `resubmit_count + 1`, lưu `appeal_reason`, hồ sơ về `Requested` chờ duyệt.
- Hồ sơ `Failed` cũ (trước khi có "Báo thất bại") vẫn gửi lại được, không tính lượt.

## 4. Máy trạng thái

- `Requested`: chờ Admin/Staff duyệt.
- `Approved`: đã duyệt. `exported_at = null` ⇒ chờ Admin xuất; có giá trị ⇒ đã gửi kế toán, đang xử lý.
- `Completed`: chuyển tiền thành công.
- `Rejected`: Admin từ chối trước khi xuất.
- `AwaitingBankInfo`: chờ khách nhập (lại) STK; cũng là trạng thái sau khi Admin "Báo thất bại".
- `Failed`: trạng thái cũ, không còn được tạo mới (dữ liệu trước đây vẫn hiển thị).
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

## 7. Chứng từ chuyển khoản

- Ảnh lưu ở bảng `refund_proofs` (migration `20261116_refund_proofs.sql`), gắn với hồ sơ (`refund_case_id`) hoặc khoản hoàn cũ (`payment_id`), không có URL public.
- Nơi lưu: bucket Supabase private (`Supabase:PrivateBucket`, mặc định `refund-proofs`, cần tạo thủ công và để Private) hoặc thư mục `App_Data/private-uploads/refund-proofs` khi chưa cấu hình Supabase.
- Chỉ Admin và khách sở hữu hồ sơ `Completed` xem được, qua endpoint stream có kiểm tra quyền.
- Luồng khoản hoàn cũ `POST /admin/refunds/{paymentId}/mark-manual` cũng bắt buộc 1-3 ảnh.

## 8. Giới hạn hiện tại

- Hệ thống không tự động chuyển tiền qua SePay.
- Kế toán vẫn chuyển khoản ngoài hệ thống.
- Trạng thái `Completed` dựa trên xác nhận của Admin và mã giao dịch đã nhập.
