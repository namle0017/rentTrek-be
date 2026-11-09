using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Tour.Responses;
using RenTrek.DAL.Entities;

namespace RenTrek.BLL.Mappings;

/// <summary>
/// Khai báo ánh xạ AutoMapper cho tour.
/// </summary>
public class TourProfile : AutoMapper.Profile
{
    public TourProfile()
    {
        CreateMap<TourImage, TourImageResponse>();

        CreateMap<TourActivity, TourActivityResponse>();

        CreateMap<TourPreparationItem, TourItemResponse>();

        CreateMap<TourProvidedItem, TourItemResponse>();

        CreateMap<Trip, TripSummaryResponse>()
            .ForMember(d => d.Id, opt => opt.MapFrom(s => s.TripId))
            .ForMember(d => d.StartAt, opt => opt.MapFrom(s => s.StartAt.UtcDateTime))
            .ForMember(d => d.EndAt, opt => opt.MapFrom(s => s.EndAt.UtcDateTime));

        CreateMap<Tour, TourResponse>()
            .ForMember(d => d.CategoryName, opt => opt.MapFrom(s => s.Category.Name))
            .ForMember(d => d.LocationName, opt => opt.MapFrom(s => s.MainLocation.Name))
            .ForMember(d => d.ThumbnailUrl, opt => opt.MapFrom(s =>
                s.Images.Where(i => i.IsPrimary).Select(i => i.ImageUrl).FirstOrDefault()
                ?? s.Images.OrderBy(i => i.DisplayOrder).Select(i => i.ImageUrl).FirstOrDefault()))
            .ForMember(d => d.BadgeStatus, opt => opt.MapFrom(s =>
                s.Status == TourStatuses.Expired ? "Hết hạn" : "Đang mở"))
            .ForMember(d => d.DepartureDateRange, opt => opt.MapFrom(s =>
                s.Trips.OrderBy(t => t.StartAt)
                    .Select(t => $"{t.StartAt:dd} – {t.EndAt:dd/MM/yyyy}")
                    .FirstOrDefault()))
            .ForMember(d => d.SlotStatusText, opt => opt.MapFrom(s =>
                GetSlotStatusText(s)))
            .ForMember(d => d.AvailableSlots, opt => opt.MapFrom(s =>
                s.Trips.Where(t => t.StartAt > DateTime.UtcNow && t.Status == TripStatuses.Open)
                    .OrderBy(t => t.StartAt)
                    .Select(t => (int?)Math.Max(0, t.TotalSlots - t.BookedSlots))
                    .FirstOrDefault()))
            .ForMember(d => d.NextDepartureDate, opt => opt.MapFrom(s =>
                s.Trips.Where(t => t.StartAt > DateTime.UtcNow && t.Status == TripStatuses.Open)
                    .OrderBy(t => t.StartAt)
                    .Select(t => (DateTime?)t.StartAt.UtcDateTime)
                    .FirstOrDefault()));

        CreateMap<Tour, TourDetailResponse>()
            .ForMember(d => d.CategoryName, opt => opt.MapFrom(s => s.Category.Name))
            .ForMember(d => d.LocationName, opt => opt.MapFrom(s => s.MainLocation.Name))
            .ForMember(d => d.LocationAddress, opt => opt.MapFrom(s => s.MainLocation.Address))
            .ForMember(d => d.Latitude, opt => opt.MapFrom(s => s.MainLocation.Latitude))
            .ForMember(d => d.Longitude, opt => opt.MapFrom(s => s.MainLocation.Longitude))
            .ForMember(d => d.Images, opt => opt.MapFrom(s => s.Images.OrderBy(i => i.DisplayOrder)))
            .ForMember(d => d.Activities, opt => opt.MapFrom(s => s.Activities.OrderBy(a => a.DayNumber).ThenBy(a => a.DisplayOrder)))
            .ForMember(d => d.AvailableTrips, opt => opt.MapFrom(s => s.Trips
                .Where(t => t.Status != TripStatuses.Draft && t.Status != TripStatuses.Cancelled)
                .OrderBy(t => t.StartAt)))
            .ForMember(d => d.RelatedPosts, opt => opt.MapFrom(s => s.TourPosts
                .Where(tp => !tp.Post.IsDeleted && tp.Post.Status == PostStatuses.Published)
                .OrderBy(tp => tp.DisplayOrder)
                .Select(tp => new TourRelatedPostResponse
                {
                    PostId = tp.Post.PostId,
                    Title = tp.Post.Title,
                    Slug = tp.Post.Slug,
                    PostType = tp.Post.PostType,
                    PublishedAt = tp.Post.PublishedAt
                })));
    }

    private static string GetSlotStatusText(Tour tour)
    {
        if (tour.Status == TourStatuses.Expired)
        {
            var trip = tour.Trips.OrderByDescending(t => t.EndAt).FirstOrDefault();
            if (trip != null && trip.EndAt < DateTime.UtcNow)
                return "Đã kết thúc";
            return "Đã khởi hành";
        }

        var openTrip = tour.Trips
            .Where(t => t.StartAt > DateTime.UtcNow && t.Status == TripStatuses.Open)
            .OrderBy(t => t.StartAt)
            .FirstOrDefault();

        if (openTrip == null)
            return "Đã hết chỗ";

        var remaining = Math.Max(0, openTrip.TotalSlots - openTrip.BookedSlots);
        return remaining > 0 ? $"Còn {remaining} chỗ" : "Đã hết chỗ";
    }
}
