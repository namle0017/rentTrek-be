using AutoMapper;
using RenTrek.BLL.DTOs.Warehouse.Responses;
using RenTrek.DAL.Entities;

namespace RenTrek.BLL.Mappings;

public class AssetProfile : AutoMapper.Profile
{
    public AssetProfile()
    {
        CreateMap<EquipmentAsset, AssetResponse>()
            .ForMember(dest => dest.AssetId, opt => opt.MapFrom(src => src.EquipmentAssetId))
            .ForMember(dest => dest.ProductVariantId, opt => opt.MapFrom(src => src.ProductVariantId))
            .ForMember(dest => dest.Sku, opt => opt.MapFrom(src => src.ProductVariant.Sku))
            .ForMember(dest => dest.ProductName, opt => opt.MapFrom(src => src.ProductVariant.Product.Name))
            .ForMember(dest => dest.VariantName, opt => opt.MapFrom(src => src.ProductVariant.Name))
            .ForMember(dest => dest.CurrentAssignment, opt => opt.MapFrom(src =>
                src.AssetAssignments
                    .Where(x => x.Status == "Issued" || x.Status == "Reserved")
                    .OrderByDescending(x => x.ReservedFrom)
                    .Select(x => new AssetCurrentAssignmentResponse
                    {
                        AssignmentId = x.AssetAssignmentId,
                        Status = x.Status,
                        SourceType = x.RentalOrderItemId.HasValue ? "RentalOrder" :
                                     x.TripProvidedItemId.HasValue ? "TripProvided" :
                                     x.AppliedPromotionId.HasValue ? "Promotion" : "Other",
                        SourceLabel = x.RentalOrderItem != null && x.RentalOrderItem.RentalOrder != null && x.RentalOrderItem.RentalOrder.Order != null
                                        ? x.RentalOrderItem.RentalOrder.Order.OrderCode
                                        : x.TripProvidedItem != null && x.TripProvidedItem.Trip != null && x.TripProvidedItem.Trip.Tour != null
                                            ? $"{x.TripProvidedItem.Trip.Tour.Name} ({x.TripProvidedItem.Trip.StartAt:dd/MM})"
                                            : x.AppliedPromotion != null && x.AppliedPromotion.PromotionTier != null && x.AppliedPromotion.PromotionTier.Promotion != null
                                                ? x.AppliedPromotion.PromotionTier.Promotion.Name
                                                : "Phân bổ hệ thống",
                        ReservedFrom = x.ReservedFrom,
                        ReservedUntil = x.ReservedUntil
                    }).FirstOrDefault()));

        CreateMap<EquipmentAsset, AssetDetailResponse>()
            .IncludeBase<EquipmentAsset, AssetResponse>()
            .ForMember(dest => dest.RecentAssignments, opt => opt.MapFrom(src =>
                src.AssetAssignments
                    .OrderByDescending(x => x.CreatedAt)
                    .Take(10)
                    .Select(x => new AssetAssignmentHistoryResponse
                    {
                        AssignmentId = x.AssetAssignmentId,
                        SourceType = x.RentalOrderItemId.HasValue ? "RentalOrder" :
                                     x.TripProvidedItemId.HasValue ? "TripProvided" :
                                     x.AppliedPromotionId.HasValue ? "Promotion" : "Other",
                        SourceLabel = x.RentalOrderItem != null && x.RentalOrderItem.RentalOrder != null && x.RentalOrderItem.RentalOrder.Order != null
                                        ? x.RentalOrderItem.RentalOrder.Order.OrderCode
                                        : x.TripProvidedItem != null && x.TripProvidedItem.Trip != null && x.TripProvidedItem.Trip.Tour != null
                                            ? $"{x.TripProvidedItem.Trip.Tour.Name} ({x.TripProvidedItem.Trip.StartAt:dd/MM})"
                                            : x.AppliedPromotion != null && x.AppliedPromotion.PromotionTier != null && x.AppliedPromotion.PromotionTier.Promotion != null
                                                ? x.AppliedPromotion.PromotionTier.Promotion.Name
                                                : "Phân bổ hệ thống",
                        ReservedFrom = x.ReservedFrom,
                        ReservedUntil = x.ReservedUntil,
                        AssignedAt = x.AssignedAt,
                        ReturnedAt = x.ReturnedAt,
                        ReturnCondition = x.ReturnCondition,
                        Status = x.Status
                    }).ToList()))
            .ForMember(dest => dest.RecentCharges, opt => opt.MapFrom(src =>
                src.RentalCharges
                    .OrderByDescending(c => c.CreatedAt)
                    .Take(10)
                    .Select(c => new AssetRentalChargeResponse
                    {
                        RentalChargeId = c.RentalChargeId,
                        RentalOrderId = c.RentalOrderId,
                        ChargeType = c.ChargeType,
                        Amount = c.Amount,
                        Reason = c.Reason,
                        Status = c.Status,
                        CreatedAt = c.CreatedAt
                    }).ToList()));
    }
}
