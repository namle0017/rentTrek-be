# Thu tiền SePay và polling

API dùng JWT Customer cho create/poll; webhook dùng xác thực riêng. Không gọi API hoàn tiền SePay. Hoàn tiền xem [06-MANUAL-REFUND-WORKFLOW.md](06-MANUAL-REFUND-WORKFLOW.md).

## Tạo hoặc lấy lại hướng dẫn thanh toán

`POST /api/v1/orders/{orderId}/payments`

Headers: `Authorization: Bearer ...`, `Idempotency-Key: <1–100 ký tự>`.

```json
{"paymentMethod":"BankTransfer"}
```

201 khi tạo mới; 200 khi replay hoặc dùng lại intent đang mở. Cùng key/cùng order trả cùng payment ID, kể cả order đã Paid; khác request với cùng key trả 409. Body không được quyết định amount hoặc idempotency key. Order không thuộc customer trả 404.

```json
{
  "paymentId":"<uuid>",
  "orderCode":"RT-...",
  "amount":400000,
  "receivedAmount":100000,
  "remainingAmount":300000,
  "currency":"VND",
  "status":"Pending",
  "expiresAt":"2030-01-01T00:15:00+00:00",
  "bankTransfer":{
    "bankName":"MBBank",
    "accountNumber":"<tài khoản thu>",
    "accountName":"RENTREK",
    "transferContent":"RTP<16 ký tự hex>",
    "qrPayload":null,
    "qrImageUrl":"https://qr.sepay.vn/img?...&amount=300000&des=RTP..."
  }
}
```

Ví dụ trên là trường `data` trong envelope `ApiResponse`. `amount` là tổng yêu cầu ban đầu; QR refresh dùng số còn thiếu. `receivedAmount` thuộc intent đang xem; `remainingAmount` tính tiền verified của cả order. Không đổi nội dung chuyển khoản. QR null khi đã đủ tiền, đơn không còn payable hoặc hold đã hết.

FE refresh bằng cùng key/intent hoặc poll, không tự tạo mã chuyển khoản. Chưa có luồng chuyển khoản amount 0: trả `ORDER_AMOUNT_INVALID`; free checkout nếu cần phải được thiết kế riêng.

## Poll

`GET /api/v1/orders/{orderId}/payments/{paymentId}`

Các field: `paymentId`, `status`, `amount`, `receivedAmount`, `remainingAmount`, `expiresAt`, `orderStatus`, `orderPaymentStatus`, `reconciliationStatus` (`None/NeedsReview`), `refundStatus` (nullable hoặc trạng thái request gần nhất), `processedAt`, `failureReason`.

- Chỉ coi booking/thanh toán được xác nhận khi order `Confirmed` và `Paid`; tiền đã nhận không tự đồng nghĩa order được xác nhận.
- Chuyển thiếu được cộng các giao dịch hợp lệ trong hold. QR refresh chỉ yêu cầu phần còn thiếu.
- Tại đúng deadline hoặc xử lý sau deadline: tiền vẫn có ledger nhưng không tự Confirmed. FE hiện chờ staff đối soát, không yêu cầu trả lại toàn bộ lần nữa.
- Phần dư/tiền nhận thêm của đơn Paid có queue đối soát; order vẫn giữ Paid. Không tự hoàn tiền.
- Receipt được lưu trước ACK; worker xử lý theo chu kỳ khoảng 5 giây. FE có thể poll mỗi 3–5 giây khi màn đang mở và dừng khi có kết quả cuối. Không coi HTTP webhook 200 là order đã Paid.
- Giữ API invitation/group code của nhánh B; không đưa token contract cũ từ nhánh A vào FE.
- Chi tiết đơn thuê cũng có `payments[].receivedAmount`; `amounts.paidAmount` tính tiền đã nhận kể cả chuyển thiếu. Dữ liệu Succeeded cũ có fallback hiển thị theo amount lịch sử; quyền refund vẫn yêu cầu nguồn thu verified. Đơn Expired không có timeline Paid gây hiểu nhầm và hold null không bật canPay.

## Hủy đơn

`POST /api/v1/orders/{orderId}/cancel` (Customer owner).

Hủy PendingPayment hoặc Confirmed trước lúc dịch vụ bắt đầu; thao tác lặp trên Cancelled an toàn. Hủy trả tài nguyên/promotion phù hợp và lưu audit. Hủy không tự gửi tiền, không tự Done refund; khách phải gửi refund request có tài khoản nhận. Không tự hủy order khi khách chỉ gửi report ServiceIssue/PaymentIssue.

## Webhook

`POST /api/v1/payments/webhooks/sepay`, JSON tối đa 32 KiB. Không cần JWT; cần một mode xác thực đã cấu hình:

- ApiKey: `Authorization: Apikey <key>`.
- Hmac: `X-SePay-Signature: sha256=<hex>`, `X-SePay-Timestamp: <unix seconds>`, HMAC-SHA256 của `timestamp.raw_body`, cửa sổ ±300 giây. Không fallback sang key khi HMAC sai. Xem [SePay authentication](https://developer.sepay.vn/vi/sepay-webhooks/xac-thuc).

BankName phải khớp `gateway` trong webhook và mã QR của ngân hàng; AccountNumber khớp chính xác. Nếu cấu hình SubAccount thì phải khớp. Sai account/bank/direction/amount giữ receipt Rejected, không tạo khoản thu hợp lệ. Mã thiếu/mơ hồ vẫn lưu tiền đã xác minh thành Unmatched.

Response sau durable commit: `{"success":true}`; receipt duplicate cũng ACK. Cùng provider ID khác payload được đánh dấu conflict để review, không ghi đè. Lỗi auth 401, ID/JSON sai 400, lỗi lưu DB không ACK thành công. Worker không có chức năng chi tiền.

## Đối soát Staff

Role Staff:

- `GET /api/v1/staff/payment-reconciliation?page=1&pageSize=20`: unmatched, rejected, conflict và tiền cần review; list không trả raw account.
- `GET .../receipts/{receiptId}`: chi tiết payload gốc đã giải mã, chỉ staff có quyền.
- `POST .../transactions/{transactionId}/match`: body `{"paymentId":"<uuid>","reason":"Đã kiểm tra chứng từ"}`. Chỉ ghép khoản thu đã xác minh, chưa ghép; không force-match sai account/conflicting receipt. Replay cùng nguồn/intent không cộng lại tiền.
- `POST .../orders/{orderId}/accept`: chỉ PendingPayment còn hold, đủ tiền verified, không có refund mới/cũ đang chờ hoặc đã chi; không phục hồi Expired/Cancelled. Phần dư vẫn ở queue.

## Các mã lỗi chính

| HTTP | Mã | Cách FE xử lý |
|---|---|---|
| 400 | IDEMPOTENCY_KEY_REQUIRED | Gửi key ổn định cho thao tác |
| 409 | IDEMPOTENCY_KEY_REUSED | Không dùng lại key cho body/order khác |
| 409 | HOLD_EXPIRED / ORDER_NOT_PAYABLE | Dừng hiển thị QR, refresh trạng thái |
| 409 | ORDER_AMOUNT_INVALID | Không hướng dẫn chuyển khoản; báo lỗi đơn |
| 404 | ORDER_NOT_FOUND / PAYMENT_NOT_FOUND | Không tồn tại hoặc không thuộc customer |
| 409 | ORDER_NOT_RECONCILABLE | Staff không thể accept đơn này |

JWT, envelope lỗi và revocation giữ theo nền B. Không đưa webhook secret/API key hoặc key ring ra FE.
