namespace RenTrek.BLL.Constants;

/// <summary>
/// Mã lỗi nghiệp vụ trả trong errors.code[0] của ApiResponse (contract 03.1 phụ lục B, mục 8.7 TripGroup và các mã bổ sung cho luồng thanh toán / hoàn tiền).
/// </summary>
public static class ErrorCodes
{
    public const string ValidationError = "VALIDATION_ERROR";
    public const string InvalidRentalPeriod = "INVALID_RENTAL_PERIOD";
    public const string InvalidRentalItem = "INVALID_RENTAL_ITEM";
    public const string PriceTierNotFound = "PRICE_TIER_NOT_FOUND";
    public const string IdempotencyKeyRequired = "IDEMPOTENCY_KEY_REQUIRED";
    public const string IdempotencyKeyReused = "IDEMPOTENCY_KEY_REUSED";
    public const string ItemNotFound = "ITEM_NOT_FOUND";
    public const string ProductNotFound = "PRODUCT_NOT_FOUND";
    public const string BundleNotFound = "BUNDLE_NOT_FOUND";
    public const string OrderNotFound = "ORDER_NOT_FOUND";
    public const string PaymentNotFound = "PAYMENT_NOT_FOUND";
    public const string ChargeNotFound = "CHARGE_NOT_FOUND";
    public const string ResourceUnavailable = "RESOURCE_UNAVAILABLE";
    public const string PromotionInvalid = "PROMOTION_INVALID";
    public const string PromotionExhausted = "PROMOTION_EXHAUSTED";
    public const string RefundPolicyChanged = "REFUND_POLICY_CHANGED";
    public const string OrderNotPayable = "ORDER_NOT_PAYABLE";
    public const string HoldExpired = "HOLD_EXPIRED";
    public const string OrderNotCancellable = "ORDER_NOT_CANCELLABLE";
    public const string RefundAmountChanged = "REFUND_AMOUNT_CHANGED";
    public const string ChargeNotReviewable = "CHARGE_NOT_REVIEWABLE";
    public const string RefundNotApprovable = "REFUND_NOT_APPROVABLE";
    public const string RefundNotRetryable = "REFUND_NOT_RETRYABLE";
    public const string RefundNotCompletable = "REFUND_NOT_COMPLETABLE";
    public const string RefundExceedsCollected = "REFUND_EXCEEDS_COLLECTED";
    public const string OrderNotReconcilable = "ORDER_NOT_RECONCILABLE";
    public const string PaymentProviderError = "PAYMENT_PROVIDER_ERROR";
    public const string Forbidden = "FORBIDDEN";

    // TripGroup (03-API-CONTRACT.md §8.7)
    public const string TripGroupCountLocked = "TRIP_GROUP_COUNT_LOCKED";
    public const string TripOperationalCapacityInsufficient = "TRIP_OPERATIONAL_CAPACITY_INSUFFICIENT";
    public const string GroupCapacityExceeded = "GROUP_CAPACITY_EXCEEDED";
    public const string TripNotAssigned = "TRIP_NOT_ASSIGNED";
    public const string GroupWrongTrip = "GROUP_WRONG_TRIP";
    public const string GuideScheduleConflict = "GUIDE_SCHEDULE_CONFLICT";
    public const string TripGroupingLocked = "TRIP_GROUPING_LOCKED";
    public const string TripNotClosedForBooking = "TRIP_NOT_CLOSED_FOR_BOOKING";
    public const string ActivePaymentHolds = "ACTIVE_PAYMENT_HOLDS";
    public const string UnassignedParticipants = "UNASSIGNED_PARTICIPANTS";
    public const string GroupGuideRequired = "GROUP_GUIDE_REQUIRED";
    public const string EquipmentRequirementsStale = "EQUIPMENT_REQUIREMENTS_STALE";
    public const string ConcurrencyConflict = "CONCURRENCY_CONFLICT";

    // Hoàn tiền thủ công (05-MANUAL-REFUND-WORKFLOW.md §12)
    public const string RefundCaseNotFound = "REFUND_CASE_NOT_FOUND";
    public const string RefundCaseNotRejectable = "REFUND_CASE_NOT_REJECTABLE";
    public const string RefundCaseNotCompletable = "REFUND_CASE_NOT_COMPLETABLE";
    public const string RefundExportEmpty = "REFUND_EXPORT_EMPTY";
    public const string RefundTransactionDuplicated = "REFUND_TRANSACTION_DUPLICATED";
    public const string RefundVerificationRequired = "REFUND_VERIFICATION_REQUIRED";
    public const string RefundVerificationInvalid = "REFUND_VERIFICATION_INVALID";
    public const string RefundVerificationExpired = "REFUND_VERIFICATION_EXPIRED";
    public const string RefundSensitiveDataPurged = "REFUND_SENSITIVE_DATA_PURGED";
    public const string TooManyRequests = "TOO_MANY_REQUESTS";

    // Danh mục admin: tour, sản phẩm, chính sách hoàn tiền (03-API-CONTRACT.md §6, §13, §20)
    public const string NotFound = "NOT_FOUND";
    public const string ItineraryLocked = "ITINERARY_LOCKED";
    public const string PolicyAlreadyInUse = "POLICY_ALREADY_IN_USE";
    public const string RefundPolicyLastActive = "REFUND_POLICY_LAST_ACTIVE";
    public const string PolicyNotLatestVersion = "POLICY_NOT_LATEST_VERSION";
    public const string InvalidPolicyState = "INVALID_POLICY_STATE";
    public const string SkuAlreadyExists = "SKU_ALREADY_EXISTS";

    // Đánh giá sau chuyến đi (T2)
    public const string ReviewNotEligible = "REVIEW_NOT_ELIGIBLE";
    public const string ReviewAlreadyExists = "REVIEW_ALREADY_EXISTS";
    public const string ReviewEditWindowClosed = "REVIEW_EDIT_WINDOW_CLOSED";

    // Gia hạn đơn thuê (T5)
    public const string RentalNotExtendable = "RENTAL_NOT_EXTENDABLE";
    public const string RentalExtensionPriceChanged = "RENTAL_EXTENSION_PRICE_CHANGED";

    // Cọc, huỷ đơn đã thanh toán, giao nhận có bằng chứng
    public const string EmailNotVerified = "EMAIL_NOT_VERIFIED";
    public const string RentalOutstandingDebt = "RENTAL_OUTSTANDING_DEBT";
    public const string RentalEvidenceRequired = "RENTAL_EVIDENCE_REQUIRED";
    public const string RentalNoShowNotReached = "RENTAL_NO_SHOW_NOT_REACHED";
    public const string RentalReceiptNotConfirmable = "RENTAL_RECEIPT_NOT_CONFIRMABLE";
    public const string RentalReportNotFound = "RENTAL_REPORT_NOT_FOUND";
    public const string RentalReportNotReviewable = "RENTAL_REPORT_NOT_REVIEWABLE";
    public const string RefundBankInfoNotRequired = "REFUND_BANK_INFO_NOT_REQUIRED";
    public const string RefundCaseNotResubmittable = "REFUND_CASE_NOT_RESUBMITTABLE";
    public const string RentalExtensionPending = "RENTAL_EXTENSION_PENDING";
}
