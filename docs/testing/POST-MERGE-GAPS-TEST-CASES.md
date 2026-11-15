# Test cases: Dọn sau merge + sửa luồng gãy (Giai đoạn 0 và Giai đoạn 1)

Phạm vi: plan `t-i-c-n-b-n-l-n-functional-charm`, Giai đoạn 0 (mục 1-6), Giai đoạn 1 (mục 1-10) và mục 1.1 (huỷ đơn chưa thanh toán).
Cột Result: `Not run` cho tới khi lead chạy. Cột Auto cho biết test tự động tương ứng (nếu có); để trống nghĩa là chỉ kiểm thủ công hoặc test BE do BE dev viết.

Quy ước file Auto (FE, `npx vitest run <file>`):
- A1 `FE/src/routes/router.phase01.test.js`
- A2 `FE/src/features/trip-bookings/components/CancelBookingPanel.phase01.test.jsx`
- A3 `FE/src/features/rentals/components/RentalIssueReportModal.phase01.test.jsx`
- A4 `FE/src/features/posts/components/PostEditorForm.phase01.test.jsx`
- A5 `FE/src/pages/ops/warehouse/HandoverChecklistPage.phase01.test.jsx`

## 1. Giai đoạn 0: dọn sau merge

| ID | Plan | Precondition | Steps | Expected | Type | Pri | Auto | Result |
|----|------|--------------|-------|----------|------|-----|------|--------|
| TC-PM-001 | 0.4, GĐ1-9 | Mã nguồn FE hiện tại | Quét mọi `path:` tuyệt đối trong `router.jsx` (giải `ROUTES.*`) | Không có path nào khai báo 2 lần | Unit | High | A1 | Pass |
| TC-PM-002 | GĐ1-9 | như trên | Đếm `/admin/posts/:postId` | Đúng 1 khai báo (`ADMIN_POST` và `ADMIN_POST_EDIT` không cùng được đăng ký) | Unit | High | A1 | Pass |
| TC-PM-003 | 0.4 | Đăng nhập Admin | Mở `/admin/rental-policy` | Tự chuyển (`Navigate replace`) sang `/admin/rental-policies`; router không còn import `AdminRentalPolicyPage` | Unit + Manual | High | A1 | Pass |
| TC-PM-004 | GĐ1-5 (FE) | Router | Đọc roles của nhánh `GuideLayout` | Có `GUIDE` và `TOUR_GUIDE`; không còn `STAFF` | Unit | High | A1 | Pass |
| TC-PM-005 | 0.1 | `enums.js` | Đếm `AWAITING_BANK_INFO` | Đúng 2 dòng (1 trong `REFUND_CASE_STATUS`, 1 trong bảng nhãn); nhãn "Chờ khách cung cấp tài khoản", không còn "Chờ khách nhập STK" | Unit | Med | A1 | Pass |
| TC-PM-006 | 0.3 | Mã nguồn | Tìm `refundProofHelper`/`extractProofs` trong `RefundProgressCard`, `RefundQueue`, `RefundDetailModal`, `RentalOrderDetailPage` | Không còn dùng helper base64 | Unit | High | A1 | Pass |
| TC-PM-007 | GĐ1-7 | Mã nguồn | Mở `AdminProductDetailPage.jsx` | Không còn chuỗi `/ops/warehouse/assets`; link dùng `ROUTES.OPS_ASSETS` (`/ops/assets`) | Unit | Med | A1 | Pass |
| TC-PM-008 | GĐ1-1, 1-3 | `endpoints.js` | Đọc endpoints | Có `issue-photos`, `publish`, `archive`, đường ảnh bài viết `/admin/posts/images` | Unit | Med | A1 | Pass |
| TC-PM-009 | GĐ1-4 | Mã nguồn | Đọc `AdminBundleDetailPage.jsx` | Có xử lý `replacementValue` | Unit | Med | A1 | Pass |
| TC-PM-100 | 0.6 | FE sạch | `npm run lint` | Không lỗi (đặc biệt 13 lỗi `eslint --quiet` cũ: import thừa, `eqeqeq`, setState trong effect) | Manual | High | | Not run |
| TC-PM-101 | 0.6 | như trên | `npx vitest run` | Toàn bộ test (cũ + `*.phase01.test.*`) pass | Manual | High | | Pass (3 flaky fail in full run, pass alone) |
| TC-PM-102 | 0.6 | như trên | `npm run build` | Build thành công; không cảnh báo thiếu import | Manual | High | | Not run |
| TC-PM-103 | 0.6 | Admin | Mở dashboard doanh thu (`RevenueChart`, file merge tay) | Biểu đồ render, không lỗi console, tooltip/legend đúng | Manual | Med | | Not run |
| TC-PM-104 | 0.4 | Mã nguồn | Tìm `ADMIN_RENTAL_POLICY` và `features/rental-ops/pages/AdminRentalPolicyPage.jsx` | Trang cũ bị xoá; không còn link nào trỏ `/admin/rental-policy` ngoài redirect | Manual | Med | | Not run |

## 2. Ảnh bằng chứng giao/nhận (Giai đoạn 0 mục 2)

| ID | Plan | Precondition | Steps | Expected | Type | Pri | Auto | Result |
|----|------|--------------|-------|----------|------|-----|------|--------|
| TC-PM-020 | 0.2 | Đơn Confirmed, 2 thiết bị `Reserved` | Staff mở `/ops/rentals/{code}/handover` | Mỗi thiết bị có đúng 1 ô tải ảnh (`EvidencePhotoCapture`, tổng 2 `input[type=file]`); không còn "Chụp / chọn ảnh" của `StaffRentalEvidencePanel` | UI | High | A5 | Pass |
| TC-PM-021 | 0.2 | như trên, `allowedActions.canReportNoShow = true` | Xem cuối trang | Còn nút "Báo khách không đến nhận" | UI | High | A5 | Pass |
| TC-PM-022 | 0.2 | như trên | Bấm nút no-show, nhập ghi chú, xác nhận | Gọi `reportNoShow`; đơn huỷ, mở hồ sơ hoàn phần còn lại (nếu đã thu tiền) | Manual | High | | Not run |
| TC-PM-023 | 0.2 | Thiết bị chưa đủ ảnh tối thiểu | Quét barcode thiết bị đó | Bị chặn với thông báo "Cần chụp ít nhất N ảnh ..." (kiểm tra min photo vẫn hoạt động) | UI | High | | Not run |
| TC-PM-024 | 0.2 | Đơn InFulfillment, thiết bị `Issued` | Staff mở `/ops/rentals/{code}/return` | Mỗi thiết bị đúng 1 ô tải ảnh (`EvidencePhotoCapture`); không còn ô "Chụp / chọn ảnh" và "Ảnh hư hỏng" của panel cũ | UI | High | | Not run |
| TC-PM-025 | 0.2 | như trên, `canSubmitReturnReport = true` | Xem cuối trang | Còn nút "Gửi báo cáo kiểm tra cho admin"; gửi được báo cáo | UI | High | | Not run |
| TC-PM-026 | 0.2 | như trên | Chụp ảnh qua `EvidencePhotoCapture`, tải ảnh lên | Ảnh gắn đúng `assignmentId`, `stage`; xoá ảnh hoạt động | Manual | Med | | Not run |

## 3. Chứng từ hoàn tiền (Giai đoạn 0 mục 3)

| ID | Plan | Precondition | Steps | Expected | Type | Pri | Auto | Result |
|----|------|--------------|-------|----------|------|-----|------|--------|
| TC-PM-070 | 0.3 | Hồ sơ hoàn Completed có `proofs[]` | Khách mở `RefundProgressCard` | Chứng từ hiện bằng `ProofGallery` từ `proofs[]`; không parse base64 từ `providerTransactionId` | UI | High | | Not run |
| TC-PM-071 | 0.3 | như trên | Admin mở `RefundQueue` và `RefundDetailModal` | Dùng `ProofGallery`; không còn `refundProofHelper` | UI | High | | Not run |
| TC-PM-072 | 0.3 | Hồ sơ Completed có `proofs[]` | Khách mở chi tiết đơn thuê (`RentalOrderDetailPage`) | Chứng từ hiện bằng `ProofGallery` | UI | Med | | Not run |
| TC-PM-073 | 0.3 | `providerTransactionId` là mã giao dịch thường | Mở `RefundProgressCard` | Hiện mã giao dịch dạng text bình thường, không lỗi | UI | Med | | Not run |
| TC-PM-074 | 0.3 | Hồ sơ không có `proofs[]` | Mở các màn trên | Không hiện gallery trống/vỡ ảnh | UI | Low | | Not run |

## 4. Giai đoạn 1: huỷ đơn chưa thanh toán (mục 1.1, GĐ1-2, GĐ1-10)

| ID | Plan | Precondition | Steps | Expected | Type | Pri | Auto | Result |
|----|------|--------------|-------|----------|------|-----|------|--------|
| TC-PM-010 | 1.1 FE tour | Booking chưa thanh toán (`isPaid = false`, `paidAmount = 0`) | Khách bấm "Hủy booking" | Hộp xác nhận đơn giản có câu "không phát sinh hoàn tiền" và "Chỗ giữ sẽ được nhả ngay"; nút "Huỷ đơn"; KHÔNG có: Đã thanh toán / Dự kiến hoàn / Khoản không hoàn, "Bạn không được hoàn tiền vì", link chính sách, ô ngân hàng, ô đồng ý, nút "Tiếp tục hủy" | UI | High | A2 | Pass |
| TC-PM-011 | 1.1, GĐ1-2 | như trên | Bấm "Huỷ đơn" | Payload không có `bankAccount`, `consentAccepted`, `consentVersion`; `reason` không chứa `data:image` | UI | High | A2 | Pass |
| TC-PM-012 | 1.1 | như trên | Nhập lý do (tuỳ chọn) rồi "Huỷ đơn" | `reason` là chuỗi chữ chứa nội dung đã nhập | UI | Med | A2 | Pass |
| TC-PM-013 | GĐ1-2 | Booking chưa thanh toán | Xem hộp thoại | Không có ô đính kèm ảnh (`input[type=file]`) | UI | High | A2 | Pass |
| TC-PM-014 | 1.1 | Booking đã thanh toán 2.000.000, hoàn 1.600.000 | Mở hộp thoại, bấm "Tiếp tục hủy" | Vẫn hiện "Dự kiến hoàn", số 1.600.000, link chính sách; sang bước nhập STK (Ngân hàng, Số tài khoản, Tên chủ TK); không có ô tải ảnh | UI | High | A2 | Pass |
| TC-PM-015 | 1.1, GĐ1-2 | như trên | Nhập STK, tick đồng ý, "Hủy booking" | Payload có `bankAccount`, `consentAccepted = true`, `expectedRefundAmount = 1600000`, `reason` không chứa base64 | UI | High | A2 | Pass |
| TC-PM-016 | 1.1 FE thuê đồ | Đơn thuê chưa thanh toán (Pending) | Khách mở `CancelUnpaidRentalPanel` | Có chữ "không phát sinh hoàn tiền" | UI | High | | Not run |
| TC-PM-017 | 1.1 FE thuê đồ | Đơn thuê chưa thanh toán | Mở trang chi tiết đơn | Không có khối "Hoàn tiền" và không có `RefundProgressCard` | UI | High | | Not run |
| TC-PM-018 | 1.1 FE admin | Admin, đơn chưa thanh toán | Mở `OrderDetailPage`, bấm huỷ đơn | Tiêu đề hộp thoại "Huỷ đơn"; không có cụm "& tạo yêu cầu hoàn tiền" và "khẩn cấp" kèm hoàn tiền | UI | High | | Not run |
| TC-PM-019 | 1.1 FE admin | Admin, đơn đã thanh toán | Bấm huỷ đơn | Tiêu đề "Huỷ đơn khẩn cấp ... & tạo yêu cầu hoàn tiền" giữ nguyên | UI | Med | | Not run |
| TC-PM-110 | 1.1 BE | Booking tour chưa thanh toán | `GET .../cancellation-preview` | Response có `isPaid = false` (`PaidAmount = 0`); đơn có thanh toán một phần/đủ thì `isPaid = true` | Unit | High | | Pass |
| TC-PM-111 | 1.1 BE | Booking tour chưa thanh toán | `CancelAsync` không có STK | Thành công; không có `RefundCase`, không có payment loại Refund, không yêu cầu STK/consent; chỗ giữ được nhả | Unit | High | | Pass |
| TC-PM-112 | 1.1 BE | Đơn thuê chưa thanh toán | `POST /rental-orders/{id}/cancel` | Huỷ thành công; không `RefundCase`, không payment Refund | Unit | High | | Not run |
| TC-PM-113 | 1.1 BE | Admin, đơn chưa có payment `Succeeded` | `AdminOrderService` huỷ đơn | Không tạo khoản hoàn (`AddFullRefundsAsync` không thêm gì); `RefundCase` rỗng | Unit | High | | Not run |
| TC-PM-114 | 1.1 BE | Đơn thuê đã thanh toán | `POST /cancel` (không phải cancel-paid) | Bị từ chối đúng quy tắc, phải đi `/cancel-paid` | Unit | Med | | Not run |
| TC-PM-115 | 1.1 | Booking đã thanh toán | Huỷ qua UI cũ | Hành vi hoàn tiền không đổi (hồ sơ hoàn tạo đúng số tiền theo chính sách) | Integration | High | | Not run |

## 5. Giai đoạn 1: các lỗi luồng còn lại

| ID | Plan | Precondition | Steps | Expected | Type | Pri | Auto | Result |
|----|------|--------------|-------|----------|------|-----|------|--------|
| TC-PM-030 | GĐ1-1 | Khách có đơn thuê, mở modal báo lỗi | Chọn 2 ảnh, nhập mô tả, "Gửi báo cáo sự cố" | `rentalService.uploadIssuePhoto` gọi 2 lần (mỗi ảnh một lần), đúng `orderId`, truyền `file` | UI | High | A3 | Pass |
| TC-PM-031 | GĐ1-1 | như trên | Gửi báo cáo kèm ảnh | Không gửi base64 `proofUrls`; không POST tới `/rental-orders/{id}/issues` | UI | High | A3 | Pass |
| TC-PM-032 | GĐ1-1 | như trên | Gửi báo cáo không ảnh | Không gọi endpoint ảnh | UI | Med | A3 | Pass |
| TC-PM-033 | GĐ1-1 | Trình duyệt thật | Gửi báo cáo kèm ảnh | Network: request multipart tới `/rental-orders/{id}/issue-photos`, trả 2xx, ảnh hiện cho admin | Manual | High | | Not run |
| TC-PM-040 | GĐ1-3 | Taxonomy có tag `tag-1` | Soạn bài mới, chọn tag, "Lưu nháp" | Payload có `tagIds = ['tag-1']`, không có `tagNames`, không có `status` | UI | High | A4 | Pass |
| TC-PM-041 | GĐ1-3 | Bài `Draft` có `postId` | Bấm "Xuất bản" | Gọi `/admin/posts/{id}/publish` | UI | High | A4 | Pass |
| TC-PM-042 | GĐ1-3 | Bài `Published` | Bấm nút "Lưu trữ" | Gọi `/admin/posts/{id}/archive` | UI | High | A4 | Pass |
| TC-PM-043 | GĐ1-3 | Soạn bài | Chèn ảnh vào nội dung | Upload qua `POST /admin/posts/images`, URL trả về được chèn vào nội dung | Manual | Med | | Not run |
| TC-PM-044 | GĐ1-4 | Admin mở `AdminBundleDetailPage` | Sửa combo, nhập "Giá trị thiết bị", lưu | Payload có `replacementValue`; mở lại thấy giá trị cũ; lưu không đổi giá trị thì không bị ghi null | UI | High | | Not run |
| TC-PM-050 | GĐ1-5 (BE) | Mã nguồn BE | Đọc `[Authorize]` của `GuideController` | Cho phép `Guide`, `TourGuide`, `Admin` | Unit | High | A1 | Pass |
| TC-PM-052 | GĐ1-5 | Đăng nhập TourGuide | Mở `/guide` và các trang con | Vào được, API `/guide/*` trả 200 (không 403) | Manual | High | | Not run |
| TC-PM-053 | GĐ1-5 | Đăng nhập Staff | Mở `/guide` | Bị chặn bởi route guard (chuyển hướng/403 UI) | Manual | Med | | Not run |
| TC-PM-051 | GĐ1-6 | Mã nguồn BE | Đọc dòng alert `OverdueRental` ở `AdminReportService` | Link là `/admin/rental-orders/{x.OrderId}` (GUID), không dùng `OrderCode` | Unit | High | A1 | Pass |
| TC-PM-054 | GĐ1-6 | Có đơn thuê quá hạn | Admin bấm link trong cảnh báo dashboard | Mở đúng trang chi tiết đơn, không 404 | Manual | High | | Not run |
| TC-PM-055 | GĐ1-7 | Admin | Mở chi tiết sản phẩm, bấm link tài sản | Điều hướng `/ops/assets`, không 404 | Manual | Med | | Not run |

## 6. Giai đoạn 1 mục 8: Staff không thấy nút chỉ dành cho Admin

| ID | Plan | Precondition | Steps | Expected | Type | Pri | Auto | Result |
|----|------|--------------|-------|----------|------|-----|------|--------|
| TC-PM-060 | GĐ1-8 | Đăng nhập Staff, đơn thuê đang thu hồi | Mở màn thu hồi | Không có nút "Hoàn tất thu hồi" (chỉ Admin) | UI | High | | Not run |
| TC-PM-061 | GĐ1-8 | Staff | Mở `SettlementPage` | Không có nút hoàn tất quyết toán và duyệt/đánh giá phí | UI | High | | Not run |
| TC-PM-062 | GĐ1-8 | Staff | Mở chi tiết chuyến | Không có nút huỷ chuyến | UI | High | | Not run |
| TC-PM-063 | GĐ1-8 | Staff | Mở Dashboard | Không có link tới `/admin/refunds` và `/admin/reports` | UI | High | | Not run |
| TC-PM-064 | GĐ1-8 | Admin | Lặp lại 060-063 | Các nút/link đều hiển thị và hoạt động | UI | High | | Not run |
| TC-PM-065 | GĐ1-8 | Staff gọi API bằng tay | Gọi các endpoint Admin-only | BE trả 403 (UI ẩn không thay thế phân quyền BE) | Manual | Med | | Not run |

## 7. Hồi quy tổng quát

| ID | Plan | Precondition | Steps | Expected | Type | Pri | Auto | Result |
|----|------|--------------|-------|----------|------|-----|------|--------|
| TC-PM-120 | Kiểm tra | BE | `dotnet test -c Release` | Toàn bộ pass, gồm test mới cho 1.1 và GuideController | Manual | High | | Pass |
| TC-PM-121 | Kiểm tra | FE | `npx vitest run src/routes/router.phase01.test.js` và 4 file `*.phase01.test.jsx` | Toàn bộ pass | Manual | High | A1-A5 | Pass |
| TC-PM-122 | Kiểm tra tay | Trình duyệt, 4 vai trò | Giao rồi nhận đồ có chụp ảnh; khách báo lỗi kèm ảnh; huỷ tour; xuất bản bài có tag; TourGuide vào `/guide` | Mọi luồng chạy trọn vẹn không lỗi console | Manual | High | | Not run |

## 8. Giả định về nhãn/selector (cho các test tự động)

- Tour huỷ chưa thanh toán: cờ `preview.isPaid`; nút trong hộp thoại có tên khớp `/^H(uỷ|ủy) đơn$/`; hộp xác nhận chứa "không phát sinh hoàn tiền" và "Chỗ giữ sẽ được nhả ngay"; ô lý do là `textbox` duy nhất trong dialog.
- Đơn đã thanh toán giữ nguyên nhãn cũ: "Tiếp tục hủy", "Ngân hàng", "Số tài khoản", "Tên chủ tài khoản", "Hủy booking".
- Modal báo lỗi thuê đồ gọi `rentalService.uploadIssuePhoto(orderId, { file })` mỗi ảnh một lần; `input[type=file]` duy nhất trong modal.
- Post editor: tag vẫn nhập qua nhãn "Tag" + Enter và được map sang `tagIds` bằng taxonomy; nút "Xuất bản", "Lưu nháp", "Lưu trữ"; test kiểm tra URL axios (`/admin/posts/{id}/publish`, `/archive`) nên không phụ thuộc tên hook/service.
- Handover: màn dùng `fulfillmentService.getHandoverChecklist` (trả `orderId`, `assets[]`, `minPhotos`) và `rentalOpsService.getById` cho `StaffRentalEvidencePanel`; nút no-show "Báo khách không đến nhận".
