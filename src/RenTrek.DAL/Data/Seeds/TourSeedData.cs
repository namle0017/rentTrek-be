using Microsoft.EntityFrameworkCore;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Seeds;

public static class TourSeedData
{
    public static readonly Guid FansipanTourId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static async Task InitializeAsync(AppDbContext context)
    {
        // Kiểm tra nếu đã có dữ liệu thì không seed lại
        if (await context.Tours.AnyAsync())
            return;

        context.ChangeTracker.Clear();

        // 1. Danh mục
        var catCampingTrekking = new Category
        {
            Name = "Camping + Trekking",
            CategoryType = "TOUR",
            Description = "Cung đường kết hợp leo núi và cắm trại ngắm thiên nhiên",
            Status = "Active"
        };
        var catTrekking = new Category
        {
            Name = "Trekking & Khám Phá",
            CategoryType = "TOUR",
            Description = "Chinh phục các đỉnh núi và cung đường rừng hoang sơ",
            Status = "Active"
        };

        await context.Categories.AddRangeAsync(catCampingTrekking, catTrekking);

        // 2. Địa điểm (Chuẩn theo các thẻ trên ảnh 1 & 2)
        var locSapa = new Location
        {
            Name = "Sa Pa",
            Address = "Thị xã Sa Pa, Tỉnh Lào Cai",
            Latitude = 22.3033m,
            Longitude = 103.7756m
        };
        var locTaNang = new Location
        {
            Name = "Lâm Đồng – Bình Thuận",
            Address = "Cung đường Tà Năng – Phan Dũng",
            Latitude = 11.5833m,
            Longitude = 108.4500m
        };
        var locLaoThan = new Location
        {
            Name = "Bát Xát, Lào Cai",
            Address = "Xã Y Tý, Huyện Bát Xát, Tỉnh Lào Cai",
            Latitude = 22.6167m,
            Longitude = 103.6000m
        };
        var locBidoup = new Location
        {
            Name = "Lạc Dương, Lâm Đồng",
            Address = "Vườn quốc gia Bidoup Núi Bà, Huyện Lạc Dương, Tỉnh Lâm Đồng",
            Latitude = 12.1833m,
            Longitude = 108.6667m
        };
        var locPuLuong = new Location
        {
            Name = "Bá Thước, Thanh Hóa",
            Address = "Khu bảo tồn thiên nhiên Pù Luông, Bá Thước, Thanh Hóa",
            Latitude = 20.4833m,
            Longitude = 105.1833m
        };
        var locNuiChua = new Location
        {
            Name = "Ninh Hải, Ninh Thuận",
            Address = "Vườn quốc gia Núi Chúa, Ninh Hải, Tỉnh Ninh Thuận",
            Latitude = 11.7000m,
            Longitude = 109.1500m
        };
        var locBachMoc = new Location
        {
            Name = "Lai Châu",
            Address = "Đỉnh Ky Quan San (Bạch Mộc Lương Tử), Phong Thổ, Lai Châu",
            Latitude = 22.5000m,
            Longitude = 103.5833m
        };

        await context.Locations.AddRangeAsync(locSapa, locTaNang, locLaoThan, locBidoup, locPuLuong, locNuiChua, locBachMoc);
        await context.SaveChangesAsync();

        // 3. Tour Chi tiết (Fansipan 2N1Đ - Chuẩn 100% Ảnh 2)
        var tourFansipan = new Tour
        {
            Id = FansipanTourId,
            Name = "Tour Fansipan 2N1Đ - Chinh Phục Nóc Nhà Đông Dương",
            Description = "Hành trình chinh phục đỉnh Fansipan 3.143m theo tuyến Trạm Tôn. Trải nghiệm rừng Hoàng Liên Sơn nguyên sinh, săn biển mây bồng bềnh và ngắm bình minh kỳ vĩ.",
            DifficultyLevel = "Thử thách",
            DurationDays = 2,
            DurationNights = 1,
            DurationMinutes = 2880,
            BasePrice = 2450000m,
            EquipmentDeposit = 500000m,
            InspectionRate = 96,
            Status = "Active",
            Category = catCampingTrekking,
            MainLocation = locSapa,
            Images =
            [
                new TourImage
                {
                    ImageUrl = "https://images.unsplash.com/photo-1589182373726-e4f658ab50f0?auto=format&fit=crop&w=1200&q=80",
                    DisplayOrder = 1,
                    IsPrimary = true
                },
                new TourImage
                {
                    ImageUrl = "https://images.unsplash.com/photo-1506744038136-46273834b3fb?auto=format&fit=crop&w=600&q=80",
                    DisplayOrder = 2,
                    IsPrimary = false
                },
                new TourImage
                {
                    ImageUrl = "https://images.unsplash.com/photo-1464822759023-fed622ff2c3b?auto=format&fit=crop&w=600&q=80",
                    DisplayOrder = 3,
                    IsPrimary = false
                },
                new TourImage
                {
                    ImageUrl = "https://images.unsplash.com/photo-1510312305653-8ed496efae75?auto=format&fit=crop&w=600&q=80",
                    DisplayOrder = 4,
                    IsPrimary = false
                },
                new TourImage
                {
                    ImageUrl = "https://images.unsplash.com/photo-1448375240586-882707db888b?auto=format&fit=crop&w=600&q=80",
                    DisplayOrder = 5,
                    IsPrimary = false
                }
            ],
            Activities =
            [
                // Ngày 1
                new TourActivity
                {
                    DayNumber = 1,
                    DayTitle = "Trạm Tôn → Trại 2.800m",
                    StartTime = new TimeSpan(6, 0, 0),
                    DurationMinutes = 60,
                    DisplayOrder = 1,
                    Name = "Khởi hành từ đầu đường mòn Trạm Tôn",
                    Description = "Gặp hướng dẫn viên, nhận trang bị và bắt đầu trekking."
                },
                new TourActivity
                {
                    DayNumber = 1,
                    DayTitle = "Trạm Tôn → Trại 2.800m",
                    StartTime = new TimeSpan(11, 30, 0),
                    DurationMinutes = 90,
                    DisplayOrder = 2,
                    Name = "Ăn trưa tại trại 2.200m",
                    Description = "Nghỉ ngơi, nạp năng lượng trước khi tiếp tục lên cao."
                },
                new TourActivity
                {
                    DayNumber = 1,
                    DayTitle = "Trạm Tôn → Trại 2.800m",
                    StartTime = new TimeSpan(17, 30, 0),
                    DurationMinutes = 120,
                    DisplayOrder = 3,
                    Name = "Dựng lều Eureka tại độ cao 2.800m",
                    Description = "Ổn định khu cắm trại, ăn tối bên lửa trại."
                },
                // Ngày 2
                new TourActivity
                {
                    DayNumber = 2,
                    DayTitle = "Chạm đỉnh Fansipan → Trở về Sa Pa",
                    StartTime = new TimeSpan(4, 30, 0),
                    DurationMinutes = 180,
                    DisplayOrder = 4,
                    Name = "Lên đỉnh 3.143m đón bình minh",
                    Description = "Chinh phục nóc nhà Đông Dương và ngắm biển mây."
                },
                new TourActivity
                {
                    DayNumber = 2,
                    DayTitle = "Chạm đỉnh Fansipan → Trở về Sa Pa",
                    StartTime = new TimeSpan(9, 0, 0),
                    DurationMinutes = 300,
                    DisplayOrder = 5,
                    Name = "Bắt đầu xuống núi",
                    Description = "Thu dọn trang bị và trở về theo đường mòn."
                },
                new TourActivity
                {
                    DayNumber = 2,
                    DayTitle = "Chạm đỉnh Fansipan → Trở về Sa Pa",
                    StartTime = new TimeSpan(15, 0, 0),
                    DurationMinutes = 60,
                    DisplayOrder = 6,
                    Name = "Lễ trao huy chương",
                    Description = "Ghi dấu hành trình chinh phục Fansipan 2N1Đ."
                }
            ],
            ProvidedItems =
            [
                new TourProvidedItem { ItemName = "Lều Eureka 2 lớp", Quantity = 1, QuantityBasis = "PerGroup" },
                new TourProvidedItem { ItemName = "Túi ngủ -5°C", Quantity = 1, QuantityBasis = "PerPerson" },
                new TourProvidedItem { ItemName = "Gậy trekking", Quantity = 1, QuantityBasis = "PerPerson" },
                new TourProvidedItem { ItemName = "Bộ sơ cứu y tế", Quantity = 1, QuantityBasis = "PerPerson" }
            ],
            PreparationItems =
            [
                new TourPreparationItem { ItemName = "Giày chống thấm nước" },
                new TourPreparationItem { ItemName = "Các lớp áo giữ ấm" },
                new TourPreparationItem { ItemName = "Đèn pin đội đầu cá nhân" }
            ],
            Trips =
            [
                new Trip
                {
                    StartAt = new DateTime(2026, 10, 24, 6, 0, 0, DateTimeKind.Utc),
                    EndAt = new DateTime(2026, 10, 25, 18, 0, 0, DateTimeKind.Utc),
                    TotalSlots = 12,
                    BookedSlots = 8, // Còn 4 chỗ
                    MinParticipants = 8,
                    PricePerPerson = 2450000m,
                    Status = "Open"
                }
            ]
        };

        // 4. Các Tour trên Ảnh 1 (Tour List)
        // Card 1: Tà Năng – Phan Dũng: Băng qua đại ngàn
        var tourTaNang = new Tour
        {
            Name = "Tà Năng – Phan Dũng: Băng qua đại ngàn",
            DifficultyLevel = "Trung bình",
            DurationDays = 2,
            DurationNights = 1,
            BasePrice = 2200000m,
            Status = "Active",
            Category = catCampingTrekking,
            MainLocation = locTaNang,
            Images = [new TourImage { ImageUrl = "https://images.unsplash.com/photo-1551632811-561732d1e306?auto=format&fit=crop&w=800&q=80", IsPrimary = true }],
            Trips =
            [
                new Trip
                {
                    StartAt = new DateTime(2026, 9, 28, 6, 0, 0, DateTimeKind.Utc),
                    EndAt = new DateTime(2026, 9, 29, 18, 0, 0, DateTimeKind.Utc),
                    TotalSlots = 15,
                    BookedSlots = 11, // Còn 4 chỗ
                    PricePerPerson = 2200000m,
                    Status = "Open"
                }
            ]
        };

        // Card 2: Lảo Thẩn: Săn mây trên nóc nhà Y Tý
        var tourLaoThan = new Tour
        {
            Name = "Lảo Thẩn: Săn mây trên nóc nhà Y Tý",
            DifficultyLevel = "Thử thách",
            DurationDays = 2,
            DurationNights = 1,
            BasePrice = 1950000m,
            Status = "Active",
            Category = catTrekking,
            MainLocation = locLaoThan,
            Images = [new TourImage { ImageUrl = "https://images.unsplash.com/photo-1519681393784-d120267933ba?auto=format&fit=crop&w=800&q=80", IsPrimary = true }],
            Trips =
            [
                new Trip
                {
                    StartAt = new DateTime(2026, 10, 5, 6, 0, 0, DateTimeKind.Utc),
                    EndAt = new DateTime(2026, 10, 6, 18, 0, 0, DateTimeKind.Utc),
                    TotalSlots = 12,
                    BookedSlots = 10, // Còn 2 chỗ
                    PricePerPerson = 1950000m,
                    Status = "Open"
                }
            ]
        };

        // Card 3: Bidoup: Chạm rừng nguyên sinh
        var tourBidoup = new Tour
        {
            Name = "Bidoup: Chạm rừng nguyên sinh",
            DifficultyLevel = "Trung bình",
            DurationDays = 2,
            DurationNights = 1,
            BasePrice = 2600000m,
            Status = "Active",
            Category = catCampingTrekking,
            MainLocation = locBidoup,
            Images = [new TourImage { ImageUrl = "https://images.unsplash.com/photo-1448375240586-882707db888b?auto=format&fit=crop&w=800&q=80", IsPrimary = true }],
            Trips =
            [
                new Trip
                {
                    StartAt = new DateTime(2026, 10, 12, 6, 0, 0, DateTimeKind.Utc),
                    EndAt = new DateTime(2026, 10, 13, 18, 0, 0, DateTimeKind.Utc),
                    TotalSlots = 15,
                    BookedSlots = 8, // Còn 7 chỗ
                    PricePerPerson = 2600000m,
                    Status = "Open"
                }
            ]
        };

        // Card 4: Pù Luông: Mùa vàng trên bản cao
        var tourPuLuong = new Tour
        {
            Name = "Pù Luông: Mùa vàng trên bản cao",
            DifficultyLevel = "Dễ",
            DurationDays = 2,
            DurationNights = 1,
            BasePrice = 1800000m,
            Status = "Active",
            Category = catCampingTrekking,
            MainLocation = locPuLuong,
            Images = [new TourImage { ImageUrl = "https://images.unsplash.com/photo-1500382017468-9049fed747ef?auto=format&fit=crop&w=800&q=80", IsPrimary = true }],
            Trips =
            [
                new Trip
                {
                    StartAt = new DateTime(2026, 10, 19, 6, 0, 0, DateTimeKind.Utc),
                    EndAt = new DateTime(2026, 10, 20, 18, 0, 0, DateTimeKind.Utc),
                    TotalSlots = 20,
                    BookedSlots = 10, // Còn 10 chỗ
                    PricePerPerson = 1800000m,
                    Status = "Open"
                }
            ]
        };

        // Card 5: Núi Chúa: Cung đường đá và biển (Hết hạn / Đã khởi hành)
        var tourNuiChua = new Tour
        {
            Name = "Núi Chúa: Cung đường đá và biển",
            DifficultyLevel = "Thử thách",
            DurationDays = 2,
            DurationNights = 1,
            BasePrice = 2350000m,
            Status = "Expired", // Tab "Hết hạn"
            Category = catCampingTrekking,
            MainLocation = locNuiChua,
            Images = [new TourImage { ImageUrl = "https://images.unsplash.com/photo-1507525428034-b723cf961d3e?auto=format&fit=crop&w=800&q=80", IsPrimary = true }],
            Trips =
            [
                new Trip
                {
                    StartAt = new DateTime(2026, 9, 14, 6, 0, 0, DateTimeKind.Utc),
                    EndAt = new DateTime(2026, 9, 15, 18, 0, 0, DateTimeKind.Utc),
                    TotalSlots = 15,
                    BookedSlots = 15,
                    PricePerPerson = 2350000m,
                    Status = "Closed"
                }
            ]
        };

        // Card 6: Bạch Mộc Lương Tử: Chạm biển mây (Hết hạn / Đã kết thúc)
        var tourBachMoc = new Tour
        {
            Name = "Bạch Mộc Lương Tử: Chạm biển mây",
            DifficultyLevel = "Khó",
            DurationDays = 2,
            DurationNights = 1,
            BasePrice = 2900000m,
            Status = "Expired", // Tab "Hết hạn"
            Category = catTrekking,
            MainLocation = locBachMoc,
            Images = [new TourImage { ImageUrl = "https://images.unsplash.com/photo-1464822759023-fed622ff2c3b?auto=format&fit=crop&w=800&q=80", IsPrimary = true }],
            Trips =
            [
                new Trip
                {
                    StartAt = new DateTime(2026, 9, 7, 6, 0, 0, DateTimeKind.Utc),
                    EndAt = new DateTime(2026, 9, 8, 18, 0, 0, DateTimeKind.Utc),
                    TotalSlots = 12,
                    BookedSlots = 12,
                    PricePerPerson = 2900000m,
                    Status = "Closed"
                }
            ]
        };

        await context.Tours.AddRangeAsync(tourFansipan, tourTaNang, tourLaoThan, tourBidoup, tourPuLuong, tourNuiChua, tourBachMoc);
        await context.SaveChangesAsync();
    }
}
