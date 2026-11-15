# Test cases: nhập kết quả chuyển khoản Excel + chứng từ hoàn tiền

Phạm vi: plan `t-i-c-n-b-n-l-n-functional-charm`, RFP-10/RFP-11 (`FE/docs/03.2-API-CONTRACT-REFUND-PROOF-SYSTEM.md`), `BE/docs/05-MANUAL-REFUND-WORKFLOW.md`, `BE/docs/03-API-CONTRACT.md` §17.
Cột Result: `Not run` cho tới khi lead chạy. Auto = test tự động tương ứng.

## 1. Business rules

| BR | Quy tắc |
|----|---------|
| BR-01 | Admin xuất Excel (OTP) cho kế toán; hồ sơ Requested -> Approved khi xuất. |
| BR-02 | File xuất có thêm cột ẩn `RefundCaseId` (GUID đầy đủ) và 4 cột: Đã chuyển, Mã giao dịch, Lý do chưa chuyển, Hình ảnh minh chứng. |
| BR-03 | Reader tìm cột theo tên header (không phân biệt hoa/thường, dấu), không phụ thuộc vị trí. |
| BR-04 | Khớp hồ sơ theo GUID ẩn; không có thì dùng "Mã hồ sơ" và chỉ nhận khi khớp duy nhất một hồ sơ. |
| BR-05 | Ảnh hỗ trợ 2 kiểu: ảnh nổi neo trong dòng và Place in Cell (Excel 365). |
| BR-06 | Đã chuyển Có/Co/Yes/X/1 -> Complete; Không/Khong/No/0 -> Fail; trống hoặc khác -> Skip "Chưa điền trạng thái". Dòng trống hoàn toàn bị bỏ qua. |
| BR-07 | Complete: mã giao dịch bắt buộc (<=255), duy nhất trong file và DB; 1..3 ảnh, mỗi ảnh <=5MB, JPG/PNG/WebP theo magic bytes. |
| BR-08 | Fail: lý do bắt buộc, <=500 ký tự; hồ sơ -> Failed, RejectionReason = lý do; payment Failed. |
| BR-09 | Hồ sơ phải tồn tại, Approved và không phải hồ sơ của chính admin đang thao tác. Dòng sai: action Skip + errors. |
| BR-10 | dryRun=true (mặc định) không ghi gì (DB, storage, audit, notification). dryRun=false áp từng dòng độc lập; một dòng lỗi không chặn dòng khác. |
| BR-11 | Idempotent: upload lại file đã áp dụng -> các dòng đã Completed/Failed là Skip có lỗi, không tạo thêm proof. |
| BR-12 | Hoàn tất thủ công (`/complete`) là multipart: providerTransactionId bắt buộc, files 1..3 bắt buộc (400 key `files`). |
| BR-13 | `mark-manual` multipart: manualReference bắt buộc (<=100), note <=500, files 1..3 bắt buộc; proof gắn payment_id. |
| BR-14 | Ảnh lưu storage riêng tư, không có URL public; tên file do server sinh; ghi `refund_proofs` cùng transaction, lỗi transaction thì xóa file đã upload. |
| BR-15 | Xem ảnh: Admin (admin endpoint), khách sở hữu hồ sơ (customer endpoint, khác chủ -> 404); Staff/Customer trên endpoint admin -> 403; chưa đăng nhập -> 401; Cache-Control private,no-store. |
| BR-16 | Customer GET hồ sơ có `proofs[]` chỉ khi Completed, kèm `failureReason`; queue admin có `failureReason` + `proofs[]`. |
| BR-17 | Giữ nguyên lock, transaction, cập nhật payment/order, audit (thêm importBatchId, tên file, số dòng), thông báo khách. |

## 2. Test cases

### 2.1 Excel export template (EX)

| ID | Area | Precondition | Steps | Expected | Type | Pri | Result |
|----|------|--------------|-------|----------|------|-----|--------|
| TC-RF-001 | Export | Có hồ sơ Requested | Admin xuất Excel với OTP đúng | Sheet 1 có đủ 11 header cũ theo đúng thứ tự | Integration | High | Not run |
| TC-RF-002 | Export | như trên | Đọc header sau cột Ngày tạo | Có cột `RefundCaseId`, "Đã chuyển", "Mã giao dịch", "Lý do chưa chuyển", "Hình ảnh minh chứng" | Integration | High | Not run |
| TC-RF-003 | Export | như trên | Kiểm tra cột `RefundCaseId` | Cột ẩn; mỗi dòng chứa GUID đầy đủ khớp RefundCaseId | Integration | High | Not run |
| TC-RF-004 | Export | như trên | Kiểm tra ô "Đã chuyển" | Có Data Validation list Có/Không | Integration | Med | Not run |
| TC-RF-005 | Export | như trên | Kiểm tra sheet "Hướng dẫn" | Sheet tồn tại, có nội dung hướng dẫn | Integration | Low | Not run |
| TC-RF-006 | Export | như trên | Kiểm tra protect sheet | Cột dữ liệu gốc bị khóa; 4 cột mới sửa được và cho chèn ảnh | Manual | Med | Not run |
| TC-RF-007 | Export | Holder name `=HYPERLINK(...)` | Xuất | Ô vẫn là text, không là công thức (không hồi quy) | Unit | High | Not run |
| TC-RF-008 | Export | Hồ sơ có số tài khoản đầu 0 | Xuất | Số tài khoản giữ số 0 đầu (định dạng text) | Unit | Med | Not run |

### 2.2 Reader (RD) - auto: RefundResultWorkbookReaderTests

| ID | Area | Precondition | Steps | Expected | Type | Pri | Result |
|----|------|--------------|-------|----------|------|-----|--------|
| TC-RF-010 | Reader | xlsx hợp lệ 2 dòng | Read | Rows đúng RowNumber, RefundCaseId, code, TransferredRaw, Transferred, TransactionId, Reason; MissingHeaders rỗng | Unit | High | Pass |
| TC-RF-011 | Reader | Đã chuyển = Có/có/Co/YES/x/X/1 | Read | Transferred = true | Unit | High | Pass |
| TC-RF-012 | Reader | Đã chuyển = Không/KHÔNG/Khong/no/0 | Read | Transferred = false | Unit | High | Pass |
| TC-RF-013 | Reader | Đã chuyển = trống hoặc "Chờ"/"Maybe" | Read | Transferred = null, TransferredRaw giữ nguyên | Unit | High | Pass |
| TC-RF-014 | Reader | 4 cột kết quả đảo thứ tự | Read | Vẫn đọc đúng theo tên header | Unit | High | Pass |
| TC-RF-015 | Reader | Header viết HOA, không dấu | Read | Nhận diện đủ, không MissingHeaders | Unit | Med | Pass |
| TC-RF-016 | Reader | Thiếu từng header: Đã chuyển / Mã giao dịch / Lý do chưa chuyển | Read | MissingHeaders chứa tên header thiếu | Unit | High | Pass |
| TC-RF-017 | Reader | Có dòng trống giữa các dòng dữ liệu | Read | Dòng trống bị bỏ, RowNumber giữ đúng số dòng thật | Unit | Med | Pass |
| TC-RF-018 | Reader | Cột ẩn RefundCaseId có GUID | Read | RefundCaseId = GUID đầy đủ | Unit | High | Pass |
| TC-RF-019 | Reader | Không có cột RefundCaseId | Read | RefundCaseId null, RefundCode còn, không báo thiếu header | Unit | High | Pass |
| TC-RF-020 | Reader | GUID sai định dạng | Read | RefundCaseId null, không throw | Unit | Med | Pass |
| TC-RF-021 | Reader | Mã giao dịch "000123456" và ô số 987654321 | Read | Giữ số 0 đầu; số đọc thành chuỗi | Unit | Med | Pass |
| TC-RF-022 | Reader | Ảnh nổi ở 2 dòng khác nhau | Read | Mỗi dòng đúng ảnh của mình, ContentType image/png, có FileName | Unit | High | Pass |
| TC-RF-023 | Reader | 3 ảnh nổi trong cùng 1 dòng | Read | Images.Count = 3 | Unit | High | Pass |
| TC-RF-024 | Reader | 4 ảnh trong 1 dòng | Read | Trả đủ 4 (service chịu trách nhiệm giới hạn 3) | Unit | High | Pass |
| TC-RF-025 | Reader | Ảnh Place in Cell | Read | Trích đúng byte gốc, ContentType image/png | Unit | High | Pass |
| TC-RF-026 | Reader | Place in Cell ở 2 dòng | Read | Mỗi dòng đúng ảnh của mình | Unit | High | Pass |
| TC-RF-027 | Reader | Trộn ảnh nổi và Place in Cell | Read | Cả hai kiểu đều được nhận | Unit | High | Pass |
| TC-RF-028 | Reader | Place in Cell chứa byte không phải ảnh | Read | Không throw (kiểm magic bytes ở service) | Unit | Med | Pass |
| TC-RF-029 | Reader | Stream không phải zip | Read | Ném exception | Unit | High | Pass |
| TC-RF-030 | Reader | Chỉ có header | Read | Rows rỗng, MissingHeaders rỗng | Unit | Low | Pass |
| TC-RF-031 | Reader | File Excel 365 thật có ảnh Place in Cell | Mở bằng Excel, chèn ảnh, lưu, import | Ảnh được trích đúng (fixture tự dựng chưa chứng minh tương thích Excel thật) | Manual | High | Not run |

### 2.3 Import dry-run / apply (IM) - auto: RefundProofServiceTests

| ID | Area | Precondition | Steps | Expected | Type | Pri | Result |
|----|------|--------------|-------|----------|------|-----|--------|
| TC-RF-040 | Import | 3 hồ sơ Approved: Có+ảnh, Không+lý do, trống | dryRun=true | batchId null, totalRows 3, validRows 2, errorCount 1, actions Complete/Fail/Skip, imageCount đúng, applied=false | Unit | High | Pass |
| TC-RF-041 | Import | như trên | dryRun=true | Không đổi trạng thái, không proof, không file upload, không audit, không commit | Unit | High | Pass |
| TC-RF-042 | Import | Hồ sơ Approved, dòng Có + 2 ảnh | dryRun=false | Case Completed, ProviderTransactionId đã trim, payment Succeeded, 2 proof source ExcelImport, batchId có, audit chứa batchId | Unit | High | Pass |
| TC-RF-043 | Import | Dòng Không + lý do | dryRun=false | Case Failed, RejectionReason = lý do đã trim, payment Failed, không proof | Unit | High | Pass |
| TC-RF-044 | Import | 1 dòng hợp lệ + 1 thiếu ảnh + 1 Không hợp lệ | dryRun=false | Dòng hợp lệ áp dụng, dòng lỗi giữ Approved, report đúng đếm | Unit | High | Pass |
| TC-RF-045 | Import | Đã áp dụng file một lần | Upload lại cùng file | CompletedCount 0, ErrorCount 1, action Skip có lỗi, không thêm proof | Unit | High | Pass |
| TC-RF-046 | Import | Đã chuyển trống | dryRun | Skip, errors chứa "Chưa điền trạng thái" | Unit | High | Pass |
| TC-RF-047 | Import | Đã chuyển = "Chờ xử lý" | dryRun | Skip, ErrorCount 1 | Unit | Med | Pass |
| TC-RF-048 | Import | Có/co/Yes/X/1 | dryRun | Action Complete | Unit | Med | Pass |
| TC-RF-049 | Import | Không/khong/No/0 | dryRun | Action Fail | Unit | Med | Pass |
| TC-RF-050 | Import | Có nhưng thiếu mã giao dịch | dryRun | Skip + lỗi | Unit | High | Pass |
| TC-RF-051 | Import | Có nhưng không có ảnh | dryRun | Skip + lỗi | Unit | High | Pass |
| TC-RF-052 | Import | Có nhưng 4 ảnh | dryRun | Skip + lỗi | Unit | High | Pass |
| TC-RF-053 | Import | Có nhưng 1 ảnh >5MB | dryRun | Skip + lỗi | Unit | High | Pass |
| TC-RF-054 | Import | Có nhưng ảnh không phải image (magic bytes) | dryRun | Skip + lỗi | Unit | High | Pass |
| TC-RF-055 | Import | Ảnh Place in Cell hợp lệ | dryRun=false | Hoàn tất, 1 proof | Unit | High | Pass |
| TC-RF-056 | Import | Không nhưng thiếu lý do | dryRun | Skip + lỗi | Unit | High | Pass |
| TC-RF-057 | Import | Lý do 501 ký tự | dryRun | Skip + lỗi | Unit | Med | Pass |
| TC-RF-058 | Import | Lý do đúng 500 ký tự | dryRun | Action Fail, không lỗi | Unit | Med | Pass |
| TC-RF-059 | Import | 2 dòng cùng mã giao dịch trong file | dryRun=false | Có ít nhất 1 dòng lỗi, tối đa 1 hồ sơ Completed | Unit | High | Pass |
| TC-RF-060 | Import | Mã giao dịch đã dùng cho hồ sơ Completed khác | dryRun=false | Dòng lỗi, hồ sơ giữ Approved, không proof | Unit | High | Pass |
| TC-RF-061 | Import | Hồ sơ Requested / Completed / Failed | dryRun=false | Skip + lỗi, trạng thái không đổi | Unit | High | Pass |
| TC-RF-062 | Import | Hồ sơ của chính admin | dryRun=false | Lỗi, không đổi trạng thái | Unit | High | Pass |
| TC-RF-063 | Import | GUID không tồn tại | dryRun | Skip + lỗi | Unit | High | Pass |
| TC-RF-064 | Import | Dòng trống hoàn toàn | dryRun | Không tính vào totalRows | Unit | Med | Pass |
| TC-RF-065 | Import | File .txt / không phải xlsx | POST import | 400 | Unit/API | High | Pass |
| TC-RF-066 | Import | Thiếu header bắt buộc | POST import | 400 | Unit | High | Pass |
| TC-RF-067 | Import | Chỉ có "Mã hồ sơ" (không GUID), khớp duy nhất | dryRun | Dòng hợp lệ | Integration | High | Not run |
| TC-RF-068 | Import | "Mã hồ sơ" 8 ký tự trùng 2 hồ sơ, không GUID | dryRun | Skip + lỗi "không xác định duy nhất" | Integration | High | Not run |
| TC-RF-069 | Import | File >20MB | POST import | 400/413 | API | Med | Not run |
| TC-RF-070 | Import | Không gửi file | POST import | 400 | API | Med | Skipped (Postgres gated, Docker off) |
| TC-RF-071 | Import | Order NeedsReconciliation | dryRun=false | Dòng Có lỗi giống CompleteAsync (ORDER_NOT_RECONCILABLE) | Integration | Med | Not run |
| TC-RF-072 | Import | Hoàn một phần vs toàn bộ | dryRun=false | Order PartiallyRefunded / Refunded đúng | Integration | Med | Not run |
| TC-RF-073 | Import | Áp dụng thành công | Sau apply | Khách nhận notification RefundResult | Integration | Med | Not run |
| TC-RF-074 | Import | Lưu DB lỗi giữa chừng một dòng | dryRun=false | File đã upload của dòng đó bị xóa, dòng khác vẫn chạy | Integration | High | Not run |
| TC-RF-075 | Import | 2 admin import cùng file song song | Chạy đồng thời | Mỗi hồ sơ chỉ Completed một lần, không proof trùng | Integration | Med | Not run |

### 2.4 Hoàn tất thủ công (CM) - auto: RefundProofServiceTests

| ID | Area | Precondition | Steps | Expected | Type | Pri | Result |
|----|------|--------------|-------|----------|------|-----|--------|
| TC-RF-080 | Complete | Approved | Complete với 2 ảnh hợp lệ | Completed, 2 proof (source Manual, UploadedBy = admin), 2 file lưu | Unit | High | Pass |
| TC-RF-081 | Complete | Approved | Complete không file | 400 key `files`, không đổi trạng thái | Unit/API | High | Pass |
| TC-RF-082 | Complete | Approved | 4 file | 400 | Unit | High | Pass |
| TC-RF-083 | Complete | Approved | File .png giả (text) | 400 do magic bytes | Unit | High | Pass |
| TC-RF-084 | Complete | Approved | File >5MB | 400 | Unit | High | Pass |
| TC-RF-085 | Complete | Mã giao dịch đã dùng | Complete | 409 REFUND_TRANSACTION_DUPLICATED, không để file mồ côi | Unit | High | Pass |
| TC-RF-086 | Complete | SaveChanges lỗi | Complete | Exception, file đã upload bị xóa | Unit | High | Pass |
| TC-RF-087 | Complete | Approved | Thiếu providerTransactionId | 400 | API | High | Not run |
| TC-RF-088 | Complete | rowVersion cũ | Complete | 409 CONCURRENCY_CONFLICT | Unit | Med | Pass |

### 2.5 Mark-manual legacy (MM)

| ID | Area | Precondition | Steps | Expected | Type | Pri | Result |
|----|------|--------------|-------|----------|------|-----|--------|
| TC-RF-090 | MarkManual | Payment hoàn tiền chờ | mark-manual có ref + 1 ảnh | 200, payment Succeeded, proof gắn payment_id (source Manual) | Integration | High | Not run |
| TC-RF-091 | MarkManual | như trên | Không file | 400 key `files` | API | High | Not run |
| TC-RF-092 | MarkManual | như trên | Thiếu manualReference | 400 | API | High | Not run |
| TC-RF-093 | MarkManual | như trên | manualReference 101 ký tự / note 501 ký tự | 400 | API | Med | Not run |
| TC-RF-094 | MarkManual | như trên | 4 ảnh hoặc ảnh sai định dạng | 400 | API | Med | Not run |
| TC-RF-095 | MarkManual | Payment đã Succeeded | mark-manual lại | 409, không thêm proof | Integration | Med | Not run |
| TC-RF-096 | MarkManual | Có payment đã có proof | GET /admin/refunds | Items có manualReference + proofs[] | API | Med | Not run |

### 2.6 Proof access control (AC) - auto: RefundProofHttpTests

| ID | Area | Precondition | Steps | Expected | Type | Pri | Result |
|----|------|--------------|-------|----------|------|-----|--------|
| TC-RF-100 | Access | Case Completed có proof | Admin GET /admin/refund-cases/{id}/proofs/{pid} | 200, đúng content-type, Cache-Control private,no-store | API | High | Not run |
| TC-RF-101 | Access | như trên | Khách sở hữu GET /refund-cases/{id}/proofs/{pid} | 200 | API | High | Not run |
| TC-RF-102 | Access | như trên | Khách khác GET customer endpoint | 404 | API | High | Skipped (Postgres gated, Docker off) |
| TC-RF-103 | Access | như trên | Anonymous GET customer endpoint | 401 | API | High | Skipped (Postgres gated, Docker off) |
| TC-RF-104 | Access | như trên | Staff GET admin proof | 403 | API | High | Skipped (Postgres gated, Docker off) |
| TC-RF-105 | Access | như trên | Customer GET admin proof | 403 | API | High | Skipped (Postgres gated, Docker off) |
| TC-RF-106 | Access | như trên | Anonymous GET admin proof | 401 | API | High | Skipped (Postgres gated, Docker off) |
| TC-RF-107 | Access | Proof của case A | GET /refund-cases/B/proofs/{proofA} | 404 (không lẫn hồ sơ) | API | High | Not run |
| TC-RF-108 | Access | Proof không tồn tại | Admin GET | 404 | API | Med | Not run |
| TC-RF-109 | Access | Staff/Customer/Anonymous | POST import-result, POST complete, mark-manual | 403/401 | API | High | Skipped (Postgres gated, Docker off) |
| TC-RF-110 | Access | Payment proof | Admin GET /admin/refunds/{paymentId}/proofs/{pid} | 200; Staff/Customer 403 | API | High | Not run |
| TC-RF-111 | Access | Khách, hồ sơ chưa Completed | GET /refund-cases/{id} | proofs[] rỗng/không có; failureReason có khi Failed | API | High | Not run |
| TC-RF-112 | Access | File storage | Truy cập đường dẫn vật lý/URL storage trực tiếp | Không truy cập được (không public) | Manual | High | Not run |
| TC-RF-113 | Access | Route table | Reflection | Route import-result, proofs, multipart đúng; AdminRouteCount cập nhật | Unit | Med | Pass |

### 2.7 Customer UI (CU)

| ID | Area | Precondition | Steps | Expected | Type | Pri | Result |
|----|------|--------------|-------|----------|------|-----|--------|
| TC-RF-120 | Customer UI | Booking tour có hồ sơ Completed + proof | Mở trang booking | RefundProgressCard có link "Xem biên lai chuyển khoản", ảnh hiển thị | UI | High | Not run |
| TC-RF-121 | Customer UI | Đơn thuê có refundCase | Mở RentalOrderDetailPage | RefundProgressCard hiển thị | UI | High | Not run |
| TC-RF-122 | Customer UI | Hồ sơ Failed | Mở card | Hiện lý do thất bại, không biên lai | UI | Med | Not run |
| TC-RF-123 | Customer UI | Hồ sơ Approved | Mở card | Không có biên lai | UI | Med | Not run |
| TC-RF-124 | Customer UI | Đã đăng xuất | Mở URL ảnh | Bị chặn (401) | Manual | High | Not run |
| TC-RF-125 | Customer UI | Ảnh lỗi tải | Mở card | Hiện "Lỗi tải ảnh" không vỡ trang | UI | Low | Not run |

### 2.8 FE validation and dialogs (FE) - auto: vitest

| ID | Area | Precondition | Steps | Expected | Type | Pri | Result |
|----|------|--------------|-------|----------|------|-----|--------|
| TC-RF-130 | FE util | - | validateProofFiles([]) | Lỗi "Vui lòng đính kèm ít nhất 1 ảnh chứng từ"; required=false thì hợp lệ | Unit | High | Pass |
| TC-RF-131 | FE util | - | 3 file ok, 4 file | 3 hợp lệ, 4 lỗi | Unit | High | Pass |
| TC-RF-132 | FE util | - | pdf/gif/text | Lỗi nêu tên file | Unit | High | Pass |
| TC-RF-133 | FE util | - | 5MB và 5MB+1 | Đúng hợp lệ, hơn lỗi | Unit | High | Pass |
| TC-RF-134 | ImportDialog | - | Chọn .xlsx | Gọi importResult(file,true), hiện bảng preview và badge | UI | High | Pass |
| TC-RF-135 | ImportDialog | Preview xong | Bấm "Áp dụng N dòng hợp lệ" | Gọi importResult(file,false), hiện "Đã hoàn tất X · Thất bại Y · Lỗi Z", invalidate queue | UI | High | Pass |
| TC-RF-136 | ImportDialog | validRows=0 | Xem nút | Nút Áp dụng disabled | UI | Med | Pass |
| TC-RF-137 | ImportDialog | - | Chọn .csv | Báo lỗi .xlsx, không gọi API | UI | Med | Pass |
| TC-RF-138 | ImportDialog | API lỗi | Preview/Apply lỗi | Hiện alert, giữ dialog | UI | Med | Pass |
| TC-RF-139 | CompleteDialog | - | Nhãn | "Mã giao dịch ngân hàng *", không còn "không bắt buộc" | UI | High | Pass |
| TC-RF-140 | CompleteDialog | - | Submit thiếu mã GD / thiếu ảnh | Chặn, không gọi API | UI | High | Pass |
| TC-RF-141 | CompleteDialog | Hợp lệ | Submit | complete({refundCaseId, formData}) với providerTransactionId trim, expectedRowVersion, files | UI | High | Pass |
| TC-RF-142 | CompleteDialog | - | 4 ảnh / >5MB / pdf | Chặn kèm lỗi | UI | High | Pass |
| TC-RF-143 | CompleteDialog | - | Xóa ảnh bằng "Xóa ảnh {name}" | Ảnh bị gỡ | UI | Low | Pass |
| TC-RF-144 | ManualRefundDialog | - | Thiếu mã tham chiếu / thiếu ảnh | Chặn, không gọi API | UI | High | Pass |
| TC-RF-145 | ManualRefundDialog | Hợp lệ | Submit | markManual({paymentId, formData}); note bỏ qua khi rỗng | UI | High | Pass |
| TC-RF-146 | ProofGallery | - | proofs rỗng | "Chưa có chứng từ" | UI | Med | Pass |
| TC-RF-147 | ProofGallery | proofs có | Render | fetchProofBlob(path) cho từng ảnh, img alt=fileName, click phóng to, revoke URL khi unmount | UI | High | Pass |
| TC-RF-148 | RefundQueue | Hồ sơ Completed/Failed | Xem hàng đợi | Cột chứng từ + mã GD (Completed), lý do (Failed) | UI | Med | Not run |
| TC-RF-149 | AdminRefundsPage | - | Tab thủ công | Danh sách payment chờ, mở ManualRefundDialog | UI | Med | Not run |

### 2.9 Regression (RG)

| ID | Area | Precondition | Steps | Expected | Type | Pri | Result |
|----|------|--------------|-------|----------|------|-----|--------|
| TC-RF-160 | Regression | Hồ sơ Requested | Export (RefundCaseServiceTests) | Vẫn xuất được; Requested -> Approved; audit RefundExported không chứa số tài khoản | Unit | High | Pass |
| TC-RF-161 | Regression | - | Export thiếu OTP/sai OTP/hết hạn | Hành vi cũ giữ nguyên | Unit | High | Pass |
| TC-RF-162 | Regression | Hồ sơ Approved | Re-export | Không đổi trạng thái, rowVersion giữ nguyên | Unit | Med | Pass |
| TC-RF-163 | Regression | Hồ sơ Requested | Complete | REFUND_CASE_NOT_COMPLETABLE (không hoàn tất trước khi xuất) | Unit | High | Pass |
| TC-RF-164 | Regression | Hồ sơ của admin | Complete / mark-failed | 403 | Unit | High | Pass |
| TC-RF-165 | Regression | Schema | SchemaSynchronizationTests + EF model | Bảng `refund_proofs`, CHECK một-trong-hai, index | Integration | High | Not run |
| TC-RF-166 | Regression | - | mark-failed thủ công (JSON) | Vẫn hoạt động, có failureReason | API | Med | Not run |
