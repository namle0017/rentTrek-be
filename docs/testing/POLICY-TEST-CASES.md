# Test cases - Quản lý Chính sách (Hoàn tiền + Thuê đồ)

Nguồn: plan `t-i-c-n-b-n-l-n-functional-charm.md`, `03-API-CONTRACT.md` §20, Screen_Spec O34, PRD FR-F05 / UAT-26, `06-RENTAL-DEPOSIT-FLOW.md`.
Type: Unit / Integration (Postgres service test) / API (HTTP) / UI (vitest) / Manual. Pri: P0 (chặn release) / P1 / P2.
Cột "Auto" ghi file test tự động bao phủ (dấu ✓ = cùng file với nhóm/cột trước; trống = chỉ test tay).

## Business rules (BR)
| BR | Nội dung |
|---|---|---|
| BR-01 | Trạng thái lưu: Draft / Active / Archived. displayStatus: Draft, Scheduled (Active & from>now), Active, Expired (Active & until<=now), Archived |
| BR-02 | Chỉ sửa khi chưa có Trip/Order dùng; ngược lại 409 POLICY_ALREADY_IN_USE |
| BR-03 | Xoá = xoá mềm policy + rules, chỉ khi chưa dùng; đã dùng 409 POLICY_ALREADY_IN_USE |
| BR-04 | Archive chỉ từ Active |
| BR-05 | Activate từ Draft (cần >=1 rule) hoặc từ Archived nếu là bản mới nhất của family, ngược lại 409 POLICY_NOT_LATEST_VERSION |
| BR-06 | Không archive/xoá policy đang hiệu lực duy nhất của một type: 409 REFUND_POLICY_LAST_ACTIVE |
| BR-07 | Clone -> Draft version+1, bản gốc giữ nguyên; activate bản mới đóng (effectiveUntil = new.effectiveFrom) các version cũ cùng family; Rental: đóng cả family Rental khác |
| BR-08 | PolicyType không đổi sau khi tạo |
| BR-09 | Create lưu policy + rules atomically; saveAsDraft=false -> Active |
| BR-10 | Validation: name bắt buộc <=200; type Tour/Rental; description <=2000; until > from; rules 1..20; minHoursBefore int 0..8760 không trùng; % 0..100; % không tăng khi giờ giảm |
| BR-11 | Query: whitelist status/sortBy(createdAt,name,effectiveFrom,version)/sortOrder; search <=200; latestOnly mặc định true |
| BR-12 | Đọc: Admin+Staff; ghi: chỉ Admin (Staff 403) |
| BR-13 | Trip-create dropdown chỉ hiện policy Tour đang hiệu lực |
| BR-14 | Checkout thuê lấy policy Rental mới nhất đang hiệu lực |
| BR-15 | Tính hoàn tiền vẫn chạy với policy đã Archived (trip/order đang dùng) |
| BR-16 | Rental policy: version bất biến; tạo version mới = archive bản cũ + activate bản mới ngay; `/rental-policies/active` public không lộ field admin |
| BR-17 | Rental tiers: mỗi scope >=1 bậc, ngày nguyên >0 không trùng, % 0..100 không giảm theo ngày, đúng 1 bậc terminal và là bậc cuối |

## 1. Validation (RP-V) - Unit (file: RefundPolicyValidatorTests.cs)
| ID | BR | Precondition | Steps | Expected | Type | Pri | Result |
|---|---|---|---|---|---|---|---|
| TC-RP-V01 | BR-10 | - | Validate request hợp lệ (name, Tour, 3 rules) | Valid | Unit | P0 | Pass |
| TC-RP-V02 | BR-10 | - | Name rỗng / chỉ khoảng trắng | Invalid (Name) | Unit | P0 | Pass |
| TC-RP-V03 | BR-10 | - | Name 200 ký tự / 201 ký tự | Valid / Invalid | Unit | P1 | Pass |
| TC-RP-V04 | BR-10 | - | PolicyType = Tour, Rental, "Other", "" | Valid, Valid, Invalid, Invalid | Unit | P0 | Pass |
| TC-RP-V05 | BR-10 | - | Description 2000 / 2001 ký tự / null | Valid / Invalid / Valid | Unit | P1 | Pass |
| TC-RP-V06 | BR-10 | - | EffectiveUntil = From; < From; > From; null | Invalid; Invalid; Valid; Valid | Unit | P0 | Pass |
| TC-RP-V07 | BR-10 | - | EffectiveFrom = default(DateOnly) | Invalid | Unit | P1 | Pass |
| TC-RP-V08 | BR-10 | - | Rules rỗng | Invalid | Unit | P0 | Pass |
| TC-RP-V09 | BR-10 | - | Rules 20 / 21 mốc | Valid / Invalid | Unit | P1 | Pass |
| TC-RP-V10 | BR-10 | - | MinHoursBefore trùng nhau | Invalid | Unit | P0 | Pass |
| TC-RP-V11 | BR-10 | - | MinHoursBefore = -1; 0; 8760; 8761 | Invalid; Valid; Valid; Invalid | Unit | P0 | Pass |
| TC-RP-V12 | BR-10 | - | RefundPercentage = -0.01; 0; 100; 100.01 | Invalid; Valid; Valid; Invalid | Unit | P0 | Pass |
| TC-RP-V13 | BR-10 | - | 168h=100%, 72h=80%, 24h=50%, 0h=0% | Valid | Unit | P0 | Pass |
| TC-RP-V14 | BR-10 | - | 168h=50%, 72h=80% (tăng khi gần giờ đi) | Invalid | Unit | P0 | Pass |
| TC-RP-V15 | BR-10 | - | Hai mốc cùng % (không tăng) | Valid | Unit | P1 | Pass |
| TC-RP-V16 | BR-10 | - | Rules gửi không theo thứ tự giờ nhưng hợp lệ | Valid (validator tự sắp) | Unit | P1 | Pass |
| TC-RP-V17 | BR-10 | - | RefundPolicyRulesRequestValidator: cùng bộ luật như V08-V14 | Cùng kết quả | Unit | P0 | Pass |
| TC-RP-V18 | BR-11 | - | Query: sortBy = createdAt/name/effectiveFrom/version; "evil" | Valid; Invalid | Unit | P1 | Pass |
| TC-RP-V19 | BR-11 | - | Query: sortOrder asc/desc/"up" | Valid/Valid/Invalid | Unit | P1 | Pass |
| TC-RP-V20 | BR-11 | - | Query: status Draft/Scheduled/Active/Expired/Archived/"x" | Valid x5 / Invalid | Unit | P1 | Pass |
| TC-RP-V21 | BR-11 | - | Query: search 200 / 201 ký tự | Valid / Invalid | Unit | P2 | Pass |
| TC-RP-V22 | BR-10 | Admin | POST/PUT với rules sai qua HTTP | 400 envelope, errors keyed | API | P1 | Not run (Postgres/Docker unavailable) |

## 2. Create (RP-C) - file: AdminRefundPolicyServicePostgresTests.cs
| ID | BR | Precondition | Steps | Expected | Type | Pri | Result |
|---|---|---|---|---|---|---|---|
| TC-RP-C01 | BR-09 | Admin | Create Tour 3 rules saveAsDraft=false | Guid; detail có đủ 3 rules; Status Active; Version 1 | Integration | P0 | Not run (Postgres/Docker unavailable) |
| TC-RP-C02 | BR-09 | Admin | Create saveAsDraft=true | Status Draft, displayStatus Draft, rules lưu đủ | Integration | P0 | Not run (Postgres/Docker unavailable) |
| TC-RP-C03 | BR-09 | Admin | Create lưu description + effectiveUntil | Detail trả đúng | Integration | P1 | Not run (Postgres/Docker unavailable) |
| TC-RP-C04 | BR-09 | Admin | Create rồi list | rulesCount = số rules đã gửi (không còn 0) | Integration | P0 | Not run (Postgres/Docker unavailable) |
| TC-RP-C05 | BR-09 | Admin | Create lỗi giữa chừng | Không policy mồ côi (atomic) | Integration | P1 (Manual) | Manual |
| TC-RP-C06 | BR-01 | Admin | Create Active, effectiveFrom tương lai | displayStatus Scheduled | Integration | P0 | Not run (Postgres/Docker unavailable) |
| TC-RP-C07 | BR-01 | Admin | Create Active, effectiveFrom quá khứ | displayStatus Active | Integration | P0 | Not run (Postgres/Docker unavailable) |
| TC-RP-C08 | BR-10 | Admin | Create saveAsDraft=false, 0 rule | 400 validation | API | P0 (Manual) | Manual |
| TC-RP-C09 | BR-12 | Staff | POST create | 403 | API | P0 | Not run (Postgres/Docker unavailable) |
| TC-RP-C10 | BR-09 | Admin | Audit "Create" sau create | AuditLog entity RefundPolicy | Integration | P2 | Not run (Postgres/Docker unavailable) |

## 3. Edit (RP-E)
| ID | BR | Precondition | Steps | Expected | Type | Pri | Result |
|---|---|---|---|---|---|---|---|
| TC-RP-E01 | BR-02 | Draft chưa dùng | PUT đổi name/description/until + PUT rules | Detail phản ánh; rules bị thay hoàn toàn | Integration | P0 | Not run (Postgres/Docker unavailable) |
| TC-RP-E02 | BR-02 | Policy seed (Trip+Order dùng) | PUT | 409 POLICY_ALREADY_IN_USE | Integration | P0 | Not run (Postgres/Docker unavailable) |
| TC-RP-E03 | BR-02 | Policy đã dùng | PUT {id}/rules | 409 POLICY_ALREADY_IN_USE | Integration | P0 | Not run (Postgres/Docker unavailable) |
| TC-RP-E04 | BR-08 | Chưa dùng, type Tour | PUT policyType = Rental | Type vẫn Tour (bỏ qua) hoặc lỗi 4xx; không bao giờ đổi | Integration | P0 | Not run (Postgres/Docker unavailable) |
| TC-RP-E05 | BR-02 | Active chưa dùng | PUT đổi rules | Cho phép | Integration | P1 | Not run (Postgres/Docker unavailable) |
| TC-RP-E06 | BR-02 | Id không tồn tại | PUT | 404 | Integration | P1 | Not run (Postgres/Docker unavailable) |
| TC-RP-E07 | BR-02 | Policy đã xoá mềm | PUT | 404 | Integration | P2 | Not run (Postgres/Docker unavailable) |
| TC-RP-E08 | BR-12 | Staff | PUT / PUT rules | 403 | API | P0 (Manual) | Manual |
| TC-RP-E09 | BR-10 | Admin | PUT rules % tăng dần | 400 | API | P1 (Manual) | Manual |

## 4. Delete (RP-D)
| ID | BR | Precondition | Steps | Expected | Type | Pri | Result |
|---|---|---|---|---|---|---|---|
| TC-RP-D01 | BR-03 | Draft chưa dùng | DELETE | IsDeleted=true cho policy VÀ mọi rule; GET 404; không có trong list | Integration | P0 | Not run (Postgres/Docker unavailable) |
| TC-RP-D02 | BR-03 | Policy đang dùng (seed) | DELETE | 409 POLICY_ALREADY_IN_USE; dữ liệu nguyên | Integration | P0 | Not run (Postgres/Docker unavailable) |
| TC-RP-D03 | BR-06 | Active chưa dùng, Tour hiệu lực duy nhất | DELETE | 409 REFUND_POLICY_LAST_ACTIVE | Integration | P0 | Not run (Postgres/Docker unavailable) |
| TC-RP-D04 | BR-03 | Id không tồn tại | DELETE | 404 | Integration | P1 | Not run (Postgres/Docker unavailable) |
| TC-RP-D05 | BR-03 | Family nhiều version Draft | DELETE 1 version | Version khác còn nguyên | Integration | P1 | Not run (Postgres/Docker unavailable) |
| TC-RP-D06 | BR-12 | Staff | DELETE | 403 | API | P0 | Not run (Postgres/Docker unavailable) |
| TC-RP-D07 | BR-03 | Admin | DELETE ghi audit | AuditLog "Delete" | Integration | P2 | Not run (Postgres/Docker unavailable) |

## 5. Archive / Activate (RP-A)
| ID | BR | Precondition | Steps | Expected | Type | Pri | Result |
|---|---|---|---|---|---|---|---|
| TC-RP-A01 | BR-04 | Active, có Tour hiệu lực khác | Archive | Archived | Integration | P0 | Not run (Postgres/Docker unavailable) |
| TC-RP-A02 | BR-04 | Draft | Archive | 4xx, trạng thái không đổi | Integration | P0 | Not run (Postgres/Docker unavailable) |
| TC-RP-A03 | BR-04 | Archived | Archive lần 2 | 4xx | Integration | P1 | Not run (Postgres/Docker unavailable) |
| TC-RP-A04 | BR-05 | Draft có rules | Activate | Active | Integration | P0 | Not run (Postgres/Docker unavailable) |
| TC-RP-A05 | BR-05 | Draft không rule | Activate | 4xx | Integration | P0 | Not run (Postgres/Docker unavailable) |
| TC-RP-A06 | BR-05 | Archived, mới nhất family | Activate | Active | Integration | P0 | Not run (Postgres/Docker unavailable) |
| TC-RP-A07 | BR-05 | Archived nhưng có version cao hơn | Activate | 409 POLICY_NOT_LATEST_VERSION | Integration | P0 | Not run (Postgres/Docker unavailable) |
| TC-RP-A08 | BR-05 | Active | Activate | 4xx | Integration | P1 | Not run (Postgres/Docker unavailable) |
| TC-RP-A09 | BR-04 | Policy in-use + có Active khác | Archive | Cho phép (in-use không chặn archive) | Integration | P0 | Not run (Postgres/Docker unavailable) |
| TC-RP-A10 | BR-12 | Staff | POST activate / archive | 403 | API | P0 | Not run (Postgres/Docker unavailable) |
| TC-RP-A11 | BR-04 | Active | Archive ghi audit | AuditLog | Integration | P2 | Not run (Postgres/Docker unavailable) |

## 6. Last-active guard (RP-LA)
| ID | BR | Precondition | Steps | Expected | Type | Pri | Result |
|---|---|---|---|---|---|---|---|
| TC-RP-LA01 | BR-06 | Chỉ 1 Tour hiệu lực (seed) | Archive | 409 REFUND_POLICY_LAST_ACTIVE | Integration | P0 | Not run (Postgres/Docker unavailable) |
| TC-RP-LA02 | BR-06 | Seed + Tour Active hiệu lực khác | Archive seed | OK | Integration | P0 | Not run (Postgres/Docker unavailable) |
| TC-RP-LA03 | BR-06 | Seed + Tour Active Scheduled | Archive seed | 409 (Scheduled không tính) | Integration | P1 | Not run (Postgres/Docker unavailable) |
| TC-RP-LA04 | BR-06 | Seed + Rental Active | Archive seed | 409 (guard theo type) | Integration | P1 | Not run (Postgres/Docker unavailable) |
| TC-RP-LA05 | BR-06 | Policy Active đã Expired là duy nhất | Archive | Cho phép | Integration | P2 | Not run (Postgres/Docker unavailable) |
| TC-RP-LA06 | BR-06 | Clone + activate v2 | Archive v1 | OK | Integration | P1 | Not run (Postgres/Docker unavailable) |

## 7. Clone & version family (RP-CL / RP-F)
| ID | BR | Precondition | Steps | Expected | Type | Pri | Result |
|---|---|---|---|---|---|---|---|
| TC-RP-CL01 | BR-07 | Active in-use | Clone | Guid mới; Draft; Version = max+1; cùng PolicyFamilyId; rules copy đủ | Integration | P0 | Not run (Postgres/Docker unavailable) |
| TC-RP-CL02 | BR-07 | Như trên | Đọc bản gốc sau clone | Status/EffectiveUntil/rules KHÔNG đổi | Integration | P0 | Not run (Postgres/Docker unavailable) |
| TC-RP-CL03 | BR-07 | Draft v2 | Activate v2 | v1.effectiveUntil = v2.effectiveFrom; v1 displayStatus Expired | Integration | P0 | Not run (Postgres/Docker unavailable) |
| TC-RP-CL04 | BR-07 | Clone 2 lần | - | Version 2 rồi 3 | Integration | P1 | Not run (Postgres/Docker unavailable) |
| TC-RP-CL05 | BR-07 | Draft v2 | PUT rules v2 | Cho phép; v1 không đổi | Integration | P1 | Not run (Postgres/Docker unavailable) |
| TC-RP-CL06 | BR-07 | 2 family Rental, A Active | Activate B | A bị đóng; chỉ 1 Rental hiệu lực | Integration | P0 | Not run (Postgres/Docker unavailable) |
| TC-RP-CL07 | BR-07 | 2 family Tour | Activate family B | Family A không bị đóng | Integration | P1 | Not run (Postgres/Docker unavailable) |
| TC-RP-F01 | BR-11 | Family 3 versions | GET detail | versions[] đủ 3, version desc | Integration | P0 | Not run (Postgres/Docker unavailable) |
| TC-RP-F02 | BR-11 | Family 3 versions | list latestOnly mặc định | 1 dòng/family, versionsCount=3 | Integration | P0 | Not run (Postgres/Docker unavailable) |
| TC-RP-F03 | BR-11 | như trên | latestOnly=false | cả 3 dòng | Integration | P0 | Not run (Postgres/Docker unavailable) |
| TC-RP-F04 | BR-07 | Nguồn Archived | Clone | Cho phép; Draft | Integration | P2 | Not run (Postgres/Docker unavailable) |
| TC-RP-F05 | BR-12 | Staff | POST clone-version | 403 | API | P1 (Manual) | Manual |

## 8. Status / displayStatus / flags (RP-S)
| ID | BR | Precondition | Steps | Expected | Type | Pri | Result |
|---|---|---|---|---|---|---|---|
| TC-RP-S01 | BR-01 | Draft | GET detail | Draft; canActivate=true; canArchive=false; canEdit=true; canDelete=true | Integration | P0 | Not run (Postgres/Docker unavailable) |
| TC-RP-S02 | BR-01 | Active hiệu lực, chưa dùng | GET | Active; canArchive; canEdit; !canActivate | Integration | P0 | Not run (Postgres/Docker unavailable) |
| TC-RP-S03 | BR-01 | Active, from>now | GET | Scheduled | Integration | P0 | Not run (Postgres/Docker unavailable) |
| TC-RP-S04 | BR-01 | Active, until<=now | GET | Expired | Integration | P0 | Not run (Postgres/Docker unavailable) |
| TC-RP-S05 | BR-01 | Archived | GET | Archived; canActivate chỉ true nếu mới nhất | Integration | P0 | Not run (Postgres/Docker unavailable) |
| TC-RP-S06 | BR-02 | In-use | GET | isLocked; !canEdit; !canDelete; canClone | Integration | P0 | Not run (Postgres/Docker unavailable) |
| TC-RP-S07 | BR-01 | until == now | GET | Expired (<=) | Integration | P1 | Not run (Postgres/Docker unavailable) |
| TC-RP-S08 | BR-02 | Seed | list | tripsCount=1, ordersCount>=1, usageCount=trips+orders | Integration | P0 | Not run (Postgres/Docker unavailable) |

## 9. Paging / filter / sort (RP-Q)
| ID | BR | Precondition | Steps | Expected | Type | Pri | Result |
|---|---|---|---|---|---|---|---|
| TC-RP-Q01 | BR-11 | 5 policy | pageSize=2, page 1..3 | 2/2/1; totalItems; totalPages=3 | Integration | P0 | Not run (Postgres/Docker unavailable) |
| TC-RP-Q02 | BR-11 | mix | policyType=Tour | chỉ Tour | Integration | P0 | Not run (Postgres/Docker unavailable) |
| TC-RP-Q03 | BR-11 | mix | status=Draft | chỉ Draft | Integration | P0 | Not run (Postgres/Docker unavailable) |
| TC-RP-Q04 | BR-11 | mix | status=Scheduled/Expired/Archived/Active | Lọc theo displayStatus | Integration | P0 | Not run (Postgres/Docker unavailable) |
| TC-RP-Q05 | BR-11 | tên khác nhau | search không phân biệt hoa thường | Chỉ policy có tên chứa | Integration | P1 | Not run (Postgres/Docker unavailable) |
| TC-RP-Q06 | BR-11 | - | sortBy=name asc/desc | Đúng thứ tự | Integration | P1 | Not run (Postgres/Docker unavailable) |
| TC-RP-Q07 | BR-11 | - | sortBy=version/effectiveFrom/createdAt | Đúng thứ tự | Integration | P2 | Not run (Postgres/Docker unavailable) |
| TC-RP-Q08 | BR-11 | - | Không gửi sort | Mặc định ổn định | Integration | P2 (Manual) | Manual |
| TC-RP-Q09 | BR-11 | - | GET ?sortBy=bad | 400 | API | P1 (Manual) | Manual |
| TC-RP-Q10 | BR-11 | - | pageSize=101 | Kẹp 100 | API | P2 (Manual) | Manual |
| TC-RP-Q11 | BR-11 | - | createdByName | Tên admin tạo | Integration | P1 | Not run (Postgres/Docker unavailable) |
| TC-RP-Q12 | BR-11 | policy xoá mềm | list | Không xuất hiện | Integration | P0 | Not run (Postgres/Docker unavailable) |

## 10. Permissions (RP-P) - file: AdminHttpTests.cs
| ID | BR | Precondition | Steps | Expected | Type | Pri | Result |
|---|---|---|---|---|---|---|---|
| TC-RP-P01 | BR-12 | Staff | GET admin/refund-policies | 200 | API | P0 | Not run (Postgres/Docker unavailable) |
| TC-RP-P02 | BR-12 | Staff | GET admin/refund-policies/{id} | 200 | API | P0 | Not run (Postgres/Docker unavailable) |
| TC-RP-P03 | BR-12 | Staff | GET admin/rental-policies | 200 | API | P0 | Not run (Postgres/Docker unavailable) |
| TC-RP-P04 | BR-12 | Staff | DELETE refund-policies/{id} | 403 | API | P0 | Not run (Postgres/Docker unavailable) |
| TC-RP-P05 | BR-12 | Staff | POST refund-policies/{id}/activate | 403 | API | P0 | Not run (Postgres/Docker unavailable) |
| TC-RP-P06 | BR-12 | Staff | POST admin/rental-policies | 403 | API | P0 | Not run (Postgres/Docker unavailable) |
| TC-RP-P07 | BR-12 | Anonymous / Customer | Mọi route admin | 401 / 403 | API | P0 (test hiện có; cập nhật tổng route) | Not run (Postgres/Docker unavailable) |
| TC-RP-P08 | BR-12 | Admin | Mọi route ghi | 2xx | API | P0 (Manual) | Manual |
| TC-RP-P09 | BR-16 | Anonymous | GET rental-policies/active | 200, không có ordersCount/createdByName/createdAt | API | P0 (Manual) | Manual |

## 11. Consumers (RP-X)
| ID | BR | Precondition | Steps | Expected | Type | Pri | Result |
|---|---|---|---|---|---|---|---|
| TC-RP-X01 | BR-13 | Tour v1 expired, v2 Active, 1 Rental Active | Mở dropdown trip create | Chỉ Tour hiệu lực (v2); gọi `?policyType=Tour&status=Active` | UI/Manual | P0 | Manual |
| TC-RP-X02 | BR-13 | Policy Scheduled | Dropdown | Không hiện | Manual | P1 | Manual |
| TC-RP-X03 | BR-13 | Draft / Archived | Dropdown | Không hiện | Manual | P1 | Manual |
| TC-RP-X04 | BR-13 | Trip tạo không chọn policy | TripRepository.GetActiveRefundPolicyAsync | Tour hiệu lực mới nhất | Integration | P0 | Not run (Postgres/Docker unavailable) |
| TC-RP-X05 | BR-13 | Recurrence rule không chỉ định policy | GetDefaultOrSpecificRefundPolicyAsync | Tour hiệu lực; bỏ 'TripBooking' | Integration | P1 | Not run (Postgres/Docker unavailable) |
| TC-RP-X06 | BR-14 | Rental refund policy v1, v2 Active | Checkout thuê | Order.RefundPolicyId = bản mới nhất | Integration/Manual | P0 | Manual |
| TC-RP-X07 | BR-14 | Đã activate Rental family B | Checkout | Chỉ policy của B | Manual | P1 | Manual |
| TC-RP-X08 | BR-15 | Trip dùng policy Archived | Khách huỷ | Vẫn tính theo rules | Integration | P0 | Not run (Postgres/Docker unavailable) |
| TC-RP-X09 | BR-15 | Order snapshot policy Expired | Refund case | Vẫn tính được | Integration | P1 | Not run (Postgres/Docker unavailable) |
| TC-RP-X10 | BR-13 | TourSeedData | Khởi tạo DB | policy_type = 'Tour' | Integration | P1 | Not run (Postgres/Docker unavailable) |
| TC-RP-X11 | BR-13 | Migration chạy 2 lần | Idempotent; chuẩn hoá | Manual | P1 | Manual |

## 12. Rental policy (RP-R) - file: RentalPolicyAdminPostgresTests.cs
| ID | BR | Precondition | Steps | Expected | Type | Pri | Result |
|---|---|---|---|---|---|---|---|
| TC-RP-R01 | BR-16 | 2 version | GetAll admin | Version desc; có ordersCount, createdByName, createdAt | Integration | P0 | Not run (Postgres/Docker unavailable) |
| TC-RP-R02 | BR-16 | Đơn thuê snapshot v1 | GetAll | v1.ordersCount đúng | Integration | P0 | Not run (Postgres/Docker unavailable) |
| TC-RP-R03 | BR-16 | - | GetById | đủ tiers 2 scope + admin fields | Integration | P0 | Not run (Postgres/Docker unavailable) |
| TC-RP-R04 | BR-16 | - | GetById không tồn tại | NotFound | Integration | P1 | Not run (Postgres/Docker unavailable) |
| TC-RP-R05 | BR-16 | v1 Active | CreateVersion | v1 Archived + effectiveUntil; v2 Active; cùng family | Integration | P0 | Not run (Postgres/Docker unavailable) |
| TC-RP-R06 | BR-16 | DB trống | CreateVersion | Version 1 Active | Integration | P1 | Not run (Postgres/Docker unavailable) |
| TC-RP-R07 | BR-17 | - | thiếu scope / terminal sai / % giảm / ngày trùng / ngày<=0 | ValidationException (key `tiers.<scope>`) | Integration | P0 | Not run (Postgres/Docker unavailable) |
| TC-RP-R08 | BR-16 | - | Bậc terminal | PercentOfDeposit ép 100 | Integration | P2 | Not run (Postgres/Docker unavailable) |
| TC-RP-R09 | BR-16 | sourcePolicyId (API) | POST có sourcePolicyId | Version mới áp dụng ngay | API | P1 (Manual) | Manual |
| TC-RP-R10 | BR-16 | Đơn cũ snapshot v1 | Tạo v2 | GetSnapshotAsync(v1) vẫn số cũ | Integration | P0 | Not run (Postgres/Docker unavailable) |
| TC-RP-R11 | BR-16 | - | name rỗng / >255 | ValidationException | Integration | P1 | Not run (Postgres/Docker unavailable) |
| TC-RP-R12 | BR-16 | Anonymous | /active không field admin | = TC-RP-P09 | API | P0 (Manual) | Manual |

## 13. FE UI (RP-U) - vitest
| ID | BR | Precondition | Steps | Expected | Type | Pri | Result |
|---|---|---|---|---|---|---|---|
| TC-RP-U01 | BR-10 | - | validateRefundRules hợp lệ | isValid=true | UI(unit) | P0 | Pass |
| TC-RP-U02 | BR-10 | - | rỗng / >20 / giờ trùng / âm / >8760 / không nguyên / % ngoài 0..100 / % tăng khi giờ giảm | fieldErrors `rules.<i>.minHoursBefore|refundPercentage` hoặc rootError | UI(unit) | P0 | Pass |
| TC-RP-U03 | - | - | hasZeroHourRule | true khi có mốc 0 giờ | UI(unit) | P2 | Pass |
| TC-RP-U04 | BR-17 | - | validateRentalTiers theo BR-17 | errors.PickupLate / ReturnLate | UI(unit) | P0 | Pass |
| TC-RP-U05 | - | - | formatHours(72/0/5/36/168) | "3 ngày (72 giờ)", "0 giờ", "5 giờ", "1,5 ngày (36 giờ)", "7 ngày (168 giờ)" | UI(unit) | P1 | Pass |
| TC-RP-U06 | - | open=false | ConfirmDialog | Không render nội dung | UI | P1 | Pass |
| TC-RP-U07 | - | open | Bấm Xác nhận / Huỷ / isPending | onConfirm / onClose; disabled khi pending | UI | P0 | Pass |
| TC-RP-U08 | BR-01 | 5 status | PolicyStatusBadge | Bản nháp / Sắp hiệu lực / Đang áp dụng / Hết hiệu lực / Ngừng áp dụng | UI | P0 | Pass |
| TC-RP-U09 | BR-12 | Staff | Danh sách refund | Không có link/nút "Tạo chính sách" | UI | P0 | Pass |
| TC-RP-U10 | BR-12 | Admin | Danh sách refund | Có link tạo -> /admin/refund-policies/new | UI | P0 | Pass |
| TC-RP-U11 | BR-11 | Admin | Lọc Loại / Trạng thái / tìm kiếm / bỏ "Chỉ hiện phiên bản mới nhất" | getRefundPolicies gọi đúng params | UI | P0 | Pass |
| TC-RP-U12 | - | data rỗng / lỗi | Danh sách | EmptyState / ErrorState | UI | P1 | Pass |
| TC-RP-U13 | BR-09 | Admin | Editor: tên, 2 mốc, "Lưu & áp dụng" | createRefundPolicy payload đủ, saveAsDraft:false | UI | P0 | Pass |
| TC-RP-U14 | BR-09 | Admin | "Lưu nháp" | saveAsDraft:true | UI | P0 | Pass |
| TC-RP-U15 | BR-10 | Admin | % tăng khi giờ giảm -> submit | Lỗi từng ô; KHÔNG gọi API | UI | P0 | Pass |
| TC-RP-U16 | BR-10 | Admin | Giờ trùng -> submit | Lỗi; không gọi API | UI | P0 | Pass |
| TC-RP-U17 | BR-10 | Admin | Thiếu tên | Lỗi, không gọi API | UI | P1 | Pass |
| TC-RP-U18 | BR-08 | Sửa policy | Select Loại | disabled | UI | P1 | Pass |
| TC-RP-U19 | BR-02 | canEdit=false | Mở /:id/edit | Chuyển về /:id | UI | P0 | Pass |
| TC-RP-U20 | BR-02 | Detail với từng cờ can* | Render | Nút hiện đúng theo cờ | UI | P0 | Pass |
| TC-RP-U21 | BR-02 | isLocked=true | Render | Banner khoá | UI | P0 | Pass |
| TC-RP-U22 | BR-12 | Staff | Detail | Không nút ghi dù cờ true | UI | P0 | Pass |
| TC-RP-U23 | BR-03 | Admin canDelete | Bấm Xoá | ConfirmDialog; chỉ gọi delete sau xác nhận | UI | P0 | Pass |
| TC-RP-U24 | BR-04 | Admin canArchive | Bấm Ngừng áp dụng | ConfirmDialog -> archive | UI | P1 | Pass |
| TC-RP-U25 | BR-07 | canClone | Tạo phiên bản mới | cloneRefundPolicy; điều hướng tới id mới | UI | P0 | Pass |
| TC-RP-U26 | BR-05 | canActivate | Kích hoạt | activateRefundPolicy | UI | P1 | Pass |
| TC-RP-U27 | - | Admin | Live chart khi đổi % | SVG đổi theo | Manual | P1 | Manual |
| TC-RP-U28 | BR-12 | Staff | Vào /admin/refund-policies/new | Bị chặn | Manual | P0 | Manual |
| TC-RP-U29 | - | - | /ops/refund-policies | Chuyển hướng | Manual | P1 | Manual |
| TC-RP-U30 | - | - | Nav "Chính sách" cho Staff + Admin | Hiển thị | Manual | P1 | Manual |
| TC-RP-U31 | BR-16 | Rental detail | "Tạo phiên bản mới từ bản này" | /new?from=id, điền sẵn | Manual | P1 | Manual |
| TC-RP-U32 | BR-16 | Rental create | Submit | ConfirmDialog "Áp dụng ngay cho đơn mới" | Manual | P1 | Manual |
| TC-RP-U33 | BR-13 | Trip create | Dropdown | `policyType=Tour&status=Active` | UI | P1 | Not run (Postgres/Docker unavailable) |
| TC-RP-U34 | - | List trả PagedResponse | Dropdown trip-create | Xử lý được cả mảng lẫn paged | UI | P1 | Not run (Postgres/Docker unavailable) |

## 14. Regression (RP-G) - bug trong plan Context
| ID | Bug | Steps | Expected | Type | Pri | Result |
|---|---|---|---|---|---|---|
| TC-RP-G01 | Tạo mới mất rules | Create 3 mốc -> detail | 3 mốc (= C01) | Integration | P0 | Not run (Postgres/Docker unavailable) |
| TC-RP-G02 | Danh sách luôn "0 mốc" | list.rulesCount (= C04) | số mốc thật | Integration | P0 | Not run (Postgres/Docker unavailable) |
| TC-RP-G03 | Số chuyến undefined | tripsCount/ordersCount/usageCount (= S08) | là số | Integration/UI | P0 | Not run (Postgres/Docker unavailable) |
| TC-RP-G04 | Nhân bản đọc sai `res.data` | Clone qua UI (= U25) | Điều hướng tới Guid mới | UI | P0 | Not run (Postgres/Docker unavailable) |
| TC-RP-G05 | Trạng thái luôn "Active" | Create Draft -> list (= C02) | displayStatus Draft | Integration | P0 | Not run (Postgres/Docker unavailable) |
| TC-RP-G06 | Không sửa/xoá/ngừng được | E01, D01, A01 | Làm được khi hợp lệ | Integration | P0 | Not run (Postgres/Docker unavailable) |
| TC-RP-G07 | Seed 'TOUR' không khớp lọc 'Tour' | list policyType=Tour | có kết quả | Integration | P1 | Not run (Postgres/Docker unavailable) |
| TC-RP-G08 | Rental FE chưa có màn | Mở /admin/rental-policies | Thẻ Đang áp dụng + bảng version | Manual | P1 | Manual |
| TC-RP-G09 | Clone cũ đóng bản gốc ngay | Clone (= CL02) | Bản gốc không đổi | Integration | P0 | Not run (Postgres/Docker unavailable) |
| TC-RP-G10 | Dropdown liệt kê version hết hạn | = X01 | Chỉ hiệu lực | Manual | P0 | Manual |

## Ambiguities / contradictions phát hiện
1. §20.6 (API contract hiện tại) nói clone đặt `effectiveUntil = now` cho bản cũ ngay và bản mới Active; plan nói clone tạo Draft và chỉ đóng bản cũ lúc activate. Test theo plan.
2. `POLICY_ALREADY_IN_USE` hiện ném 400 (BadRequestException) trong `GetEditableAsync`; plan/lead yêu cầu 409. Test assert 409 (ConflictException).
3. Contract §20.1 trả mảng + `activeTripsCount`; plan: PagedResponse + tripsCount/ordersCount/usageCount. Test không dùng `activeTripsCount`.
4. Nhãn Archived của refund là "Ngừng áp dụng", của rental là "Đã thay thế": chủ ý?
5. Create với saveAsDraft=false và effectiveFrom tương lai/quá khứ có gọi ApplyActivationEffects (đóng version/family Rental khác) không? Giả định: create chỉ tạo family mới; test không assert.
6. Rental validator ở service (name <=255, ValidationException) khác refund (FluentValidation <=200).
7. Validator rules hiện chưa chặn minHoursBefore > 8760 (plan thêm); TC-RP-V11 yêu cầu thay đổi này.
8. "Bản mới nhất của family" khi activate Archived: tính cả version Draft cao hơn? Giả định: tính mọi version chưa xoá.
9. Định nghĩa "hiệu lực" cho guard last-active: Active & from<=now & (until null | until>now), theo type. Refund policy Rental khác khái niệm RentalPolicy (cọc).
10. Sửa Active chưa dùng cho phép theo plan; không nói ảnh hưởng tới trip tạo sau.
11. `AdminHttpTests.EveryAdminRoute...` có `Assert.Equal(169, count)`: sẽ lệch khi BE thêm route (activate, archive, delete, GET rental-policies/{id}: +4 và có thể thêm); chốt ở phase 2.
12. Với POLICY_NOT_LATEST_VERSION / REFUND_POLICY_LAST_ACTIVE chưa có trong `ErrorCodes.cs` hiện tại (chỉ có PolicyAlreadyInUse); BE cần thêm.


## Kết quả chạy (phase 2)
- BE `dotnet test -c Release`: BLL 350 pass / 0 fail / 79 skip; API 31 pass / 0 fail / 6 skip; Architecture 41 pass. Mọi test cần Postgres (service, rental, HTTP) bị SKIP vì Docker daemon không chạy và không có RENTREK_TEST_POSTGRES: các TC đó "Not run" (đã compile).
- FE `vitest run`: 286 pass / 4 fail (PostEditorForm, TripBookingCreatePage: ngoài phạm vi policies); `npm run build` thành công. Các test policies (97) đều pass.
- Số route admin thực tế: 187 (gồm `GET /api/v1/rental-policies/active` AllowAnonymous nằm trong AdminRentalPoliciesController). Test 401/403 đã loại route AllowAnonymous nên kỳ vọng 186.
