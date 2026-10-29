# Hướng dẫn FE: luồng đặt chuyến đi

Tài liệu mô tả cách FE gọi API booking hiện có. Hợp đồng chi tiết và mã lỗi chuẩn nằm trong [`03-API-CONTRACT.md`](03-API-CONTRACT.md#3-trip-bookings).

## 1. Luồng tổng quát

1. Người dùng đăng nhập. FE giữ `accessToken` theo cơ chế bảo mật hiện tại của ứng dụng.
2. Ở trang chi tiết chuyến đi, FE gửi `POST /api/v1/trip-bookings` với `tripId` và thông tin người đi cùng. Người mua được lấy từ tài khoản đăng nhập và tự động là participant đầu tiên.
3. BE tạo booking `PendingPayment` / `Unpaid`, giữ chỗ 15 phút và trả `orderId`, tổng tiền, participant cùng thời hạn giữ chỗ.
4. FE gọi `POST /api/v1/orders/{orderId}/payments` để khởi tạo thanh toán chuyển khoản. BE trả thông tin tài khoản, nội dung chuyển khoản và link ảnh QR (VietQR qua SePay).
5. FE hiển thị QR. Người dùng quét bằng app ngân hàng và chuyển khoản. SePay gọi webhook về BE; BE tự đối soát và chuyển đơn sang `Confirmed` / `Paid`.
6. FE poll `GET /api/v1/orders/{orderId}/payments/{paymentId}` cho đến khi thanh toán `Succeeded` hoặc hết thời hạn giữ chỗ.
7. Chỉ sau khi BE xác nhận đơn `Confirmed` và thanh toán `Paid` đầy đủ, buyer gọi API tạo lời mời cho participant chưa có tài khoản liên kết.
8. FE chuyển token lời mời tới đúng participant qua kênh đã được sản phẩm hỗ trợ. Participant đăng ký/đăng nhập bằng email đã khai báo, rồi claim token.
9. FE tải lại booking để hiển thị trạng thái mới.

> FE không tự đánh dấu đã thanh toán và không gọi webhook. Chỉ BE (qua webhook SePay) được cập nhật trạng thái thanh toán; bước 7 chỉ gọi khi BE đã trả `Confirmed` / `Paid`.

## 2. Quy ước gọi API

- Base URL: `https://localhost:<port>/api/v1` (local); môi trường khác lấy từ cấu hình FE.
- JSON: gửi `Content-Type: application/json`.
- Các endpoint bên dưới yêu cầu `Authorization: Bearer <accessToken>`.
- Response luôn có envelope `success`, `statusCode`, `message`, `data`, `errors`, `timestamp`. FE đọc payload nghiệp vụ từ `data`.
- JSON dùng camelCase. ID là UUID; ngày sinh dùng `YYYY-MM-DD`; thời gian chuyến đi dùng ISO 8601.

## 3. Tạo booking

`POST /trip-bookings`

Tạo một `Idempotency-Key` mới cho mỗi lần người dùng chủ động gửi booking. Nếu request bị timeout hoặc FE không biết kết quả, retry đúng payload với **cùng key**. Không dùng lại key đó cho payload đã chỉnh sửa.

```http
Authorization: Bearer <accessToken>
Content-Type: application/json
Idempotency-Key: <unique-key-per-submit>
```

```json
{
  "tripId": "00000000-0000-0000-0000-000000000000",
  "note": "Đi cùng nhóm bạn",
  "additionalParticipants": [
    {
      "fullName": "Nguyễn Văn B",
      "email": "b@example.com"
    }
  ]
}
```

Mỗi người đi cùng chỉ cần `fullName` và `email`. BE vẫn nhận các trường tuỳ chọn `phone`, `dateOfBirth`, `emergencyContactName`, `emergencyContactPhone` nhưng FE hiện không gửi. `additionalParticipants` có thể là mảng rỗng; tối đa 20 người. Không gửi thông tin buyer trong body. BE lấy tên, email, điện thoại buyer từ account hiện tại. Email người đi cùng phải hợp lệ và không trùng nhau hoặc trùng email buyer.

Giá và số chỗ do BE tính từ chuyến đi. FE không gửi giá, tổng tiền, số lượng participant hay trạng thái thanh toán.

Thành công trả `201 Created`; lưu `data.orderId` để xem booking và tiếp tục luồng thanh toán. `data.holdExpiresAt` là thời hạn giữ chỗ. Hiển thị bộ đếm nếu phù hợp; khi hết hạn, tải lại booking và xử lý trạng thái BE trả về.

## 4. Xem hoặc làm mới booking

`GET /trip-bookings/{orderId}`

Gọi sau khi tạo, khi quay lại trang booking, sau khi thanh toán, và sau khi claim lời mời. Chỉ buyer hoặc participant đã liên kết mới xem được.

Buyer có `canManage: true` và nhận đủ tổng tiền, trạng thái thanh toán, thời hạn giữ chỗ, danh sách participant. Participant có `canManage: false`; các trường tiền được trả `0`, `paymentStatus` rỗng, `holdExpiresAt` là `null`, và chỉ nhận participant của mình.

Các trạng thái cần hiển thị lấy trực tiếp từ response, không tự suy diễn thanh toán từ giao diện cổng thanh toán:

- `status: "PendingPayment"`, `paymentStatus: "Unpaid"`: đang chờ thanh toán.
- `status: "Confirmed"`, `paymentStatus: "Paid"`: booking đã thanh toán; buyer có thể tạo lời mời.
- `paymentStatus: "NeedsReconciliation"`: tiền đã vào nhưng không tự xác nhận được (sai số tiền, chuyển sau khi hết giữ chỗ...). Hiển thị "Đang chờ đối soát, vui lòng liên hệ hỗ trợ"; không cho tạo thanh toán mới.

## 5. Thanh toán chuyển khoản và tạo QR

Phương thức duy nhất hiện tại là `BankTransfer` (VietQR, đối soát tự động qua SePay). Các endpoint yêu cầu role `Customer` và chỉ buyer của đơn gọi được.

### 5.1. Khởi tạo thanh toán

`POST /orders/{orderId}/payments`

```http
Authorization: Bearer <accessToken>
Content-Type: application/json
```

```json
{
  "paymentMethod": "BankTransfer"
}
```

Điều kiện: đơn đang `PendingPayment` / `Unpaid` và chưa hết `holdExpiresAt`.

- `201 Created`: tạo lần thanh toán mới.
- `200 OK`: đơn đã có lần thanh toán `Pending`; BE trả lại đúng lần đó (cùng `paymentId`, cùng nội dung chuyển khoản). Vì vậy FE gọi lại an toàn khi người dùng quay lại trang thanh toán hoặc F5; không cần `Idempotency-Key`.

Response `data`:

```json
{
  "paymentId": "3f2a9c1e-7b4d-4e8a-9c0f-1a2b3c4d5e6f",
  "orderCode": "RT-20261028-0001",
  "amount": 1500000,
  "currency": "VND",
  "status": "Pending",
  "expiresAt": "2026-10-28T15:30:00Z",
  "bankTransfer": {
    "bankName": "MBBank",
    "accountNumber": "0123456789",
    "accountName": "RENTREK",
    "transferContent": "RTP3F2A9C1E7B4D4E8A",
    "qrPayload": null,
    "qrImageUrl": "https://qr.sepay.vn/img?acc=0123456789&bank=MBBank&amount=1500000&des=RTP3F2A9C1E7B4D4E8A"
  }
}
```

Lưu `paymentId` để poll trạng thái. `expiresAt` chính là thời hạn giữ chỗ của đơn.

### 5.2. Hiển thị QR

`bankTransfer.qrImageUrl` là ảnh PNG VietQR đã điền sẵn tài khoản, số tiền và nội dung. FE **không cần thư viện tạo QR**, chỉ render ảnh:

```tsx
function PaymentQr({ payment }: { payment: PaymentResponse }) {
  const bt = payment.bankTransfer;
  return (
    <div>
      <img src={bt.qrImageUrl} alt={`QR chuyển khoản ${payment.orderCode}`} width={280} height={280} />
      <dl>
        <dt>Ngân hàng</dt><dd>{bt.bankName}</dd>
        <dt>Số tài khoản</dt><dd>{bt.accountNumber}</dd>
        <dt>Chủ tài khoản</dt><dd>{bt.accountName}</dd>
        <dt>Số tiền</dt><dd>{payment.amount.toLocaleString("vi-VN")} ₫</dd>
        <dt>Nội dung</dt><dd>{bt.transferContent}</dd>
      </dl>
    </div>
  );
}
```

Quy tắc hiển thị:

- Luôn hiển thị thêm thông tin chuyển khoản dạng text kèm nút copy cho người dùng chuyển tay (không quét được QR).
- Nhắc người dùng **giữ nguyên nội dung** `transferContent` và **chuyển đúng số tiền**. Sai nội dung thì BE không nhận diện được; sai số tiền thì đơn chuyển sang `NeedsReconciliation`.
- Không tự ghép `qrImageUrl` ở FE; luôn dùng giá trị BE trả về.
- Hiển thị bộ đếm ngược tới `expiresAt`. Hết hạn thì ẩn QR và không cho chuyển tiếp.

### 5.3. Theo dõi trạng thái thanh toán

`GET /orders/{orderId}/payments/{paymentId}`

```json
{
  "paymentId": "3f2a9c1e-7b4d-4e8a-9c0f-1a2b3c4d5e6f",
  "status": "Succeeded",
  "amount": 1500000,
  "expiresAt": "2026-10-28T15:30:00Z",
  "orderStatus": "Confirmed",
  "orderPaymentStatus": "Paid",
  "processedAt": "2026-10-28T15:21:07Z",
  "failureReason": null
}
```

Poll mỗi 3–5 giây khi màn hình QR đang mở; dừng khi tab ẩn (`document.visibilityState`) và poll lại ngay khi tab hiện. Dừng hẳn khi:

| Điều kiện | Hành vi |
|---|---|
| `status: "Succeeded"` và `orderPaymentStatus: "Paid"` | Báo thành công, chuyển sang trang booking. |
| `orderPaymentStatus: "NeedsReconciliation"` | Báo đang chờ đối soát; hướng dẫn liên hệ hỗ trợ. |
| `status` là `Failed` hoặc `Cancelled` | Báo thất bại, hiển thị `failureReason` nếu có. |
| Quá `expiresAt` | Poll thêm 1 lần (webhook có thể tới chậm), rồi báo hết giữ chỗ và tải lại booking. |

```ts
async function pollPayment(orderId: string, paymentId: string, signal: AbortSignal) {
  while (!signal.aborted) {
    const res = await api.get(`/orders/${orderId}/payments/${paymentId}`, { signal });
    const p = res.data.data;
    if (p.status !== "Pending" || p.orderPaymentStatus !== "Unpaid") return p;
    if (p.expiresAt && Date.now() > Date.parse(p.expiresAt) + 10_000) return p;
    await new Promise((r) => setTimeout(r, 4000));
  }
}
```

### 5.4. Webhook (chỉ BE)

`POST /payments/webhooks/sepay` do SePay gọi, xác thực bằng header `Authorization: Apikey <key>`. FE **không gọi** endpoint này và không giữ API key. Muốn test local: dùng ngrok trỏ URL webhook SePay về BE, hoặc giả lập request bằng Swagger/Postman với đúng header.

## 6. Tạo lời mời participant

`POST /trip-bookings/{orderId}/participant-invitations`

Chỉ buyer gọi sau khi API xem booking trả về `Confirmed` và `Paid`. API cũng kiểm tra tổng payment thành công đã đủ. Trước điều kiện này, BE trả `409 Conflict`.

Response `data` là mảng lời mời:

```json
[
  {
    "participantId": "00000000-0000-0000-0000-000000000000",
    "fullName": "Nguyễn Văn B",
    "email": "b@example.com",
    "token": "one-time-token",
    "expiresAt": "2026-11-01T00:00:00Z"
  }
]
```

FE cần chuyển token đến participant tương ứng. Token thô chỉ được trả ở lần tạo lời mời; BE chỉ lưu hash. Lời mời hết hạn sau tối đa 7 ngày hoặc khi chuyến đi bắt đầu. Gọi lại endpoint sẽ thu hồi các lời mời đang chờ trước khi tạo token mới. Vì vậy, chỉ gọi khi người dùng yêu cầu gửi/tạo lại lời mời và xử lý token trả về ngay.

Không ghi token vào log, analytics, URL hoặc thông báo không dành riêng cho participant. API chỉ tạo token; contract hiện chưa mô tả gửi email tự động.

## 7. Participant nhận lời mời

Participant cần tài khoản đang hoạt động với email trùng email participant trong booking. Nếu chưa có tài khoản, đăng ký bằng email đó trước; sau đó đăng nhập để lấy `accessToken`.

`POST /trip-bookings/participant-invitations/claim`

```json
{
  "token": "one-time-token"
}
```

Gửi cùng `Authorization: Bearer <accessToken>`. Thành công trả booking trong phạm vi participant. Token dùng một lần. Sau thành công, FE tải lại trang booking hoặc dùng `data` trả về để cập nhật giao diện.

## 8. Lỗi FE cần xử lý

| HTTP | Trường hợp | Hành vi gợi ý |
|---|---|---|
| `400` | Thiếu/sai dữ liệu hoặc `Idempotency-Key`; `paymentMethod` khác `BankTransfer` (`VALIDATION_ERROR`) | Hiển thị lỗi theo field/message; giữ dữ liệu form để sửa. |
| `401` | Token thiếu/hết hạn | Yêu cầu đăng nhập lại theo auth flow của ứng dụng. |
| `403` | Account không hoạt động, không có role `Customer`, hoặc email claim không khớp | Báo người dùng đăng nhập đúng tài khoản. |
| `404` | Trip, booking, token không tồn tại/không thuộc quyền xem; `ORDER_NOT_FOUND`, `PAYMENT_NOT_FOUND` | Hiển thị trạng thái không tìm thấy; không tiết lộ booking của người khác. |
| `409` | Trip đóng/hết chỗ, key dùng với payload khác, booking chưa thanh toán, token hết hạn/đã dùng | Tải lại dữ liệu liên quan; cho phép người dùng sửa hoặc yêu cầu buyer tạo lời mời mới khi phù hợp. |
| `409` | `ORDER_NOT_PAYABLE`: đơn không còn chờ thanh toán | Tải lại booking; nếu đã `Paid` thì chuyển sang trang booking. |
| `409` | `HOLD_EXPIRED`: hết thời gian giữ chỗ | Báo hết hạn, cho người dùng tạo booking mới. |

Đọc `message` và `errors` từ envelope để hiển thị chi tiết. Không phụ thuộc vào câu chữ cố định nếu FE hỗ trợ đa ngôn ngữ.

## 9. Endpoint tóm tắt

| Mục đích | Method | Path | Ai gọi |
|---|---|---|---|
| Tạo booking và giữ chỗ | `POST` | `/trip-bookings` | User đăng nhập |
| Xem booking | `GET` | `/trip-bookings/{orderId}` | Buyer hoặc participant đã liên kết |
| Khởi tạo thanh toán, lấy QR | `POST` | `/orders/{orderId}/payments` | Buyer (role `Customer`) |
| Poll trạng thái thanh toán | `GET` | `/orders/{orderId}/payments/{paymentId}` | Buyer (role `Customer`) |
| Webhook SePay | `POST` | `/payments/webhooks/sepay` | Chỉ SePay, FE không gọi |
| Tạo lại lời mời | `POST` | `/trip-bookings/{orderId}/participant-invitations` | Buyer, sau khi thanh toán đủ |
| Claim participant | `POST` | `/trip-bookings/participant-invitations/claim` | Participant đăng nhập đúng email |

Path trên được ghép với base URL `/api/v1`. Contract chuẩn: [`03-API-CONTRACT.md`](03-API-CONTRACT.md#3-trip-bookings).
