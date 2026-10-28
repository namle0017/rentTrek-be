using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Order.Requests;
using RenTrek.BLL.Helpers;
using RenTrek.BLL.Validators.Order;
using RenTrek.DAL.Models;

namespace RenTrek.BLL.Tests;

public sealed class MyOrderMapperTests
{
    [Fact]
    public void BuildTitle_RentalWithSingleItem_ReturnsItemName()
    {
        var row = new MyOrderRow { OrderType = OrderTypes.Rental, FirstItemName = "Lều 2 người", ItemCount = 1 };

        Assert.Equal("Lều 2 người", MyOrderMapper.BuildTitle(row));
    }

    [Fact]
    public void BuildTitle_RentalWithManyItems_AppendsRemainingCount()
    {
        var row = new MyOrderRow { OrderType = OrderTypes.Rental, FirstItemName = "Lều 2 người", ItemCount = 4 };

        Assert.Equal("Lều 2 người +3", MyOrderMapper.BuildTitle(row));
    }

    [Fact]
    public void BuildTitle_RentalWithoutItemName_UsesFallback()
    {
        var row = new MyOrderRow { OrderType = OrderTypes.Rental, FirstItemName = null, ItemCount = 0 };

        Assert.Equal("Thuê thiết bị", MyOrderMapper.BuildTitle(row));
    }

    [Fact]
    public void BuildTitle_TripBooking_ReturnsTourName()
    {
        var row = new MyOrderRow { OrderType = OrderTypes.TripBooking, TourName = "Fansipan 2 ngày", FirstItemName = "bỏ qua", ItemCount = 3 };

        Assert.Equal("Fansipan 2 ngày", MyOrderMapper.BuildTitle(row));
    }

    [Fact]
    public void ToResponse_CopiesFieldsAndTotalPayableEqualsTotalAmount()
    {
        var id = Guid.NewGuid();
        var start = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
        var row = new MyOrderRow
        {
            OrderId = id,
            OrderCode = "RT-001",
            OrderType = OrderTypes.Rental,
            Status = "Pending",
            PaymentStatus = "Unpaid",
            TotalAmount = 500_000m,
            ServiceStartAt = start,
            ServiceEndAt = start.AddDays(2),
            FirstItemName = "Balo",
            ItemCount = 2
        };

        var response = MyOrderMapper.ToResponse(row);

        Assert.Equal(id, response.Id);
        Assert.Equal("RT-001", response.OrderCode);
        Assert.Equal(500_000m, response.TotalAmount);
        Assert.Equal(500_000m, response.TotalPayable);
        Assert.Equal(start, response.ServiceStartAt);
        Assert.Equal("Balo +1", response.Title);
    }

    [Fact]
    public void ToFilter_NormalizesValuesAndMapsSorting()
    {
        var request = new MyOrderQueryRequest
        {
            OrderType = " rental ",
            Status = " Pending ",
            Search = "  RT ",
            SortBy = "SERVICESTARTAT",
            SortOrder = "asc",
            PageNumber = 0,
            PageSize = 1000
        };

        var filter = MyOrderMapper.ToFilter(request);

        Assert.Equal(OrderTypes.Rental, filter.OrderType);
        Assert.Equal("Pending", filter.Status);
        Assert.Equal("RT", filter.Search);
        Assert.Equal("serviceStartAt", filter.SortBy);
        Assert.False(filter.IsDescending);
        Assert.Equal(1, filter.PageNumber);
        Assert.Equal(100, filter.PageSize);
    }

    [Fact]
    public void Validator_RejectsInvalidOrderTypeAndSort()
    {
        var validator = new MyOrderQueryRequestValidator();

        var invalid = validator.Validate(new MyOrderQueryRequest { OrderType = "Tour", SortBy = "name", SortOrder = "up" });
        var valid = validator.Validate(new MyOrderQueryRequest { OrderType = "TripBooking", SortBy = "serviceStartAt", SortOrder = "asc" });

        Assert.False(invalid.IsValid);
        Assert.Equal(3, invalid.Errors.Count);
        Assert.True(valid.IsValid);
    }
}
