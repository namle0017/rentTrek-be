# Unified SePay Collection and Manual Refund Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement task-by-task. Steps use checkbox syntax. Không tự mở subagent; hai subagent trước đó chỉ được giao review.

**Goal:** Hợp nhất thu tiền an toàn và hoàn tiền theo policy, do staff duyệt và chuyển khoản thủ công.

**Architecture:** Nền `feature/sepay-payment` (B), port có chọn lọc collection/webhook/expiry/audit và calculator của `feature/payment-sepay` (A). Khách gửi yêu cầu có tài khoản nhận; staff approve/reject, chuyển tiền ngoài hệ thống rồi xác nhận Done. Không gọi API refund hoặc dùng worker chi tiền.

**Tech Stack:** .NET 8, ASP.NET Core, EF Core/Npgsql, PostgreSQL, xUnit, Testcontainers.PostgreSql từ A.

**Spec:** Baseline bên dưới và quyết định người dùng ngày 01/11/2026: khách nhập tài khoản để yêu cầu hoàn, staff xem report và approve/reject/done, hoàn theo refund policies hệ thống, không chuyển tự động. Các chi tiết chưa được chốt được đánh dấu đề xuất.

## Global Constraints

- Base B: `afed26bb5f14b717c0abc2f1fa5382909af26496`; donor A: `b26851d9e95d5d73fe197ea56d363f8bf376b9e5`. Xác minh refs trước triển khai.
- Người dùng đã cho phép implementation. Worktree riêng từ B, branch `codex/payment-unified`; giữ file người dùng và không tự push/merge/deploy.
- Giữ auth/revocation, DTO layout, booking/rental/promotion và invitation của B; không merge nguyên A hoặc mang `.ua`/secret.
- Không port `IRefundExecutor`, `RefundExecutor`, `RefundClaim`, `SyncRefundStatusService`, provider refund methods hoặc auto-refund retry vào runtime mới. Refund cũ đang chờ provider phải đối soát trước chuyển manual.
- Mọi mutation payment/order/refund lifecycle dùng cùng `orders FOR UPDATE`; HTTP thu tiền bên ngoài transaction. Dùng `TimeProvider`, timestamp mới `DateTimeOffset` UTC, VND nguyên `numeric(18,0)`.
- Staff thực hiện approve/reject/done; không chỉ gán cho Admin. Customer chỉ xem/thao tác request của mình. Quyền Admin nếu cần phải theo cấu hình dự án, không mặc định từ tên role.
- Bank details chỉ hiện đầy đủ cho owner và staff có quyền xử lý, list che bớt số; không log raw account/secret. Dùng cơ chế bảo vệ dữ liệu hiện có, không tự thiết kế mã hóa.
- Migration trong `database/migrations/`, đồng bộ `docs/database/supabase_schema.sql`; không chạy nguyên SQL A trên schema B. Test chỉ DB/provider giả, không chuyển tiền thật.

## Review Focus

1. Webhook trùng/cross-order chỉ ghi một khoản thu; account sai không Paid (Task 1,4–5).
2. Webhook/expiry/cancel cạnh tranh không vừa Confirmed vừa trả tài nguyên (Task 5).
3. Policy 50% đơn 1.000.000 chỉ cho tổng hoàn 500.000, không tính lại 50% trên remaining sau mỗi request (Task 6).
4. Hai staff cùng approve/done hoặc Done timeout: ghi sổ một lần, không hướng dẫn chuyển lại (Task 7).
5. Policy/account thay sau approve và dữ liệu thu legacy chưa xác minh không được thay ngầm số tiền/đích nhận/bằng chứng (Task 1,6–7,9).

## Design baseline

### Quyết định và phạm vi

| Chủ đề | Quy tắc | Trạng thái |
|---|---|---|
| Refund | Khách gửi bank details, staff duyệt và chuyển tay rồi Done | Người dùng đã chốt |
| Policy | Hệ thống xác định điều kiện/hạn mức, staff không approve vượt policy | Người dùng đã chốt |
| Request states | PendingReview → Approved → Done; PendingReview → Rejected | Đề xuất chi tiết |
| Bank details | BankCode, AccountNumber dạng chuỗi, AccountHolderName | Dữ liệu bắt buộc; tên trường là đề xuất |
| Chuyển thiếu | Cộng các khoản verified trong hold đến khi đủ; không expire ngay | Đề xuất chưa chốt |
| Deadline | Chỉ confirm khi now < HoldExpiresAt và đơn còn hợp lệ; null không là vô hạn | Đề xuất theo nền B |
| Tiền muộn/dư | Lưu ledger, vào review; hoàn qua request/policy/staff | Theo hướng manual refund |
| Thu tiền | Giữ QR cố định B cho đợt này; VA chỉ mở rộng khi có nhu cầu/capability | Đề xuất giảm phạm vi, độc lập quyết định refund |
| Tiền cọc | Giữ nghiệp vụ B, không mang depositAmount=0 của A đè lên | Giới hạn phạm vi |

Manual refund không cần ngân hàng hỗ trợ API refund SePay. Thông tin capability chỉ còn cần nếu chọn VA cho chiều thu; không triển khai cả hai engine. Nếu người dùng vẫn chọn VA, bổ sung create/lookup/cancel vào kế hoạch thu tiền trước implementation, không thêm provider refund.

### Trạng thái và ý nghĩa

- PendingReview: khách đã submit, chờ staff; giữ reservation của hạn mức hoàn.
- Approved: staff chấp thuận và được assign xử lý; chưa ghi Refund Succeeded/đã hoàn.
- Rejected: bắt buộc lý do, nhả reservation và giữ lịch sử; khách có thể gửi mới sau khi kiểm tra lại policy.
- Done: chỉ từ Approved, assigned staff xác nhận đã chuyển thành công, nhập TransferReference và TransferredAt; EvidenceReference có thể bổ sung. Ghi Refund Succeeded đúng một lần. Đây là xác nhận staff, hệ thống không tự xác minh ngân hàng đã chuyển.
- Đề xuất khóa bank details/amount từ lúc submit; không cho sửa sau approve. Reject chỉ từ PendingReview. Muốn undo approval/reassign phải bổ sung action có audit riêng trước khi mở rộng, không tự reset state.
- Done replay cùng payload trả kết quả cũ, payload khác trả 409; approve replay không đổi assigned staff. Không có action chuyển khoản tự động.
- Submit request không tự hủy order. Cancellation refund dùng trạng thái/mốc cancellation phù hợp policy; không âm thầm hủy booking/rental khi khách mới gửi report.

### Policy và budget

- Dùng policy/version/rules gắn với order và mốc nghiệp vụ đúng policy: cancellation time nếu rule gắn hủy, otherwise thời điểm đánh giá request. Snapshot policy/rule, thời gian, cơ sở tính, tỷ lệ, entitlement và request amount tại submit.
- Entitlement = floor(eligiblePolicyBase × percentage / 100). Available = max(0, min(entitlement - done - activeReserved, verifiedCollected - done - activeReserved)). Base chỉ gồm phần dịch vụ/tiền thuộc phạm vi policy, không tự coi cọc/dư đều cùng một rule.
- Collected/base 1.000.000, rule 50%: entitlement 500.000; đã Done 500.000 thì available=0. Không tái áp phần trăm lên số còn lại để hoàn thêm 250.000; context khác cũng không được bỏ qua tiền đã hoàn.
- PendingReview/Approved cùng giữ budget. Khi approve, loại reservation của chính request khi kiểm tra lại, không trừ hai lần; snapshot hợp lệ không bị thay ngầm theo policy mới. Staff không nhập số tiền vượt policy; amount quyết định luôn phía server.
- Tiền dư, thiếu, đến muộn và sự cố dịch vụ cần reason/rule được hỗ trợ. Thiếu rule trả không đủ điều kiện và vào đối soát, không ngầm hoàn 100% hoặc thêm exception bỏ policy.
- Dữ liệu cũ chỉ có aggregate Paid chưa đủ bằng chứng phải đối soát trước hoàn; bảo toàn lịch sử, không bịa transaction ID. Policy changes phải version/snapshot, không xóa rule đang được tham chiếu.

### Mô hình dữ liệu

- `payments`: giữ ID/Collection/Refund; Collection là intent, không phải một lần chuyển. Transfer code unique có index. Refund payment chỉ Succeeded khi Done; không coi Approved là tiền đã hoàn.
- `payment_webhook_receipts`: provider event ID unique, payload hash, trạng thái Received/Processed/Unmatched/Rejected/Retry và retry metadata; không auth secret. Cùng ID khác payload vào review.
- `bank_transactions`: provider transaction unique, bank/account/subaccount, amount/direction/reference, timestamps, verification, payment ID nullable, AppliedAt và số tiền phân bổ/phần dư; retry không cộng lại.
- `refund_requests`: OrderId/CustomerUserId, bank snapshot, reason/note, Amount, Status, policy snapshot; SubmittedAt, ReviewedBy/At, AssignedStaffId, RejectReason; CompletedBy/At, TransferReference, TransferredAt, EvidenceReference nullable, RefundPaymentId nullable.
- `refund_allocations`: request ID, nguồn bank transaction và amount để giữ budget. Một request có thể lấy từ nhiều nguồn và staff chuyển một khoản tổng; không phải chia lệnh provider theo giao dịch gốc.
- `payment_idempotency_keys`: customer/key unique và hash request cho create payment. Refund submit có key/hash riêng; body khác cùng key trả 409. Không xóa key khi hết hold làm request cũ tạo intent mới.
- Giữ Paid/history legacy; không tự biến legacy Pending/unknown refund thành Done. Snapshot ngân hàng không tự thay khi khách sửa hồ sơ.

### Màn hình và API

Khách: xem điều kiện/số tiền theo policy → nhập ngân hàng, số tài khoản, tên chủ, lý do → gửi → theo dõi PendingReview/Approved/Rejected/Done và lý do từ chối. Staff: queue/list/detail/policy → approve/reject → xem bank snapshot đã duyệt → chuyển khoản bên ngoài → nhập reference/time → Done. Repo BE cung cấp contract; màn FE cần triển khai ở repo FE, không coi API là đã có màn hình.

Giữ route create/poll/webhook B. Customer routes prefix `/api/v1`: `GET /orders/{orderId}/refund-eligibility?reasonCode=...`, `POST /orders/{orderId}/refund-requests`, `GET /orders/{orderId}/refund-requests`, `GET /refund-requests/{id}` (owner only). Staff routes `/api/v1/staff/refund-requests`, detail và `/{id}/approve|reject|done`. List page=1/pageSize=20, tối đa 100; account đầy đủ chỉ ở authorized detail. DTO `DTOs/Refund/Requests|Responses|Internal/`, actor từ current user, không nhận acting staff ID từ body.

## Chuẩn bị triển khai

- [x] Đọc baseline mới, tạo worktree từ B bằng skill using-git-worktrees và chạy baseline test. Chốt đề xuất chuyển thiếu/deadline/QR trước implementation.
- [x] Liệt kê donor theo task; không port provider refund/claim/sync rồi chỉ tắt bằng flag. Hai khoảng nghỉ 30 phút chỉ áp dụng review đã xong.
- [x] Mỗi task red/green test, review diff và commit riêng trong worktree; không stage file người dùng, không tự push/merge/deploy.

## Task 1: Schema ledger và manual refund request

**Files:** `src/RenTrek.DAL/Entities/Payment.cs`, `Data/AppDbContext.cs`, configurations/repos/UoW; tạo entity/config/repo PaymentWebhookReceipt, BankTransaction, RefundRequest, RefundAllocation, PaymentIdempotencyKey; `database/migrations/20261101_payment_ledger_manual_refunds.sql`, canonical schema; port integration infrastructure A.
**Interfaces:** `IOrderRepository.LockAsync(Guid orderId, CancellationToken ct) -> Task<Order?>`; unique insert receipt/transaction, request locking và sum budget theo PendingReview/Approved/Done.

- [x] Viết PaymentLedgerSchemaTests/RefundRequestSchemaTests: unique ID, FK/positive amount, snapshot, Pending legacy không bị xóa, Paid legacy không tự sinh nguồn thu, refund history được giữ.
- [x] Chạy `dotnet test tests/RenTrek.IntegrationTests --filter FullyQualifiedName~Schema`; mới phải fail vì thiếu schema/invariant.
- [x] Migration additive/backfill mã legacy, phát hiện collision trước unique index; không mark Done nếu thiếu bằng chứng. Schema từng chạy A cần dry-run mapping riêng, dừng rõ khi không tương thích.
- [x] Chạy lại fixture rỗng và B có dữ liệu: pass, không mất ID/history. Commit `feat(payments): add ledger and manual refund schema`.

## Task 2: Adapter thu tiền và cấu hình

**Files:** `Options/SePaySettings.cs`, DI, `.env.example`, `RenTrek.API/Options/SePaySettingsValidator.cs`, webhook verifier; không port refund provider API.
**Interfaces:** `ISePayWebhookVerifier.Verify(string rawBody, string? authorization, string? signature, string? timestamp) -> bool`. QR B không cần CreateOrder/CancelOrder/GetRefund; VA là extension ngoài mặc định.

- [x] Viết SePayStartupValidationTests/SePayWebhookVerifierTests: thiếu account/auth bị chặn, đúng một auth mode, sai auth không fallback; không đòi capability refund.
- [x] Chạy unit/API filter theo hai lớp trên, thấy fail trước sửa.
- [x] Validate bank/account/subaccount; giữ QR B, HMAC hoặc API key đúng cấu hình webhook thực; mã có token boundary/unique index và tương thích mã RTP legacy.
- [x] Test pass không network, DI không đăng ký executor/worker chi tiền. Commit `fix(payments): validate collection configuration`.

## Task 3: Create/poll và idempotency

**Files:** port/adapt `Payments/InitiatePaymentService.cs`, `GetPaymentStatusService.cs`, interfaces và DTO Payment, đúng một controller và DI; current user của B.
**Interfaces:** `InitiateAsync(Guid orderId, string idempotencyKey, CancellationToken ct) -> Task<InitiatePaymentResult>` gồm response/Created. Poll có received/remaining/reconciliation và refund request summary, Approved không là Refunded.

- [x] Viết InitiatePaymentIdempotencyTests: cùng key/cùng payment, body khác 409, nhiều key một active intent, owner khác 404, hold null/hết hạn không tạo; poll phân biệt Approved/Done.
- [x] Chạy `dotnet test tests/RenTrek.IntegrationTests --filter FullyQualifiedName~InitiatePayment`; thấy fail trước sửa.
- [x] Persist key/hash/result trong transaction, amount từ order; QR không thêm unknown-create-VA state không dùng tới. Header mới phải đồng bộ FE.
- [x] Test 201 tạo/200 reuse/404 owner/409 conflict pass. Commit `feat(payments): persist create idempotency and polling`.

## Task 4: Durable webhook và xác minh

**Files:** Payments/SePayWebhookService, verifier, controller; thêm IPaymentWebhookInbox/PaymentWebhookInbox, IVerifiedBankTransactionService/VerifiedBankTransactionService và internal DTO.
**Interfaces:** `ReceiveAsync(string rawBody, string? authorization, string? signature, string? timestamp, CancellationToken ct) -> Task<ReceiptReceiveResult>` (Accepted/Duplicate/Rejected/Unavailable, ReceiptId?, ErrorCode?); `VerifyAsync(Guid receiptId, CancellationToken ct) -> Task<VerificationResult>` (Verified/Unmatched/Rejected/Retry, BankTransactionId?, ErrorCode?).

- [x] Viết WebhookInboxTests: không mã vẫn có review, sai account không Paid, ID<=0 reject, cùng ID khác body không ghi đè, DB fail không ACK, cross-order duplicate chỉ một transaction.
- [x] Chạy unit verifier và integration WebhookInboxTests, thấy fail trước sửa.
- [x] Auth trước ghi; ACK sau durable commit. Validate inbound/account/bank/subaccount/amount/reference; mã mơ hồ vào review. Không log raw bank details. Chưa bật consumer áp order đến Task 5.
- [x] Tests pass, receipt Retry/Received replay sau restart được. Commit `feat(payments): persist verified collection receipts`.

## Task 5: Áp tiền, hold, expiry và cancel

**Files:** Payments/PaymentResultApplier, interface, PaymentJobs/ExpireHeldOrdersService, Orders/CancelOrderService/OrderResourceReleaser, assignment/promotion repo; giữ inventory locks B.
**Interfaces:** `ApplyVerifiedTransactionAsync(Guid bankTransactionId, CancellationToken ct) -> Task<PaymentApplyResult>`; `IReceiptProcessor.ProcessAsync(Guid receiptId, CancellationToken ct) -> Task` verify rồi apply.

- [x] Viết PaymentApplicationTests: 100.000+300.000 cho đơn 400.000, đảo webhook cùng kết quả, dư được queue, duplicate không cộng; PaymentExpiryRaceTests: deadline/muộn không confirm, race cancel/expiry không vừa trả tài nguyên vừa Confirmed, release một lần.
- [x] Chạy PostgreSQL Testcontainers test để thấy fail; không dùng fake UoW chứng minh concurrency.
- [x] Row lock, reread, sum verified transactions. Theo baseline đề xuất bỏ D1 confirm sau hold/D8 expire ngay khi thiếu. Ghi muộn/dư vào review, không resurrect order; cancel không tự chi tiền/Done, submit report không tự cancel.
- [x] Tests pass, audit/notification nguyên tử, order Paid nhận dư vẫn Paid và có queue. Commit `fix(payments): unify collection expiry and cancellation`.

## Task 6: Customer request và policy engine

**Files:** port/sửa Refunds/RefundCalculator, Orders/CancellationPreviewService/OrderCancellationEvaluator; tạo RefundRequestService/RefundAllocationService và interfaces; DTO Refund; Controllers/Refunds/CustomerRefundRequestsController.
**Interfaces:** `PreviewAsync(Guid orderId, string reasonCode, CancellationToken ct) -> Task<RefundEligibilityResponse>` (Eligible, Reason, Amount, PolicyVersion); `SubmitAsync(Guid orderId, CreateRefundRequest request, string idempotencyKey, CancellationToken ct) -> Task<RefundRequestResponse>`. Create gồm BankCode/AccountNumber/AccountHolderName/ReasonCode/Note, không nhận amount/percentage quyết định. Query list/detail owner only.

- [x] Viết RefundPolicyEntitlementTests: policy 50% tổng 500.000, Done 500.000 không thêm 250.000, reject nhả reservation, active trừ đúng, boundary/missing rule. CustomerRefundRequestTests: owner, bank validation giữ số 0 đầu, snapshot immutable, replay, concurrent budget, policy thay không đổi request.
- [x] Chạy unit/integration/API test mới để fail trước sửa.
- [x] Tính amount server-side theo policy snapshot và verified collection; reserve budget dưới lock, không áp phần trăm lặp. Một request phân bổ nhiều nguồn thu được. Không tự hủy order, không cho sửa bank details request đã gửi.
- [x] Test pass; submit kiểm tra lại budget dù preview trước đó eligible; legacy chưa rõ phải đối soát. Commit `feat(refunds): add policy-bound customer requests`.

## Task 7: Staff approve/reject/done và đối soát

**Files:** Refunds/StaffRefundRequestService, interface/DTO, Controllers/Refunds/StaffRefundRequestsController, Controllers/Payments/StaffReconciliationController; port audit/notification/query cần thiết A, không executor.
**Interfaces:** `ApproveAsync(Guid requestId, CancellationToken ct)`, `RejectAsync(Guid requestId, string reason, CancellationToken ct)`, `CompleteAsync(Guid requestId, CompleteRefundRequest request, string idempotencyKey, CancellationToken ct)` trả Task<RefundRequestResponse>. Complete: TransferReference, TransferredAt, EvidenceReference?; actor từ current user.

- [x] Viết StaffRefundWorkflowTests: approve không Succeeded, reject có reason/release, chỉ Approved→Done, Done một refund payment, replay cùng body không ghi/notify lần hai, body khác 409; hai staff một assignment, chỉ assigned staff được Done, Customer 403. StaffReconciliationTests: unmatched/late/excess/legacy có queue, wrong account không force-match, expired không resurrect.
- [x] Chạy API role và PostgreSQL concurrency tests để fail trước sửa.
- [x] Approve kiểm tra lại snapshot/budget, assign staff duyệt; staff chuyển bên ngoài theo bank snapshot. Done ghi Succeeded/actual refund totals và trạng thái order đúng. Done timeout thì retry cùng reference/key, không chuyển lại. Reject chỉ PendingReview; undo/reassign chưa mở nếu chưa có action audited.
- [x] Tests pass. Idempotency bảo đảm ghi sổ nội bộ, không tự ngăn thao tác chuyển ngân hàng lặp bên ngoài; assignment/status/reference phải hiện rõ cho staff. Commit `feat(refunds): add staff review and manual completion`.

## Task 8: Worker thu tiền và theo dõi tồn đọng

**Files:** BackgroundJobs/PaymentMaintenanceWorker, PaymentJobs/PaymentJobCursorStore, receipt consumer/expiry/reconciliation options/readiness. Không SyncRefundStatusService hoặc refund execution retry.
**Interfaces:** consumer dùng IReceiptProcessor Task 5. Worker retry receipt/reconcile, expire hold; request PendingReview/Approved chỉ query/báo tồn đọng, không tự approve/reject/Done hay nhả budget.

- [x] Viết PaymentWorkerRecoveryTests: restart receipt, batch lỗi không nghẽn, keyset wrap, hai worker không cộng tiền hai lần, release expired. NoAutomaticRefundTests: Approved qua worker không gọi API chi, không thành Done/Succeeded.
- [x] Chạy integration nhóm trên để fail trước sửa.
- [x] Batch/scope riêng, retry backoff đọc/xử lý thu tiền khi cần. Health phân biệt readiness/liveness, metrics không chứa PII; Approved lâu không tự coi thất bại vì chưa biết tiền staff đã chuyển hay chưa.
- [x] Test pass với clock kiểm soát, không sleep dài. Commit `fix(payments): recover collections without automatic refunds`.

## Task 9: Contract hai màn, hồi quy và rollout

**Files:** docs/05-PAYMENT-INTEGRATION-GUIDE.md, docs/06-MANUAL-REFUND-WORKFLOW.md, docs/ops/payment-rollout.md, OpenAPI/config/render và guide liên quan; ManualRefundContractTests/PaymentEndToEndTests.
**Interfaces:** contract routes/DTO/state/role từ baseline và Task 3/6/7; staff note/evidence nội bộ không tự lộ cho customer. EvidenceReference tái sử dụng storage hiện có; không xây upload system ngoài yêu cầu.

- [x] Viết E2E trip/rental: thu→preview→submit bank→approve→chuyển mô phỏng→Done→customer thấy kết quả; reject/lý do, policy vượt/duplicate/owner/Done retry; không outbound refund API; invitation B giữ đúng Paid/Confirmed.
- [x] Chạy contract/E2E mới thấy fail khi thiếu trạng thái/field/quyền.
- [x] Guide JSON/nút hai màn, masked account, reason codes và idempotency. Rollout backup/dry-run DB có dữ liệu, reconcile trước/sau; dừng executor cũ, đối soát lệnh provider đang pending trước manual để tránh staff hoàn lại khoản đã chi. Schema trước app; rollback không xóa ledger/request.
- [x] Chạy tuần tự `dotnet build RenTrek.sln`, `dotnet test tests/RenTrek.BLL.Tests`, `dotnet test tests/RenTrek.API.Tests`, `dotnet test tests/RenTrek.IntegrationTests`. Thiếu Docker ghi rõ integration chưa chạy. Không chuyển tiền thật để test Done.
- [x] Review không runtime auto refund, không secret/.ua hoặc hồi quy nghiệp vụ B. Commit `docs(refunds): finalize manual workflow and rollout`; bàn giao diff/test, chưa merge/deploy.

## Tiêu chí hoàn thành

- Inbound có ledger/inbox bền vững, duplicate/sai account/sai mã/dư/thiếu/muộn rõ xử lý.
- Customer gửi/theo dõi request có bank snapshot, không xem người khác/tự quyết định amount.
- Staff list/detail/approve/reject/done, audit actor/time/reason/reference đầy đủ; Approved chưa là đã hoàn.
- Done ghi một lần, đúng actual refund totals; policy entitlement không tăng do request lặp/concurrent và snapshot không tự đổi.
- Không API/worker hoàn tiền tự động, không phụ thuộc capability refund SePay. Màn FE là việc cần tích hợp riêng khi có repo FE.
- Migration giữ history, refund cũ đã đối soát, test có output hoặc ghi rõ chưa chạy.

## Self-review

- [x] Quyết định manual refund đã thay toàn bộ phương án hoàn provider; Staff là người xử lý.
- [x] Task 6–7 sở hữu policy, bank snapshot, customer request, staff transitions và ghi sổ Done; Task 8 không chi tiền.
- [x] Năm Review Focus có test, đặc biệt policy không tính lặp và Done retry sau timeout.
- [x] Thu QR và quy tắc hold/chuyển nhiều lần vẫn là đề xuất, không coi manual refund là xác nhận các quyết định đó.
- [x] Implementation tasks 1–9 đã hoàn tất; build sạch và 159 test đạt. Không chuyển tiền thật, chưa merge/deploy. Xem docs/ops/payment-implementation-notes.md cho deviations, review và kết quả.

## Trạng thái bàn giao

Hoàn thành backend trong worktree riêng. Policy cap được tính trên phần tiền đã xác minh, giới hạn theo base/tỷ lệ gốc; khi nguồn thu tăng, chỉ request mới dùng phần quyền lợi bổ sung, không sửa amount request cũ. DTO role suffix và interface/concrete-service khác chi tiết dự kiến được ghi tại `docs/ops/payment-implementation-notes.md`. FE và production rollout là bước tích hợp riêng theo runbook, không được coi là đã deploy trong lần làm việc này.
