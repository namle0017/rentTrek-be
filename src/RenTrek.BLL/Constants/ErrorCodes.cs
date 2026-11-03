namespace RenTrek.BLL.Constants;

/// <summary>
/// Mã lỗi nghiệp vụ trả trong errors.code[0] của ApiResponse (contract 03.1 phụ lục B và các mã bổ sung cho luồng thanh toán / hoàn tiền).
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
    public const string RefundExceedsCollected = "REFUND_EXCEEDS_COLLECTED";
    public const string OrderNotReconcilable = "ORDER_NOT_RECONCILABLE";
    public const string PaymentProviderError = "PAYMENT_PROVIDER_ERROR";
    public const string Forbidden = "FORBIDDEN";
    public const string ConcurrencyConflict = "CONCURRENCY_CONFLICT";
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
}
