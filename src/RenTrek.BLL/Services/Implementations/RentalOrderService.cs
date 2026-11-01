using System.Globalization;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Promotion.Internal;
using RenTrek.BLL.DTOs.Rental.Internal;
using RenTrek.BLL.DTOs.Rental.Requests;
using RenTrek.BLL.DTOs.RentalOrder.Internal;
using RenTrek.BLL.DTOs.RentalOrder.Requests;
using RenTrek.BLL.DTOs.RentalOrder.Responses;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Helpers;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Models;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Services.Implementations;

/// <summary>
/// Thực hiện nghiệp vụ đơn thuê thiết bị: báo giá, tạo đơn (idempotent, giữ tài sản nguyên tử) và xem chi tiết đơn của khách.
/// </summary>
public class RentalOrderService : IRentalOrderService
{
    private const int MaxNoteLength = 1500;
    private const int MaxIdempotencyKeyLength = 100;
    private const int OrderCodeAttempts = 5;
    private const string ChargeStatusDraft = "Draft";
    private const string PaymentTypeCollection = "Collection";
    private const string PaymentTypeRefund = "Refund";
    private const string PaymentStatusSucceeded = "Succeeded";

    // Chỉ các phân bổ đã giao / đã trả / ngoại lệ mới hiển thị cho khách (Reserved bị ẩn để không lộ mã tài sản)
    private static readonly string[] VisibleAssignmentStatuses =
    [
        AssignmentStatuses.Issued,
        AssignmentStatuses.Returned,
        AssignmentStatuses.LostOrException
    ];

    private readonly IUnitOfWork _unitOfWork;
    private readonly IRentalAvailabilityService _availabilityService;
    private readonly IPromotionService _promotionService;
    private readonly TimeProvider _timeProvider;

    public RentalOrderService(
        IUnitOfWork unitOfWork,
        IRentalAvailabilityService availabilityService,
        IPromotionService promotionService,
        TimeProvider timeProvider)
    {
        _unitOfWork = unitOfWork;
        _availabilityService = availabilityService;
        _promotionService = promotionService;
        _timeProvider = timeProvider;
    }

    public async Task<RentalQuoteResponse> QuoteAsync(
        Guid userId,
        RentalQuoteRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        var period = RentalPeriodCalculator.Validate(request.RentalStartAt, request.RentalEndAt, now.UtcDateTime);
        var resolvedLines = await _availabilityService.ResolveLinesAsync(ToLineInputs(request.Items), cancellationToken);

        var computation = await ComputeAsync(userId, period, resolvedLines, request.PromotionCode, now, cancellationToken);
        return MapQuote(computation);
    }

    public async Task<RentalOrderDetailResponse> CreateAsync(
        Guid userId,
        CreateRentalOrderRequest request,
        string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var normalizedKey = idempotencyKey?.Trim();
        if (string.IsNullOrEmpty(normalizedKey))
            throw new BadRequestException("Header Idempotency-Key là bắt buộc", ErrorCodes.IdempotencyKeyRequired);
        if (normalizedKey.Length > MaxIdempotencyKeyLength)
        {
            throw new BadRequestException(
                $"Idempotency-Key không vượt quá {MaxIdempotencyKeyLength} ký tự",
                ErrorCodes.ValidationError);
        }

        // Đường nhanh: gửi lại cùng key thì trả lại đơn đã tạo (kiểm tra trước khi validate thời gian vì đơn cũ có thể đã qua giờ nhận)
        var existing = await _unitOfWork.RentalOrders.GetByIdempotencyKeyAsync(userId, normalizedKey, cancellationToken);
        if (existing is not null)
        {
            await EnsureIdempotentRequestMatchesAsync(existing, request, cancellationToken);
            return await GetByIdAsync(userId, existing.OrderId, cancellationToken);
        }

        var period = RentalPeriodCalculator.Validate(
            request.RentalStartAt,
            request.RentalEndAt,
            _timeProvider.GetUtcNow().UtcDateTime);
        var resolvedLines = await _availabilityService.ResolveLinesAsync(ToLineInputs(request.Items), cancellationToken);
        var demandByVariant = RentalAvailabilityCalculator.AggregateDemand(resolvedLines);

        Guid orderId;
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _unitOfWork.RentalOrders.AcquireIdempotencyLockAsync(userId, normalizedKey, cancellationToken);
            existing = await _unitOfWork.RentalOrders.GetByIdempotencyKeyAsync(userId, normalizedKey, cancellationToken);
            if (existing is not null)
            {
                await EnsureIdempotentRequestMatchesAsync(existing, request, cancellationToken);
                await _unitOfWork.CommitAsync(cancellationToken);
                return await GetByIdAsync(userId, existing.OrderId, cancellationToken);
            }

            // Khoá biến thể (đã sắp xếp trong service) rồi khoá khuyến mãi, sau đó mới tính lại để kiểm tra và giữ chỗ nguyên tử
            await _availabilityService.LockVariantsAsync(demandByVariant.Keys, cancellationToken);

            var promotionCode = request.PromotionCode?.Trim();
            if (!string.IsNullOrEmpty(promotionCode))
            {
                var promotion = await _unitOfWork.Promotions.GetByCodeAsync(promotionCode, cancellationToken);
                if (promotion is not null)
                    await _promotionService.LockAsync(promotion.PromotionId, cancellationToken);
            }

            var now = _timeProvider.GetUtcNow();
            var computation = await ComputeAsync(userId, period, resolvedLines, promotionCode, now, cancellationToken);
            EnsureAcceptable(computation, request.AcceptedRefundPolicyVersion);

            var assetsByVariant = await _availabilityService.PickAssetsAsync(period, demandByVariant, cancellationToken);
            var orderCode = await GenerateOrderCodeAsync(now, cancellationToken);
            var order = BuildOrder(userId, request, normalizedKey, orderCode, computation, assetsByVariant, now);

            await _unitOfWork.RentalOrders.AddOrderAsync(order, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);
            orderId = order.OrderId;
        }
        catch
        {
            await _unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }

        return await GetByIdAsync(userId, orderId, cancellationToken);
    }

    public async Task<RentalOrderDetailResponse> GetByIdAsync(
        Guid userId,
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var detail = await _unitOfWork.RentalOrders.GetOwnedDetailAsync(orderId, userId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy đơn hàng", ErrorCodes.OrderNotFound);

        return MapDetail(detail, _timeProvider.GetUtcNow());
    }

    // ---------------------------------------------------------------------------------------------------------
    // Tính toán dùng chung cho báo giá và tạo đơn
    // ---------------------------------------------------------------------------------------------------------

    private async Task<RentalQuoteComputationResult> ComputeAsync(
        Guid userId,
        RentalPeriodResult period,
        IReadOnlyList<ResolvedRentalLineResult> resolvedLines,
        string? promotionCode,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var pricedLines = await PriceLinesAsync(period, resolvedLines, cancellationToken);
        var subtotal = pricedLines.Sum(line => line.LineAmount);

        var availability = await _availabilityService.EvaluateAsync(period, resolvedLines, cancellationToken);

        var promotion = await _promotionService.EvaluateAsync(
            new PromotionEvaluationInput
            {
                PromotionCode = promotionCode,
                OrderType = OrderTypes.Rental,
                SubtotalAmount = subtotal,
                Lines = pricedLines
                    .Select(line => new PromotionLineInput { Index = line.Index, LineAmount = line.LineAmount })
                    .ToList(),
                TotalQuantity = pricedLines.Sum(line => line.Quantity),
                BillableDays = period.BillableDays,
                CustomerUserId = userId,
                NowUtc = now.UtcDateTime
            },
            cancellationToken);

        var discount = promotion.IsApplied && promotion.ErrorCode is null
            ? Math.Min(promotion.DiscountAmount, subtotal)
            : 0m;
        var total = Math.Max(0m, subtotal - discount);

        var refundPolicy = await _unitOfWork.RefundPolicies.GetCurrentAsync(
            RefundPolicyTypes.Rental,
            RefundPolicyStatuses.Active,
            now.UtcDateTime,
            cancellationToken);
        var holdMinutes = await GetHoldMinutesAsync(cancellationToken);

        // Hệ thống không thu cọc: totalPayable = totalAmount
        return new RentalQuoteComputationResult
        {
            Period = period,
            ResolvedLines = resolvedLines.ToList(),
            PricedLines = pricedLines,
            Availability = availability,
            SubtotalAmount = subtotal,
            Promotion = promotion,
            DiscountAmount = discount,
            TotalAmount = total,
            DepositAmount = 0m,
            TotalPayable = total,
            RefundPolicy = refundPolicy,
            HoldMinutes = holdMinutes
        };
    }

    private async Task<List<PricedRentalLineResult>> PriceLinesAsync(
        RentalPeriodResult period,
        IReadOnlyList<ResolvedRentalLineResult> lines,
        CancellationToken cancellationToken)
    {
        var variantIds = lines.Where(line => line.ProductVariantId.HasValue)
            .Select(line => line.ProductVariantId!.Value).Distinct().ToList();
        var bundleIds = lines.Where(line => line.BundleId.HasValue)
            .Select(line => line.BundleId!.Value).Distinct().ToList();

        var variantTiers = variantIds.Count == 0
            ? new List<RentalPriceTierRow>()
            : await _unitOfWork.RentalPriceTiers.GetForVariantsAsync(variantIds, cancellationToken);
        var bundleTiers = bundleIds.Count == 0
            ? new List<RentalPriceTierRow>()
            : await _unitOfWork.RentalPriceTiers.GetForBundlesAsync(bundleIds, cancellationToken);

        var tiersByVariant = variantTiers
            .Where(tier => tier.VariantId.HasValue)
            .GroupBy(tier => tier.VariantId!.Value)
            .ToDictionary(group => group.Key, group => group.Select(ToTierOption).ToList());
        var tiersByBundle = bundleTiers
            .Where(tier => tier.BundleId.HasValue)
            .GroupBy(tier => tier.BundleId!.Value)
            .ToDictionary(group => group.Key, group => group.Select(ToTierOption).ToList());

        var priced = new List<PricedRentalLineResult>(lines.Count);
        foreach (var line in lines)
        {
            var options = line.ProductVariantId.HasValue
                ? tiersByVariant.GetValueOrDefault(line.ProductVariantId.Value)
                : tiersByBundle.GetValueOrDefault(line.BundleId!.Value);

            var tier = options is null ? null : RentalPricingCalculator.SelectTier(options, period.BillableDays);
            if (tier is null)
            {
                throw new BadRequestException(
                    $"'{line.DisplayName}' chưa có giá cho {period.BillableDays} ngày thuê",
                    ErrorCodes.PriceTierNotFound);
            }

            priced.Add(new PricedRentalLineResult
            {
                Index = line.Index,
                ItemType = line.ItemType,
                ProductVariantId = line.ProductVariantId,
                BundleId = line.BundleId,
                DisplayName = line.DisplayName,
                Quantity = line.Quantity,
                BillableDays = period.BillableDays,
                PriceTierId = tier.PriceTierId,
                TierMinDays = tier.MinDays,
                TierMaxDays = tier.MaxDays,
                DailyPrice = tier.DailyPrice,
                LineAmount = RentalPricingCalculator.CalculateLineAmount(line.Quantity, period.BillableDays, tier.DailyPrice),
                Components = line.Components
            });
        }

        return priced;
    }

    private async Task<int> GetHoldMinutesAsync(CancellationToken cancellationToken)
    {
        var value = await _unitOfWork.SystemConfigurations.GetValueAsync(RentalConfigKeys.HoldMinutes, cancellationToken);
        return int.TryParse(value?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes) && minutes > 0
            ? minutes
            : RentalConfigKeys.DefaultHoldMinutes;
    }

    private static RentalPriceTierOption ToTierOption(RentalPriceTierRow row) => new()
    {
        PriceTierId = row.Id,
        MinDays = row.MinDays,
        MaxDays = row.MaxDays,
        DailyPrice = row.DailyPrice
    };

    private static List<RentalLineInput> ToLineInputs(IEnumerable<RentalItemRequest>? items)
        => (items ?? [])
            .Select(item => new RentalLineInput
            {
                ProductVariantId = item.ProductVariantId,
                BundleId = item.BundleId,
                Quantity = item.Quantity
            })
            .ToList();

    // ---------------------------------------------------------------------------------------------------------
    // Báo giá
    // ---------------------------------------------------------------------------------------------------------

    private RentalQuoteResponse MapQuote(RentalQuoteComputationResult computation)
    {
        var availabilityByIndex = computation.Availability.ToDictionary(line => line.LineIndex);

        var response = new RentalQuoteResponse
        {
            RentalStartAt = computation.Period.StartUtc,
            RentalEndAt = computation.Period.EndUtc,
            BillableDays = computation.Period.BillableDays,
            Lines = computation.PricedLines.Select(line =>
            {
                var lineAvailability = availabilityByIndex.GetValueOrDefault(line.Index);
                var message = lineAvailability is null || lineAvailability.IsAvailable
                    ? null
                    : _availabilityService.BuildLineMessage(lineAvailability, computation.Period);

                return new RentalQuoteLineResponse
                {
                    Index = line.Index,
                    ItemType = line.ItemType,
                    ProductVariantId = line.ProductVariantId,
                    BundleId = line.BundleId,
                    DisplayName = line.DisplayName,
                    Quantity = line.Quantity,
                    DailyPrice = line.DailyPrice,
                    AppliedTier = new RentalQuoteTierResponse { MinDays = line.TierMinDays, MaxDays = line.TierMaxDays },
                    LineAmount = line.LineAmount,
                    IsAvailable = lineAvailability?.IsAvailable ?? false,
                    AvailabilityMessage = string.IsNullOrEmpty(message) ? null : message,
                    Components = line.ItemType == RentalItemTypes.Bundle
                        ? line.Components.Select(component => new RentalQuoteComponentResponse
                        {
                            ProductVariantId = component.ProductVariantId,
                            DisplayName = component.VariantName,
                            QuantityPerBundle = component.QuantityPerUnit,
                            TotalQuantity = component.TotalQuantity
                        }).ToList()
                        : []
                };
            }).ToList(),
            SubtotalAmount = computation.SubtotalAmount,
            DiscountAmount = computation.DiscountAmount,
            TotalAmount = computation.TotalAmount,
            DepositAmount = computation.DepositAmount,
            TotalPayable = computation.TotalPayable,
            IsAllAvailable = computation.IsAllAvailable,
            HoldMinutes = computation.HoldMinutes,
            RefundPolicy = computation.RefundPolicy is null
                ? null
                : new RentalQuoteRefundPolicyResponse
                {
                    RefundPolicyId = computation.RefundPolicy.RefundPolicyId,
                    Name = computation.RefundPolicy.Name,
                    Version = computation.RefundPolicy.Version,
                    Description = computation.RefundPolicy.Description,
                    Rules = computation.RefundPolicy.RefundPolicyRules
                        .OrderByDescending(rule => rule.MinHoursBefore)
                        .Select(rule => new RentalQuoteRefundRuleResponse
                        {
                            MinHoursBefore = rule.MinHoursBefore,
                            RefundPercentage = rule.RefundPercentage
                        }).ToList()
                }
        };

        var promotion = computation.Promotion;
        if (promotion.ErrorCode is not null)
        {
            response.PromotionError = new RentalQuotePromotionErrorResponse
            {
                Code = promotion.ErrorCode,
                Message = promotion.Message ?? string.Empty
            };
        }
        else if (promotion.IsApplied)
        {
            response.Promotion = new RentalQuotePromotionResponse
            {
                Code = promotion.Code ?? string.Empty,
                Name = promotion.Name ?? string.Empty,
                DiscountAmount = computation.DiscountAmount,
                Description = promotion.Description
            };
        }

        return response;
    }

    // ---------------------------------------------------------------------------------------------------------
    // Tạo đơn
    // ---------------------------------------------------------------------------------------------------------

    private void EnsureAcceptable(RentalQuoteComputationResult computation, int acceptedRefundPolicyVersion)
    {
        if (!computation.IsAllAvailable)
        {
            var errors = new Dictionary<string, string[]>();
            foreach (var line in computation.Availability.Where(line => !line.IsAvailable))
            {
                var message = _availabilityService.BuildLineMessage(line, computation.Period);
                errors[$"items[{line.LineIndex}].quantity"] =
                    [string.IsNullOrEmpty(message) ? "Thiết bị không còn đủ trong khoảng thời gian đã chọn" : message];
            }

            throw new ConflictException(
                "Thiết bị không còn đủ trong khoảng thời gian đã chọn",
                ErrorCodes.ResourceUnavailable,
                errors);
        }

        if (computation.Promotion.ErrorCode is { } promotionErrorCode)
        {
            var message = computation.Promotion.Message ?? "Mã khuyến mãi không áp dụng được";
            throw new ConflictException(
                message,
                promotionErrorCode,
                new Dictionary<string, string[]> { ["promotionCode"] = [message] });
        }

        if (computation.RefundPolicy is null)
        {
            throw new ConflictException(
                "Chưa có chính sách hoàn tiền thuê thiết bị đang hiệu lực",
                ErrorCodes.RefundPolicyChanged);
        }

        if (computation.RefundPolicy.Version != acceptedRefundPolicyVersion)
        {
            throw new ConflictException(
                "Chính sách hoàn tiền đã được cập nhật, vui lòng xem lại và đồng ý với phiên bản mới",
                ErrorCodes.RefundPolicyChanged,
                new Dictionary<string, string[]>
                {
                    ["acceptedRefundPolicyVersion"] = [$"Phiên bản hiện hành là {computation.RefundPolicy.Version}"]
                });
        }
    }

    private static Order BuildOrder(
        Guid userId,
        CreateRentalOrderRequest request,
        string idempotencyKey,
        string orderCode,
        RentalQuoteComputationResult computation,
        IReadOnlyDictionary<Guid, List<Guid>> assetsByVariant,
        DateTimeOffset now)
    {
        var utcNow = now.UtcDateTime;
        var orderId = Guid.NewGuid();
        var period = computation.Period;

        var order = new Order
        {
            OrderId = orderId,
            CustomerUserId = userId,
            OrderType = OrderTypes.Rental,
            OrderCode = orderCode,
            SubtotalAmount = computation.SubtotalAmount,
            DiscountAmount = computation.DiscountAmount,
            TotalAmount = computation.TotalAmount,
            Status = OrderStatuses.PendingPayment,
            PaymentStatus = OrderPaymentStatuses.Unpaid,
            HoldExpiresAt = now.AddMinutes(computation.HoldMinutes),
            RefundPolicyId = computation.RefundPolicy!.RefundPolicyId,
            Note = BuildNote(request.Note, request.Pickup),
            IdempotencyKey = idempotencyKey,
            CreatedAt = now,
            UpdatedAt = now
        };

        var rentalOrder = new RentalOrder
        {
            OrderId = orderId,
            RentalStartAt = period.StartUtc,
            RentalEndAt = period.EndUtc,
            DepositAmount = 0m,
            Order = order
        };
        order.RentalOrder = rentalOrder;

        var itemsByLineIndex = new Dictionary<int, RentalOrderItem>();
        var assetCursors = assetsByVariant.Keys.ToDictionary(variantId => variantId, _ => 0);

        foreach (var line in computation.PricedLines.OrderBy(line => line.Index))
        {
            var itemId = Guid.NewGuid();
            var item = new RentalOrderItem
            {
                RentalOrderItemId = itemId,
                RentalOrderId = orderId,
                ProductVariantId = line.ProductVariantId,
                BundleId = line.BundleId,
                Quantity = line.Quantity,
                BillableDays = line.BillableDays,
                DailyPrice = line.DailyPrice,
                LineAmount = line.LineAmount,
                // Lệch nhau vài tick để thứ tự dòng (CreatedAt, id) khi đọc chi tiết đúng thứ tự giỏ
                CreatedAt = utcNow.AddTicks(line.Index * 10L),
                UpdatedAt = utcNow,
                RentalOrder = rentalOrder
            };
            itemsByLineIndex[line.Index] = item;
            rentalOrder.RentalOrderItems.Add(item);

            foreach (var component in line.Components)
            {
                // Chốt thành phần combo tại thời điểm đặt
                if (line.BundleId.HasValue)
                {
                    item.RentalBundleItemSnapshots.Add(new RentalBundleItemSnapshot
                    {
                        RentalBundleItemSnapshotId = Guid.NewGuid(),
                        RentalOrderItemId = itemId,
                        ProductVariantId = component.ProductVariantId,
                        QuantityPerBundle = component.QuantityPerUnit,
                        CreatedAt = utcNow,
                        RentalOrderItem = item
                    });
                }

                // Chia tài sản đã chọn (theo biến thể) cho từng dòng và thành phần theo thứ tự xác định; dòng combo gắn vào dòng của combo
                if (!assetsByVariant.TryGetValue(component.ProductVariantId, out var assetIds)
                    || assetCursors[component.ProductVariantId] + component.TotalQuantity > assetIds.Count)
                {
                    throw new ConflictException(
                        "Thiết bị không còn đủ trong khoảng thời gian đã chọn",
                        ErrorCodes.ResourceUnavailable);
                }

                for (var i = 0; i < component.TotalQuantity; i++)
                {
                    item.AssetAssignments.Add(new AssetAssignment
                    {
                        AssetAssignmentId = Guid.NewGuid(),
                        RentalOrderItemId = itemId,
                        EquipmentAssetId = assetIds[assetCursors[component.ProductVariantId] + i],
                        Status = AssignmentStatuses.Reserved,
                        AssignedAt = utcNow,
                        ReservedFrom = period.StartUtc,
                        ReservedUntil = period.EndUtc,
                        CreatedAt = utcNow,
                        RentalOrderItem = item
                    });
                }

                assetCursors[component.ProductVariantId] += component.TotalQuantity;
            }
        }

        AddAppliedPromotions(order, computation, itemsByLineIndex, utcNow);
        return order;
    }

    // Một dòng cấp đơn (tổng mức giảm) và, với mã áp theo dòng, thêm một dòng cho mỗi dòng thuê được giảm.
    // Status = Applied: tính vào lượt dùng ngay khi tạo đơn.
    private static void AddAppliedPromotions(
        Order order,
        RentalQuoteComputationResult computation,
        IReadOnlyDictionary<int, RentalOrderItem> itemsByLineIndex,
        DateTime utcNow)
    {
        var promotion = computation.Promotion;
        if (!promotion.IsApplied || promotion.ErrorCode is not null || promotion.TierId is null)
            return;

        order.AppliedPromotions.Add(new AppliedPromotion
        {
            AppliedPromotionId = Guid.NewGuid(),
            PromotionTierId = promotion.TierId.Value,
            OrderId = order.OrderId,
            RentalOrderItemId = null,
            DiscountAmount = computation.DiscountAmount,
            Status = AppliedPromotionStatuses.Applied,
            AppliedAt = utcNow,
            CreatedAt = utcNow
        });

        if (!promotion.IsLineScope)
            return;

        foreach (var lineDiscount in promotion.LineDiscounts.Where(discount => discount.Amount > 0))
        {
            if (!itemsByLineIndex.TryGetValue(lineDiscount.LineIndex, out var item))
                continue;

            order.AppliedPromotions.Add(new AppliedPromotion
            {
                AppliedPromotionId = Guid.NewGuid(),
                PromotionTierId = promotion.TierId.Value,
                OrderId = order.OrderId,
                RentalOrderItemId = item.RentalOrderItemId,
                RentalOrderItem = item,
                DiscountAmount = lineDiscount.Amount,
                Status = AppliedPromotionStatuses.Applied,
                AppliedAt = utcNow,
                CreatedAt = utcNow
            });
        }
    }

    // Mã đơn theo định dạng chung của hệ thống (RT-yyyyMMdd-...), kiểm tra trùng trước khi dùng
    private async Task<string> GenerateOrderCodeAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < OrderCodeAttempts; attempt++)
        {
            var code = $"RT-{now:yyyyMMdd}-{Guid.NewGuid():N}"[..32].ToUpperInvariant();
            if (!await _unitOfWork.RentalOrders.OrderCodeExistsAsync(code, cancellationToken))
                return code;
        }

        throw new InvalidOperationException("Không sinh được mã đơn hàng duy nhất");
    }

    // orders.note = ghi chú của khách + dòng "Nhận đồ: ..." (thông tin nhận đồ chưa có cột riêng)
    private static string? BuildNote(string? note, RentalPickupRequest? pickup)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(note))
            parts.Add(note.Trim());

        if (pickup is not null)
        {
            var receiver = string.Join(' ', new[] { pickup.ReceiverName?.Trim(), pickup.ReceiverPhone?.Trim() }
                .Where(text => !string.IsNullOrEmpty(text)));
            var method = string.IsNullOrWhiteSpace(pickup.Method) ? null : $"({pickup.Method.Trim()})";
            var pickupLine = string.Join(' ', new[] { receiver, method }.Where(text => !string.IsNullOrEmpty(text)));
            if (pickupLine.Length > 0)
                parts.Add($"Nhận đồ: {pickupLine}");
        }

        if (parts.Count == 0)
            return null;

        var result = string.Join('\n', parts);
        return result.Length > MaxNoteLength ? result[..MaxNoteLength] : result;
    }

    // ---------------------------------------------------------------------------------------------------------
    // Idempotency
    // ---------------------------------------------------------------------------------------------------------

    // Cùng Idempotency-Key chỉ được dùng lại cho cùng nội dung: thời gian thuê, các dòng, mã khuyến mãi, ghi chú + nhận đồ, phiên bản chính sách
    private async Task EnsureIdempotentRequestMatchesAsync(
        Order existing,
        CreateRentalOrderRequest request,
        CancellationToken cancellationToken)
    {
        var rental = existing.RentalOrder;
        var sameRequest = rental is not null
            && IsSameInstant(rental.RentalStartAt, request.RentalStartAt)
            && IsSameInstant(rental.RentalEndAt, request.RentalEndAt)
            && NormalizeItems(rental.RentalOrderItems.Select(item => (item.ProductVariantId, item.BundleId, item.Quantity)))
                .SequenceEqual(NormalizeItems((request.Items ?? []).Select(item => (item.ProductVariantId, item.BundleId, item.Quantity))))
            && string.Equals(existing.Note?.Trim(), BuildNote(request.Note, request.Pickup), StringComparison.Ordinal)
            && await PromotionMatchesAsync(existing, request.PromotionCode, cancellationToken)
            && await RefundPolicyVersionMatchesAsync(existing, request.AcceptedRefundPolicyVersion, cancellationToken);

        if (!sameRequest)
        {
            throw new ConflictException(
                "Idempotency-Key đã được dùng cho một yêu cầu khác",
                ErrorCodes.IdempotencyKeyReused);
        }
    }

    private async Task<bool> PromotionMatchesAsync(Order existing, string? promotionCode, CancellationToken cancellationToken)
    {
        var code = promotionCode?.Trim();
        if (string.IsNullOrEmpty(code))
            return existing.AppliedPromotions.Count == 0;

        // Mã được so không phân biệt hoa thường qua bậc khuyến mãi đã áp dụng cho đơn
        var promotion = await _unitOfWork.Promotions.GetByCodeAsync(code, cancellationToken);
        if (promotion is null || existing.AppliedPromotions.Count == 0)
            return false;

        var tierIds = promotion.PromotionTiers.Select(tier => tier.PromotionTierId).ToHashSet();
        return existing.AppliedPromotions.All(applied => tierIds.Contains(applied.PromotionTierId));
    }

    private async Task<bool> RefundPolicyVersionMatchesAsync(Order existing, int acceptedVersion, CancellationToken cancellationToken)
    {
        var policy = await _unitOfWork.RefundPolicies.GetByIdAsync(existing.RefundPolicyId, cancellationToken);
        return policy is not null && policy.Version == acceptedVersion;
    }

    private static List<string> NormalizeItems(IEnumerable<(Guid? ProductVariantId, Guid? BundleId, int Quantity)> items)
        => items
            .Select(item => $"{item.ProductVariantId ?? Guid.Empty:N}|{item.BundleId ?? Guid.Empty:N}|{item.Quantity}")
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToList();

    // Thời điểm lưu trong DB có độ chính xác micro giây nên so sánh với sai số 1 micro giây
    private static bool IsSameInstant(DateTime stored, DateTime requested)
        => Math.Abs((RentalPeriodCalculator.ToUtc(stored) - RentalPeriodCalculator.ToUtc(requested)).Ticks) <= 10;

    // ---------------------------------------------------------------------------------------------------------
    // Chi tiết đơn
    // ---------------------------------------------------------------------------------------------------------

    private static RentalOrderDetailResponse MapDetail(RentalOrderDetailRow detail, DateTimeOffset now)
    {
        var visibleAssignments = detail.Assignments
            .Where(assignment => VisibleAssignmentStatuses.Contains(assignment.Status) && assignment.RentalOrderItemId.HasValue)
            .ToList();

        // Phí nháp của nhân viên không hiển thị cho khách
        var charges = detail.Charges
            .Where(charge => !string.Equals(charge.Status, ChargeStatusDraft, StringComparison.OrdinalIgnoreCase))
            .Select(charge => new RentalOrderChargeResponse
            {
                Id = charge.Id,
                ChargeType = charge.ChargeType,
                Amount = charge.Amount,
                Reason = charge.Reason,
                AssetCode = charge.AssetCode,
                Status = charge.Status,
                CreatedAt = RentalPeriodCalculator.ToUtc(charge.CreatedAt)
            })
            .ToList();

        var payments = detail.Payments.Select(payment => new RentalOrderPaymentResponse
        {
            Id = payment.Id,
            PaymentType = payment.PaymentType,
            Purpose = payment.Purpose,
            Amount = payment.Amount,
            PaymentMethod = payment.PaymentMethod,
            Status = payment.Status,
            CreatedAt = payment.CreatedAt.UtcDateTime,
            ProcessedAt = payment.ProcessedAt?.UtcDateTime
        }).ToList();

        var paidAmount = detail.Payments
            .Where(payment => payment.PaymentType == PaymentTypeCollection && payment.Status == PaymentStatusSucceeded)
            .Sum(payment => payment.Amount);
        var refundedAmount = detail.Payments
            .Where(payment => payment.PaymentType == PaymentTypeRefund && payment.Status == PaymentStatusSucceeded)
            .Sum(payment => payment.Amount);

        var hasDeliveredAsset = visibleAssignments.Count > 0;
        var statusAllowsCancel = detail.Status is OrderStatuses.PendingPayment or OrderStatuses.Confirmed;
        var canCancel = statusAllowsCancel && !hasDeliveredAsset;
        var canPay = detail.Status == OrderStatuses.PendingPayment
                     && detail.PaymentStatus == OrderPaymentStatuses.Unpaid
                     && (detail.HoldExpiresAt is null || detail.HoldExpiresAt > now);

        string? cancelBlockedReason = null;
        if (!canCancel)
        {
            cancelBlockedReason = hasDeliveredAsset
                ? "Không thể hủy sau khi đã giao thiết bị"
                : "Đơn ở trạng thái hiện tại không thể hủy";
        }

        return new RentalOrderDetailResponse
        {
            Id = detail.OrderId,
            OrderCode = detail.OrderCode,
            OrderType = detail.OrderType,
            Status = detail.Status,
            PaymentStatus = detail.PaymentStatus,
            CreatedAt = detail.CreatedAt.UtcDateTime,
            HoldExpiresAt = detail.HoldExpiresAt?.UtcDateTime,
            RentalStartAt = RentalPeriodCalculator.ToUtc(detail.RentalStartAt),
            RentalEndAt = RentalPeriodCalculator.ToUtc(detail.RentalEndAt),
            BillableDays = RentalPeriodCalculator.CalculateBillableDays(
                RentalPeriodCalculator.ToUtc(detail.RentalStartAt),
                RentalPeriodCalculator.ToUtc(detail.RentalEndAt)),
            Note = detail.Note,
            CancelledAt = detail.CancelledAt?.UtcDateTime,
            CancelReason = detail.CancelReason,
            HandledBy = detail.HandledByName is null
                ? null
                : new RentalOrderHandledByResponse { FullName = detail.HandledByName },
            RefundPolicy = new RentalOrderRefundPolicyResponse
            {
                RefundPolicyId = detail.RefundPolicyId,
                Name = detail.RefundPolicyName,
                Version = detail.RefundPolicyVersion
            },
            Lines = detail.Lines.Select(MapLine).ToList(),
            Assets = visibleAssignments.Select(assignment => new RentalOrderAssetResponse
            {
                AssetCode = assignment.AssetCode,
                DisplayName = $"{assignment.ProductName} — {assignment.VariantName}",
                RentalOrderItemId = assignment.RentalOrderItemId!.Value,
                Status = assignment.Status,
                AssignedAt = RentalPeriodCalculator.ToUtc(assignment.AssignedAt),
                ReturnedAt = assignment.ReturnedAt is null ? null : RentalPeriodCalculator.ToUtc(assignment.ReturnedAt.Value),
                ReturnCondition = assignment.ReturnCondition
            }).ToList(),
            Amounts = new RentalOrderAmountsResponse
            {
                SubtotalAmount = detail.SubtotalAmount,
                DiscountAmount = detail.DiscountAmount,
                TotalAmount = detail.TotalAmount,
                DepositAmount = 0m,
                TotalPayable = detail.TotalAmount,
                PaidAmount = paidAmount,
                RefundedAmount = refundedAmount
            },
            Charges = charges,
            Payments = payments,
            Timeline = BuildTimeline(detail, visibleAssignments),
            AllowedActions = new RentalOrderAllowedActionsResponse
            {
                CanPay = canPay,
                CanCancel = canCancel,
                CancelBlockedReason = cancelBlockedReason
            }
        };
    }

    private static RentalOrderLineResponse MapLine(RentalOrderLineRow line)
    {
        var isBundle = line.BundleId.HasValue;
        return new RentalOrderLineResponse
        {
            Id = line.ItemId,
            ItemType = isBundle ? RentalItemTypes.Bundle : RentalItemTypes.Variant,
            DisplayName = isBundle
                ? line.BundleName ?? string.Empty
                : $"{line.ProductName} — {line.VariantName}",
            Sku = line.Sku,
            Quantity = line.Quantity,
            DailyPrice = line.DailyPrice,
            BillableDays = line.BillableDays,
            LineAmount = line.LineAmount,
            BundleComponents = isBundle
                ? line.Components.Select(component => new RentalOrderBundleComponentResponse
                {
                    VariantName = $"{component.ProductName} — {component.VariantName}",
                    QuantityPerBundle = component.QuantityPerBundle
                }).ToList()
                : []
        };
    }

    // Dòng thời gian dẫn xuất từ đơn, thanh toán và phân bổ tài sản
    private static List<RentalOrderTimelineEventResponse> BuildTimeline(
        RentalOrderDetailRow detail,
        IReadOnlyList<RentalOrderAssignmentRow> visibleAssignments)
    {
        var events = new List<RentalOrderTimelineEventResponse>
        {
            new() { Event = "Created", At = detail.CreatedAt.UtcDateTime }
        };

        var firstPaidAt = detail.Payments
            .Where(payment => payment.PaymentType == PaymentTypeCollection
                              && payment.Status == PaymentStatusSucceeded
                              && payment.ProcessedAt is not null)
            .Select(payment => payment.ProcessedAt)
            .Min();
        if (firstPaidAt is not null)
            events.Add(new RentalOrderTimelineEventResponse { Event = "Paid", At = firstPaidAt.Value.UtcDateTime });

        if (visibleAssignments.Count > 0)
        {
            events.Add(new RentalOrderTimelineEventResponse
            {
                Event = "Issued",
                At = RentalPeriodCalculator.ToUtc(visibleAssignments.Min(assignment => assignment.AssignedAt))
            });
        }

        var returnedTimes = visibleAssignments
            .Where(assignment => assignment.ReturnedAt is not null)
            .Select(assignment => assignment.ReturnedAt!.Value)
            .ToList();
        if (returnedTimes.Count > 0)
        {
            events.Add(new RentalOrderTimelineEventResponse
            {
                Event = "Returned",
                At = RentalPeriodCalculator.ToUtc(returnedTimes.Max())
            });
        }

        if (detail.CancelledAt is not null)
            events.Add(new RentalOrderTimelineEventResponse { Event = "Cancelled", At = detail.CancelledAt.Value.UtcDateTime });

        return events.OrderBy(timelineEvent => timelineEvent.At).ToList();
    }
}
