# Yêu cầu hoàn tiền thủ công theo policy

Khách gửi yêu cầu và tài khoản nhận. Staff kiểm tra policy/chứng từ, duyệt hoặc từ chối; chuyển tiền bằng ứng dụng ngân hàng bên ngoài rồi đánh dấu Done. Backend không phát lệnh chuyển tiền hoặc tự xác minh giao dịch chi ngân hàng.

## Màn khách hàng

1. Gọi `GET /api/v1/orders/{orderId}/refund-eligibility?reasonCode=ServiceIssue`.
2. Hiện `eligible`, `reason`, `amount`, `policyVersion`, `refundPercentage`. Nếu không đủ điều kiện, giải thích reason; không cho khách tự nhập phần trăm/số tiền vượt policy.
3. Form: ngân hàng, số tài khoản (chuỗi, giữ số 0 đầu), tên chủ tài khoản, lý do, ghi chú.
4. Submit với Idempotency-Key ổn định, rồi hiện trạng thái/lý do từ chối và lịch sử.

Customer JWT owner only. Các reason code hiện có: `Cancellation`, `ServiceIssue`, `PaymentIssue`; đều kiểm tra policy/rules gắn với order. Cancellation submit yêu cầu order đã Cancelled; API hủy riêng, không tự hủy khi chỉ gửi report. Các rule giờ trước dịch vụ hiện có không cho hoàn sau lúc bắt đầu; muốn hỗ trợ tình huống khác phải có policy phù hợp, không để staff bypass bằng amount nhập tay.

`POST /api/v1/orders/{orderId}/refund-requests`

Headers: JWT và `Idempotency-Key`. 201 mới, 200 replay cùng request; key dùng cho body khác trả 409.

```json
{
  "bankCode":"MB",
  "accountNumber":"0012345678",
  "accountHolderName":"NGUYEN VAN A",
  "reasonCode":"ServiceIssue",
  "note":"Mô tả vấn đề cần staff xem xét"
}
```

`amount` do server quyết định. BankCode 2–30 ký tự chữ/số/_/-, account 4–40 ký tự chữ/số, holder 2–100 ký tự, note tối đa 2000. Form chỉ xác minh hình dạng dữ liệu, không chứng minh tài khoản tồn tại hay đúng chủ.

- `GET /api/v1/orders/{orderId}/refund-requests?page=1&pageSize=20`: lịch sử order, số tài khoản che bớt.
- `GET /api/v1/refund-requests/{id}`: chi tiết của chính khách, bank snapshot đầy đủ, policy/version/amount/status, reason/note, thời gian duyệt/hoàn, rejectReason, transferReference. EvidenceReference nội bộ không trả cho khách.
- Gửi lại key cũ trả request cũ, không sinh yêu cầu mới. Sau Rejected có thể gửi key mới nếu policy/hạn mức còn cho phép.
- Request đã submit không sửa bank details/amount. Khách sửa hồ sơ không làm thay đích chuyển khoản đã chốt.

## Màn Staff

Role Staff; không chỉ Admin. JWT customer không vào được queue/actions staff.

`GET /api/v1/staff/refund-requests?status=PendingReview&page=1&pageSize=20`; status có thể PendingReview/Approved/Rejected/Done, bỏ status xem tất cả. Mặc định page 1, pageSize 20, tối đa 100. List che account; `GET .../{id}` có bank details và policy snapshot summary để xử lý.

| State | Ý nghĩa | Thao tác |
|---|---|---|
| PendingReview | Đã giữ hạn mức, chưa được duyệt | Approve hoặc Reject |
| Approved | Đã duyệt, chưa ghi nhận tiền đã hoàn | Staff được phân công chuyển tay, sau đó Done |
| Rejected | Bị từ chối, hạn mức được nhả | Chỉ xem lịch sử; khách có thể gửi mới |
| Done | Staff đã xác nhận chuyển thành công | Chỉ xem/replay cùng thông tin; không sửa |

Trước approve, staff đối chiếu vấn đề/chứng từ, số tiền theo policy và thông tin người nhận với ngân hàng. Nếu thông tin sai, reject khi còn PendingReview. Kế hoạch hiện không mở undo approval/reassign; không sửa trực tiếp bank details hoặc reset Approved bằng SQL. Approved chưa chuyển hoặc chưa rõ kết quả không được tính là Done.

### Approve

`POST /api/v1/staff/refund-requests/{id}/approve`

Staff duyệt được gán làm người xử lý. Approve không tạo Refund Succeeded, không gửi tiền. Policy snapshot được giữ nguyên; server kiểm tra lại tổng giữ hạn mức và nguồn tiền. Replay cùng staff không ghi thêm quyết định; staff khác không chiếm assignment đã có.

### Reject

`POST .../{id}/reject`

```json
{"reason":"Chứng từ hoặc thông tin nhận tiền chưa hợp lệ"}
```

Chỉ PendingReview; reason bắt buộc 1–2000 ký tự. Nhả reservation, giữ audit và thông báo khách. Không reject Done hoặc silently undo Approved.

### Done sau chuyển tay

Chỉ assigned staff thực hiện. Khi ngân hàng đã báo chuyển thành công:

`POST .../{id}/done`, header `Idempotency-Key`.

```json
{
  "transferReference":"MB-20261101-000123",
  "transferredAt":"2030-01-01T10:00:00+07:00",
  "evidenceReference":"<tham chiếu chứng từ nội bộ, có thể bỏ>"
}
```

Thời gian phải sau/lúc approve, không ở tương lai. Reference 1–200 ký tự, chuẩn hóa trim/uppercase; dùng reference phân biệt được giao dịch/kênh ngân hàng, không ghi chung chung “đã chuyển”. Một reference không dùng cho hai request Done. EvidenceReference tối đa 1000, là storage/reference hiện có; API này không tạo hệ upload mới.

Server ghi một Refund payment Succeeded và cập nhật tổng đã hoàn/order Refunded hoặc PartiallyRefunded. Audit có actor/time/state, thông báo khách cùng transaction. Không tự hủy order khi hoàn do ServiceIssue.

Nếu ngân hàng chuyển thành công nhưng API Done timeout, kiểm tra trạng thái rồi retry **cùng request/key/reference/time**, không chuyển ngân hàng lần nữa. Replay cùng payload an toàn; payload khác khi đã Done trả 409. Idempotency chỉ bảo vệ ghi sổ nội bộ; assignment và kiểm tra reference/status giúp staff tránh lặp thao tác bên ngân hàng.

## Policy, nguồn thu và dữ liệu cũ

- Tính trên policy/version/rule gắn với order và mốc nghiệp vụ thích hợp; snapshot lưu base, phần trăm, entitlement và thời điểm đánh giá.
- `available = max(0, min(entitlement - done - reserved, verifiedCollected - done - reserved))`.
- PendingReview/Approved cùng giữ reservation. Approve không trừ chính request lần hai. Rejected nhả; Done chuyển sang actual refund.
- Đơn 1.000.000, policy 50%: tổng hoàn tối đa 500.000. Sau Done 500.000 không tiếp tục áp 50% lên 500.000 còn lại để hoàn thêm 250.000.
- Một yêu cầu có thể phân bổ trên nhiều giao dịch thu; staff chuyển một khoản tổng. Không còn điều kiện provider chỉ cho hoàn về giao dịch gốc.
- Nếu tiền thực nhận tăng sau yêu cầu đầu, hạn mức order có thể tăng trong phạm vi base/tỷ lệ policy; amount của request đã gửi không bị sửa. Ví dụ base 400.000, policy 50%: nhận 100.000 tạo request 50.000; nhận thêm 300.000 thì có thể tạo request bổ sung 150.000. Tổng vẫn chỉ 200.000, không tính lại tỷ lệ trên số dư.
- Thiếu nguồn thu verified hoặc rule phù hợp: không tự cho hoàn 100%; legacy Paid cần đối soát/replay giao dịch gốc chính thức trước khi mở quyền lợi. Không tự suy ra giao dịch ngân hàng từ amount cũ.
- Tài khoản nhận và payload được bảo vệ bằng ASP.NET Data Protection; mọi instance/restart phải dùng chung key ring bền vững. Không đưa dữ liệu này vào log hoặc analytics.

## Lỗi nghiệp vụ

| HTTP | Mã | Ý nghĩa |
|---|---|---|
| 400 | REFUND_INVALID_BANK_DETAILS | Form tài khoản/lý do không hợp lệ |
| 400 | REFUND_REASON_UNSUPPORTED | Reason chưa có trong contract |
| 409 | POLICY_NOT_ELIGIBLE / REFUND_POLICY_UNAVAILABLE | Không đủ điều kiện/thiếu policy |
| 409 | VERIFIED_COLLECTION_REQUIRED | Cần đối soát nguồn thu |
| 409 | REFUND_ENTITLEMENT_EXHAUSTED | Đã hoàn hoặc đang giữ toàn bộ hạn mức |
| 409 | REFUND_BUDGET_CHANGED | Nguồn thu/hạn mức cần kiểm tra lại |
| 409 | REFUND_STATE_INVALID | Thao tác không đúng state |
| 403 | — | Sai role hoặc không phải assigned staff |
| 409 | REFUND_ALREADY_COMPLETED | Đã Done với payload khác |
| 409 | REFUND_TRANSFER_ALREADY_RECORDED | Reference đã ghi cho request khác |
| 400 | REFUND_COMPLETION_INVALID | Reference/thời gian chứng từ không hợp lệ |

Tài liệu là contract BE cho hai màn; chưa có implementation FE trong repo này.
