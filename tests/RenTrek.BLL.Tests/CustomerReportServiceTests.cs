using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.CustomerReport.Requests;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Services.Implementations;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Tests;

public sealed class CustomerReportServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 3, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RentalOwner_CreatesOpenReport_AndNotifiesStaff()
    {
        var f = new Fixture(OrderTypes.Rental, OrderStatuses.InFulfillment);

        var result = await f.Service.CreateAsync(f.Request(CustomerReportSourceTypes.Rental, "Damaged", "/uploads/customer-reports/a.jpg"));

        var report = Assert.Single(f.Reports);
        Assert.Equal(CustomerReportStatuses.Open, report.Status);
        Assert.Equal(f.OwnerId, report.ReporterUserId);
        Assert.Equal(["/uploads/customer-reports/a.jpg"], report.PhotoUrls);
        Assert.Equal(Now, report.CreatedAt);
        Assert.Equal(CustomerReportSourceTypes.Rental, result.SourceType);
        Assert.Equal([(f.StaffId, NotificationTypes.CustomerReportSubmitted)], f.Notifications);
    }

    [Fact]
    public async Task TripParticipant_CanReport_ButStrangerCannot()
    {
        var f = new Fixture(OrderTypes.TripBooking, OrderStatuses.Confirmed);
        f.CurrentUserId = f.ParticipantId;

        var result = await f.Service.CreateAsync(f.Request(CustomerReportSourceTypes.Trip, "Guide"));
        Assert.Equal("Tour test", result.TourName);

        f.CurrentUserId = Guid.NewGuid();
        await Assert.ThrowsAsync<NotFoundException>(() => f.Service.CreateAsync(f.Request(CustomerReportSourceTypes.Trip, "Guide")));
    }

    [Fact]
    public async Task SourceTypeMustMatchOrderType()
    {
        var f = new Fixture(OrderTypes.Rental, OrderStatuses.InFulfillment);

        await Assert.ThrowsAsync<NotFoundException>(() => f.Service.CreateAsync(f.Request(CustomerReportSourceTypes.Trip, "Guide")));
    }

    [Fact]
    public async Task UnpaidOrder_IsRejected()
    {
        var f = new Fixture(OrderTypes.Rental, OrderStatuses.PendingPayment);

        await Assert.ThrowsAsync<ConflictException>(() => f.Service.CreateAsync(f.Request(CustomerReportSourceTypes.Rental, "Damaged")));
    }

    [Theory]
    [InlineData("Guide", "ngắn", null)]
    [InlineData("NotACategory", "Mô tả đủ dài để hợp lệ", null)]
    [InlineData("Damaged", "Mô tả đủ dài để hợp lệ", "javascript:alert(1)")]
    [InlineData("Damaged", "Mô tả đủ dài để hợp lệ", "//evil.example/a.png")]
    public async Task InvalidInput_ThrowsValidation(string category, string description, string? photo)
    {
        var f = new Fixture(OrderTypes.Rental, OrderStatuses.InFulfillment);
        var request = f.Request(CustomerReportSourceTypes.Rental, category, photo);
        request.Description = description;

        await Assert.ThrowsAsync<ValidationException>(() => f.Service.CreateAsync(request));
        Assert.Empty(f.Reports);
    }

    [Fact]
    public async Task Resolve_RequiresNote_ThenNotifiesReporter_AndLocks()
    {
        var f = new Fixture(OrderTypes.Rental, OrderStatuses.InFulfillment);
        await f.Service.CreateAsync(f.Request(CustomerReportSourceTypes.Rental, "Damaged"));
        var report = f.Reports.Single();
        f.Notifications.Clear();
        f.CurrentUserId = f.StaffId;

        await Assert.ThrowsAsync<ValidationException>(() => f.Service.UpdateStatusAsync(
            report.CustomerReportId, new UpdateCustomerReportStatusRequest { Status = CustomerReportStatuses.Resolved }));

        var result = await f.Service.UpdateStatusAsync(
            report.CustomerReportId,
            new UpdateCustomerReportStatusRequest { Status = CustomerReportStatuses.Resolved, AdminNote = "Đã đổi thiết bị mới" });

        Assert.Equal(CustomerReportStatuses.Resolved, result.Status);
        Assert.Equal("Đã đổi thiết bị mới", report.AdminNote);
        Assert.Equal(f.StaffId, report.HandledByUserId);
        Assert.Equal([(f.OwnerId, NotificationTypes.CustomerReportUpdated)], f.Notifications);

        await Assert.ThrowsAsync<ConflictException>(() => f.Service.UpdateStatusAsync(
            report.CustomerReportId, new UpdateCustomerReportStatusRequest { Status = CustomerReportStatuses.InProgress }));
    }

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class Fixture
    {
        public Fixture(string orderType, string orderStatus)
        {
            var trip = new Trip { TripId = Guid.NewGuid(), StartAt = Now.AddDays(3), Tour = new Tour { Name = "Tour test" } };
            Order = new Order
            {
                OrderId = Guid.NewGuid(),
                OrderCode = "OD-1",
                OrderType = orderType,
                Status = orderStatus,
                CustomerUserId = OwnerId,
                TripBookingOrder = orderType == OrderTypes.TripBooking
                    ? new TripBookingOrder
                    {
                        Trip = trip,
                        Participants = [new BookingParticipant { LinkedUserId = ParticipantId }]
                    }
                    : null!
            };
            CurrentUserId = OwnerId;

            var repository = TestProxy.Create<ICustomerReportRepository>(
                ("GetOrderForReportAsync", args => Task.FromResult<Order?>((Guid)args[0]! == Order.OrderId ? Order : null)),
                ("AddAsync", args =>
                {
                    var report = (CustomerReport)args[0]!;
                    report.Order = Order;
                    Reports.Add(report);
                    return Task.CompletedTask;
                }),
                ("GetDetailAsync", args => Task.FromResult(Reports.SingleOrDefault(x => x.CustomerReportId == (Guid)args[0]!))));
            var unitOfWork = TestProxy.Create<IUnitOfWork>(
                ("get_CustomerReports", _ => repository),
                ("get_RefundCases", _ => TestProxy.Create<IRefundCaseRepository>(
                    ("GetActiveUserIdsByRolesAsync", _ => Task.FromResult<IReadOnlyList<Guid>>([StaffId])))),
                ("SaveChangesAsync", _ => Task.FromResult(1)));
            var notifications = TestProxy.Create<INotificationService>(("QueueAsync", args =>
            {
                Notifications.Add(((Guid)args[0]!, (string)args[1]!));
                return Task.CompletedTask;
            }));

            Service = new CustomerReportService(
                unitOfWork,
                TestProxy.Create<ICurrentUserService>(("get_RequiredUserId", _ => CurrentUserId)),
                TestProxy.Create<IFileStorageService>(),
                notifications,
                new FixedTime());
        }

        public Guid OwnerId { get; } = Guid.NewGuid();
        public Guid ParticipantId { get; } = Guid.NewGuid();
        public Guid StaffId { get; } = Guid.NewGuid();
        public Guid CurrentUserId { get; set; }
        public Order Order { get; }
        public CustomerReportService Service { get; }
        public List<CustomerReport> Reports { get; } = [];
        public List<(Guid, string)> Notifications { get; } = [];

        public CreateCustomerReportRequest Request(string sourceType, string category, string? photo = null) => new()
        {
            SourceType = sourceType,
            OrderId = Order.OrderId,
            Category = category,
            Description = "Thiết bị bị rách một bên khi nhận",
            PhotoUrls = photo is null ? [] : [photo]
        };
    }
}
