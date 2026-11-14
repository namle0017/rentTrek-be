# 06 — Luồng thuê đồ có cọc, phạt, giao nhận có bằng chứng và hoàn tiền

Áp dụng cho đơn thuê thiết bị từ migration `database/migrations/20261114_rental_deposit_policies.sql`.
Bổ sung cho `03.1-API-CONTRACT-RENTAL-CUSTOMER.md` (thay các mục "không thu cọc", D-1, D-5) và `05-MANUAL-REFUND-WORKFLOW.md` (thêm nguồn hoàn từ đơn thuê).

## 1. Chính sách (bảng `rental_policies` + `rental_policy_tiers`, có version)

| Mục | Mặc định |
|---|---|
| Cọc | 20% tiền thuê **trước giảm giá**, làm tròn lên 1.000đ |
| Thanh toán | Một lần: `totalPayable = totalAmount (sau giảm) + depositAmount` |
| Huỷ miễn phí | Trước giờ nhận + 24h, hoàn 100%; tối đa 2 lần / 30 ngày, từ lần 3 mất 5% cọc |
| Trễ nhận | ≥1 ngày 5% cọc · ≥2 ngày 20% cọc · ≥3 ngày (hoặc hết thời gian thuê) = no-show, mất 100% cọc |
| Trả trễ | Sau 2h ân hạn: tiền thuê ngày × số ngày trễ, **cộng** phạt ≥3 ngày 5% · ≥4 ngày 20% · ≥5 ngày 100% cọc |
| Gia hạn | Được tới hết ngày trễ thứ 2; phải thanh toán trước, hạn trả chỉ đổi khi tiền vào |
| Nghi mất | Trễ ≥7 ngày → report `SuspectedLoss`; admin duyệt = bồi thường giá trị thiết bị |
| Xác nhận nhận đồ | Khách bấm, hoặc tự xác nhận sau 24h |
| Ảnh bằng chứng | Tối thiểu 1 ảnh / thiết bị lúc giao và lúc nhận lại (BE chặn nếu thiếu) |

- Ngày trễ = số khối đủ 24 giờ từ giờ nhận (trễ nhận) hoặc hạn trả (trễ trả). Logic ở `Helpers/RentalPenaltyCalculator.cs`.
- Phí theo bậc là một dòng `rental_charges` / loại (`LatePickup`, `LateReturnPenalty`, `CancellationFee`), chỉ được nâng bậc.
- Đơn snapshot `rental_orders.rental_policy_id`; admin tạo version mới ở `POST /admin/rental-policies` (đơn cũ giữ version cũ).
- Giá trị thiết bị: `product_variants.replacement_value` / `product_bundles.replacement_value`, chụp vào `rental_order_items.unit_replacement_value` lúc đặt. Chưa nhập thì staff / admin lập phí mất thủ công.

## 2. Chống phá kho

- Chỉ tài khoản đã xác thực email (`EMAIL_NOT_VERIFIED`) và không còn đơn `AwaitingSettlement` (`RENTAL_OUTSTANDING_DEBT`) mới đặt thuê được.
- Phải thanh toán trước; giới hạn huỷ miễn phí theo cửa sổ 30 ngày.

## 3. Trạng thái

- `orders.status` thêm `AwaitingSettlement` (phí vượt cọc, chờ khách trả thêm).
- `rental_orders.cancellation_type`: `Unpaid | FreeCancel | OverLimitCancel | LateCancel | NoShow | Admin`.
- `refund_cases.status` thêm `AwaitingBankInfo`; `refund_cases.source`: `TripCancellation | RentalCancellation | RentalNoShow | RentalDepositReturn`.
- `rental_reports`: `NoShow | LateCancellation | ReturnInspection | SuspectedLoss | ExtraPayment` × `Submitted | Approved | Rejected | Resolved`.
- `rental_order_adjustments.status`: `Pending` (gia hạn chờ thanh toán, đã giữ chỗ thiết bị) → `Applied | Cancelled` (hết hạn giữ chỗ).

## 4. Endpoint mới

| Ai | Endpoint | Ghi chú |
|---|---|---|
| Khách | `GET /rental-orders/{id}/cancellation-preview` | Phí, số hoàn, loại huỷ, lượt huỷ miễn phí đã dùng |
| Khách | `POST /rental-orders/{id}/cancel-paid` | `expectedRefundAmount` + STK + consent; lệch số → 409 `errors.refundAmount` |
| Khách | `POST /rental-orders/{id}/confirm-receipt` | Đã nhận & kiểm tra |
| Khách | `POST /rental-orders/{id}/issue-photos` | multipart, ảnh báo lỗi lúc nhận |
| Khách | `POST /rental-orders/{id}/extensions` | Có tiền thêm → tạo payment `RentalExtension` Pending; trả bằng `POST /orders/{id}/payments` |
| Khách | `POST /refund-cases/{id}/bank-info` | Hồ sơ `AwaitingBankInfo` → `Requested` (STK mã hoá) |
| Mọi người | `GET /rental-policies/active` | Chính sách đang áp dụng |
| Staff | `POST /admin/rental-orders/{id}/evidence-photos` | multipart: `file`, `stage` (Handover/Return/Damage), `assignmentId`, `note` |
| Staff | `DELETE /admin/rental-orders/{id}/evidence-photos/{photoId}` | Chỉ khi bước tương ứng chưa xong |
| Staff | `POST /admin/rental-orders/{id}/no-show` | Chỉ khi đã tới mốc no-show |
| Staff | `POST /admin/rental-orders/{id}/reports` | Báo cáo `ReturnInspection` sau khi nhận lại đủ |
| Admin | `POST /admin/rental-orders/{id}/complete` | Quyết toán cọc trực tiếp (staff bị chặn) |
| Staff/Admin | `GET /admin/rental-reports`, `GET /admin/rental-reports/{id}` | |
| Admin | `POST /admin/rental-reports/{id}/approve` · `/reject` | `{ reviewNote, expectedRowVersion }` |
| Staff/Admin | `GET /admin/rental-policies` · Admin `POST` | Version chính sách |

Chi tiết đơn của khách bổ sung: `rentalPolicy`, `refundCase`, `settlement`, `pendingAdditionalPayment`, `evidencePhotos`,
`handedOverAt`, `customerReceivedConfirmedAt`, `cancellationType`, `allowedActions.{canCancelPaid, canConfirmReceipt, canSubmitBankInfo, canPayAdditional, canExtend}`.

## 5. Luồng

1. **Đặt + trả**: quote/create tính cọc → `POST /orders/{id}/payments` tạo payment `Amount = tiền thuê + cọc` → webhook SePay khớp đúng số → `Confirmed/Paid`.
2. **Huỷ trước khi nhận**: preview → cancel-paid → phí (nếu có) + huỷ đơn, nhả kho → RefundCase `Requested` (có STK ngay) → admin export Excel → kế toán chuyển → admin complete.
3. **Job mỗi 15 phút** (`RentalLifecycleBackgroundService`): nâng bậc phí trễ nhận + mail; no-show tự huỷ, mất cọc, RefundCase `AwaitingBankInfo` (hoàn phần tiền thuê) + report `NoShow`; nhắc trả T−24h / đúng hạn; phạt trả trễ; report `SuspectedLoss`; tự xác nhận nhận đồ; nhắc nhập STK (3 / 7 ngày); huỷ gia hạn chưa thanh toán. Mỗi việc chạy scope riêng, idempotent, email gửi sau commit.
4. **Giao**: staff chụp ảnh từng thiết bị → handover (thiếu ảnh → 409 `RENTAL_EVIDENCE_REQUIRED`) → mail khách xác nhận.
5. **Nhận lại**: staff chụp ảnh → returns (tự tính tiền thuê ngày trễ + phạt bậc, đề xuất phí mất = giá trị thiết bị) → lập phí hư hỏng (staff = nháp) → gửi report `ReturnInspection`.
6. **Admin duyệt**: phí nháp → chốt; quyết toán `phí vs cọc` (`rental_settlements`): dư → RefundCase `AwaitingBankInfo` (`RentalDepositReturn`) + mail nhập STK, đơn `Completed`; thiếu → payment `AdditionalCharge` + report `ExtraPayment`, đơn `AwaitingSettlement`; khách trả QR → webhook → `Completed`.

## 6. Triển khai

1. Chạy `database/migrations/20261114_rental_deposit_policies.sql` trên DB (idempotent, có seed policy mặc định).
2. Nhập `replacement_value` cho biến thể / combo ở trang quản trị sản phẩm.
3. Cấu hình SMTP để email luồng cọc gửi thật (chưa cấu hình thì in ra console).
