using RenTrek.BLL.DTOs.AdminTrip.Requests;
using RenTrek.BLL.DTOs.Guide.Requests;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Services.Implementations;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Tests;

public sealed class TripOperationsAndGuideServicesTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 11, 1, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RecurrenceRule_PreviewAndGenerate_SkipsExistingAndCreatesNewDraftTrips()
    {
        var adminId = Guid.NewGuid();
        var tourId = Guid.NewGuid();
        var ruleId = Guid.NewGuid();
        var existingTripId = Guid.NewGuid();

        var tour = new Tour
        {
            Id = tourId,
            Name = "Tà Năng - Phan Dũng",
            Status = "Published",
            DurationDays = 2,
            DurationMinutes = 2880,
            BasePrice = 3_200_000m,
            ProvidedItems =
            [
                new TourProvidedItem
                {
                    Id = Guid.NewGuid(),
                    TourId = tourId,
                    ProductVariantId = Guid.NewGuid(),
                    ItemName = "Gậy Trekking",
                    QuantityBasis = "PerPerson",
                    Quantity = 1
                }
            ]
        };

        var rule = new TourRecurrenceRule
        {
            RecurrenceRuleId = ruleId,
            TourId = tourId,
            Tour = tour,
            Frequency = "Weekly",
            IntervalValue = 1,
            DaysOfWeek = "Friday",
            StartTime = new TimeOnly(6, 0),
            StartDate = new DateOnly(2026, 12, 4), // Friday
            EndDate = new DateOnly(2026, 12, 11),  // Next Friday (2 Fridays total)
            DefaultTotalSlots = 20,
            DefaultPrice = 3_200_000m,
            IsActive = true
        };

        var uow = new TestUnitOfWork();
        uow.RecurrenceRepo.Tour = tour;
        uow.RecurrenceRepo.Rule = rule;
        uow.RecurrenceRepo.RefundPolicy = new RefundPolicy
        {
            RefundPolicyId = Guid.NewGuid(),
            Name = "Hoàn tiền tiêu chuẩn",
            Status = "Active"
        };
        uow.RecurrenceRepo.ExistingTrips.Add(new Trip
        {
            TripId = existingTripId,
            TourId = tourId,
            StartAt = new DateTimeOffset(2026, 12, 4, 6, 0, 0, TimeSpan.Zero),
            EndAt = new DateTimeOffset(2026, 12, 6, 6, 0, 0, TimeSpan.Zero)
        });

        var service = new AdminRecurrenceRuleService(
            uow,
            new TestCurrentUserService(adminId, "Admin"),
            new TestTimeProvider(FixedNow));

        var preview = await service.PreviewTripsAsync(ruleId);
        Assert.Equal(2, preview.Count);
        Assert.True(preview[0].IsAlreadyGenerated);
        Assert.Equal(existingTripId, preview[0].ExistingTripId);
        Assert.False(preview[1].IsAlreadyGenerated);

        var generated = await service.GenerateTripsAsync(ruleId);
        Assert.Equal(1, generated.CreatedCount);
        Assert.Equal(1, generated.SkippedCount);
        Assert.Single(uow.TripRepo.AddedTrips);
        Assert.Equal("Draft", uow.TripRepo.AddedTrips[0].Status);
        Assert.Single(uow.TripRepo.AddedProvidedItems);
        Assert.Equal(20, uow.TripRepo.AddedProvidedItems[0].RequiredQuantity);
    }

    [Fact]
    public async Task AdminTrip_Update_StaffCannotHandTripToAnotherManager()
    {
        var staffId = Guid.NewGuid();
        var tripId = Guid.NewGuid();
        var uow = new TestUnitOfWork();
        uow.TripRepo.Trip = new Trip
        {
            TripId = tripId,
            TourId = Guid.NewGuid(),
            RefundPolicyId = Guid.NewGuid(),
            ManagedByUserId = staffId,
            StartAt = FixedNow.AddDays(10),
            EndAt = FixedNow.AddDays(12),
            TotalSlots = 20,
            MinParticipants = 10,
            PricePerPerson = 3_000_000m,
            Status = "Open",
            RowVersion = 2
        };
        var service = new AdminTripService(uow, new TestCurrentUserService(staffId, "Staff"), new TestTimeProvider(FixedNow), TestProxy.Create<INotificationService>());

        var ex = await Assert.ThrowsAsync<ForbiddenException>(() => service.UpdateAsync(
            tripId,
            new UpdateAdminTripRequest
            {
                StartAt = FixedNow.AddDays(10),
                EndAt = FixedNow.AddDays(12),
                TotalSlots = 20,
                MinParticipants = 10,
                PricePerPerson = 3_000_000m,
                RowVersion = 2,
                ManagedByUserId = Guid.NewGuid()
            }));
        Assert.Equal("TRIP_NOT_ASSIGNED", ex.ErrorCode);
        Assert.Equal(staffId, uow.TripRepo.Trip.ManagedByUserId);
    }

    [Fact]
    public async Task AdminTrip_Update_EnforcesRowVersionAndSoldSlotsInvariants()
    {
        var tripId = Guid.NewGuid();
        var uow = new TestUnitOfWork();
        uow.TripRepo.Trip = new Trip
        {
            TripId = tripId,
            TourId = Guid.NewGuid(),
            RefundPolicyId = Guid.NewGuid(),
            StartAt = FixedNow.AddDays(10),
            EndAt = FixedNow.AddDays(12),
            TotalSlots = 20,
            MinParticipants = 10,
            PricePerPerson = 3_000_000m,
            Status = "Open",
            RowVersion = 2
        };
        uow.TripRepo.SoldSlots = 15;

        var service = new AdminTripService(
            uow,
            new TestCurrentUserService(Guid.NewGuid(), "Admin"),
            new TestTimeProvider(FixedNow),
            TestProxy.Create<INotificationService>());

        // 1. RowVersion mismatch -> 409 CONCURRENCY_CONFLICT
        var concurrencyEx = await Assert.ThrowsAsync<ConflictException>(() => service.UpdateAsync(
            tripId,
            new UpdateAdminTripRequest
            {
                StartAt = FixedNow.AddDays(10),
                EndAt = FixedNow.AddDays(12),
                TotalSlots = 20,
                MinParticipants = 10,
                PricePerPerson = 3_000_000m,
                RowVersion = 1
            }));
        Assert.Equal("CONCURRENCY_CONFLICT", concurrencyEx.ErrorCode);

        // 2. TotalSlots < SoldSlots (12 < 15) -> 409 TOTAL_SLOTS_BELOW_SOLD
        var slotsEx = await Assert.ThrowsAsync<ConflictException>(() => service.UpdateAsync(
            tripId,
            new UpdateAdminTripRequest
            {
                StartAt = FixedNow.AddDays(10),
                EndAt = FixedNow.AddDays(12),
                TotalSlots = 12,
                MinParticipants = 10,
                PricePerPerson = 3_000_000m,
                RowVersion = 2
            }));
        Assert.Equal("TOTAL_SLOTS_BELOW_SOLD", slotsEx.ErrorCode);

        // 3. Valid update increments RowVersion
        var updated = await service.UpdateAsync(
            tripId,
            new UpdateAdminTripRequest
            {
                StartAt = FixedNow.AddDays(10),
                EndAt = FixedNow.AddDays(12),
                TotalSlots = 25,
                MinParticipants = 10,
                PricePerPerson = 3_100_000m,
                RowVersion = 2
            });
        Assert.Equal(3, updated.RowVersion);
        Assert.Equal(25, updated.TotalSlots);
    }

    [Fact]
    public async Task AdminTrip_StartAndCancel_ValidatesReadinessAndCreatesRefundsAndReleasesAssets()
    {
        var tripId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var guideId = Guid.NewGuid();

        var order = new Order
        {
            OrderId = orderId,
            OrderCode = "ORD-2026-001",
            Status = "Confirmed",
            PaymentStatus = "Paid",
            TotalAmount = 6_400_000m
        };
        var booking = new TripBookingOrder
        {
            OrderId = orderId,
            TripId = tripId,
            ParticipantCount = 2,
            UnitPrice = 3_200_000m,
            Order = order,
            Participants =
            [
                new BookingParticipant
                {
                    ParticipantId = Guid.NewGuid(),
                    BookingOrderId = orderId,
                    TripId = tripId,
                    TripGroupId = groupId,
                    FullName = "Khách 1",
                    Status = "Confirmed"
                },
                new BookingParticipant
                {
                    ParticipantId = Guid.NewGuid(),
                    BookingOrderId = orderId,
                    TripId = tripId,
                    TripGroupId = groupId,
                    FullName = "Khách 2",
                    Status = "Confirmed"
                }
            ]
        };
        foreach (var p in booking.Participants)
            p.BookingOrder = booking;

        var uow = new TestUnitOfWork();
        uow.TripRepo.Trip = new Trip
        {
            TripId = tripId,
            TourId = Guid.NewGuid(),
            RefundPolicyId = Guid.NewGuid(),
            StartAt = FixedNow.AddDays(5),
            EndAt = FixedNow.AddDays(7),
            TotalSlots = 10,
            MinParticipants = 2,
            PricePerPerson = 3_200_000m,
            Status = "ClosedForBooking",
            RowVersion = 1
        };
        uow.TripRepo.SoldSlots = 2;
        uow.TripRepo.Bookings.Add(booking);
        uow.TripRepo.Participants.AddRange(booking.Participants);
        uow.TripRepo.Groups.Add(new TripGroup
        {
            TripGroupId = groupId,
            TripId = tripId,
            GroupName = "Nhóm Đại Bàng",
            GuideUserId = guideId,
            MaxParticipants = 10,
            Status = "Open"
        });
        uow.TripRepo.ProvidedItems.Add(new TripProvidedItem
        {
            TripProvidedItemId = Guid.NewGuid(),
            TripId = tripId,
            RequiredQuantity = 1,
            Status = "Ready",
            AssetAssignments =
            [
                new AssetAssignment
                {
                    AssetAssignmentId = Guid.NewGuid(),
                    Status = "Reserved"
                }
            ]
        });

        var service = new AdminTripService(
            uow,
            new TestCurrentUserService(Guid.NewGuid(), "Admin"),
            new TestTimeProvider(FixedNow),
            TestProxy.Create<INotificationService>());

        var readiness = await service.GetReadinessChecklistAsync(tripId);
        Assert.True(readiness.IsReadyToDepart);

        var started = await service.StartTripAsync(tripId);
        Assert.Equal("InProgress", started.Status);

        var cancelResult = await service.CancelTripAsync(
            tripId,
            new CancelAdminTripRequest { Reason = "Mưa bão khẩn cấp" });

        Assert.Equal(1, cancelResult.AffectedBookingsCount);
        Assert.Equal(2, cancelResult.AffectedParticipantsCount);
        Assert.Equal(6_400_000m, cancelResult.TotalRefundAmount);
        Assert.Equal(1, cancelResult.ReleasedAssetsCount);
        Assert.Single(uow.TripRepo.AddedPayments);
        Assert.Equal("PendingApproval", uow.TripRepo.AddedPayments[0].Status);
        Assert.Single(uow.TripRepo.AddedAuditLogs);
    }

    [Fact]
    public async Task KanbanGroup_AutoGroupAndSplitApprove_KeepsPartiesTogetherUnlessSplitApproved()
    {
        var tripId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var group1Id = Guid.NewGuid();
        var group2Id = Guid.NewGuid();

        var order = new Order
        {
            OrderId = bookingId,
            OrderCode = "ORD-PARTY-3",
            Status = "Confirmed",
            PaymentStatus = "Paid",
            CreatedAt = FixedNow
        };
        var booking = new TripBookingOrder
        {
            OrderId = bookingId,
            TripId = tripId,
            ParticipantCount = 3,
            GroupingPreference = "KeepTogether",
            IsSplit = false,
            Order = order
        };

        var participants = new List<BookingParticipant>
        {
            new() { ParticipantId = Guid.NewGuid(), BookingOrderId = bookingId, TripId = tripId, FullName = "A", BookingOrder = booking, Status = "Confirmed" },
            new() { ParticipantId = Guid.NewGuid(), BookingOrderId = bookingId, TripId = tripId, FullName = "B", BookingOrder = booking, Status = "Confirmed" },
            new() { ParticipantId = Guid.NewGuid(), BookingOrderId = bookingId, TripId = tripId, FullName = "C", BookingOrder = booking, Status = "Confirmed" }
        };

        var uow = new TestUnitOfWork();
        uow.GroupRepo.Trip = new Trip { TripId = tripId, StartAt = FixedNow.AddDays(3), EndAt = FixedNow.AddDays(5) };
        uow.GroupRepo.Booking = booking;
        uow.GroupRepo.Groups.AddRange(
        [
            new TripGroup { TripGroupId = group1Id, TripId = tripId, GroupName = "Nhóm 1", MaxParticipants = 2, Status = "Draft" },
            new TripGroup { TripGroupId = group2Id, TripId = tripId, GroupName = "Nhóm 2", MaxParticipants = 2, Status = "Draft" }
        ]);
        uow.GroupRepo.Participants.AddRange(participants);

        var service = new AdminTripGroupService(
            uow,
            new TestCurrentUserService(Guid.NewGuid(), "Admin"),
            new TestTimeProvider(FixedNow));

        // Chưa duyệt tách đoàn: đoàn 3 người không vừa nhóm 2 chỗ -> giữ nguyên ở cột Chưa xếp nhóm
        var firstAttempt = await service.AutoGroupAsync(tripId);
        Assert.Equal(0, firstAttempt.AssignedCount);
        Assert.Equal(3, firstAttempt.RemainingUnassignedCount);

        // Duyệt tách đoàn (9.7)
        var splitResult = await service.ApproveBookingSplitAsync(
            tripId,
            bookingId,
            new SplitApproveBookingRequest { Reason = "Khách đồng ý tách thành 2 nhóm" });
        Assert.True(splitResult.IsSplit);

        // Sau khi duyệt tách đoàn: AutoGroup tự động chia 2 người vào Nhóm 1, 1 người vào Nhóm 2
        var secondAttempt = await service.AutoGroupAsync(tripId);
        Assert.Equal(3, secondAttempt.AssignedCount);
        Assert.Equal(0, secondAttempt.RemainingUnassignedCount);
    }

    [Fact]
    public async Task KanbanCreateGroup_OnlyAcceptsActiveGuideAccount()
    {
        var tripId = Guid.NewGuid();
        var customer = new Account { UserId = Guid.NewGuid(), Status = "Active", Role = new Role { RoleName = "Customer" } };
        var guide = new Account { UserId = Guid.NewGuid(), Status = "Active", Role = new Role { RoleName = "Guide" } };
        var uow = new TestUnitOfWork();
        uow.GroupRepo.Trip = new Trip { TripId = tripId, StartAt = FixedNow.AddDays(3), EndAt = FixedNow.AddDays(5) };
        uow.GroupRepo.Accounts.AddRange([customer, guide]);
        var service = new AdminTripGroupService(uow, new TestCurrentUserService(Guid.NewGuid(), "Admin"), new TestTimeProvider(FixedNow));

        await Assert.ThrowsAsync<BadRequestException>(() => service.CreateGroupAsync(
            tripId, new CreateTripGroupRequest { GroupName = "Nhóm A", GuideUserId = customer.UserId }));

        var created = await service.CreateGroupAsync(tripId, new CreateTripGroupRequest { GroupName = "Nhóm B", GuideUserId = guide.UserId });
        Assert.Equal("Nhóm B", created.GroupName);
    }

    [Fact]
    public async Task AttendanceAndGuidePortal_EnforcesReasonOnEdit_GuideOwnership_AndBatchIdempotency()
    {
        var tripId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        var guideId = Guid.NewGuid();
        var otherGuideId = Guid.NewGuid();
        var participantId = Guid.NewGuid();

        var activity = new TourActivity
        {
            Id = activityId,
            DayNumber = 1,
            Name = "Tập trung bìa rừng",
            StartTime = new TimeSpan(7, 30, 0),
            DurationMinutes = 45
        };
        var tour = new Tour
        {
            Id = Guid.NewGuid(),
            Name = "Tà Năng",
            Activities = [activity],
            MainLocation = new Location { Name = "Bìa rừng", Address = "Lâm Đồng", Latitude = 11.6m, Longitude = 108.3m }
        };
        var trip = new Trip
        {
            TripId = tripId,
            TourId = tour.Id,
            Tour = tour,
            StartAt = FixedNow,
            EndAt = FixedNow.AddDays(2),
            Status = "InProgress"
        };
        var participant = new BookingParticipant
        {
            ParticipantId = participantId,
            TripId = tripId,
            FullName = "Nguyễn Hoàng Nam",
            Status = "Confirmed",
            BookingOrder = new TripBookingOrder { Order = new Order { Status = "Confirmed" } }
        };
        var group = new TripGroup
        {
            TripGroupId = Guid.NewGuid(),
            TripId = tripId,
            Trip = trip,
            GuideUserId = guideId,
            GroupName = "Nhóm Đại Bàng",
            MaxParticipants = 10,
            BookingParticipants = [participant]
        };
        participant.TripGroupId = group.TripGroupId;
        participant.TripGroup = group;

        var uow = new TestUnitOfWork();
        uow.GroupRepo.Trip = trip;
        uow.GroupRepo.Groups.Add(group);
        uow.AttendanceRepo.Trip = trip;
        uow.AttendanceRepo.Participants.Add(participant);

        // 1. Guide không phụ trách nhóm bị chặn 403 GUIDE_NOT_ASSIGNED_TO_GROUP
        var unauthorizedGuideService = new GuidePortalService(
            uow,
            new TestCurrentUserService(otherGuideId, "Guide"),
            new TestTimeProvider(FixedNow));

        var forbiddenEx = await Assert.ThrowsAsync<ForbiddenException>(() =>
            unauthorizedGuideService.GetGroupRosterAsync(tripId));
        Assert.Equal("GUIDE_NOT_ASSIGNED_TO_GROUP", forbiddenEx.ErrorCode);

        // 2. Guide phụ trách nhóm điểm danh batch với ClientBatchId
        var guideService = new GuidePortalService(
            uow,
            new TestCurrentUserService(guideId, "Guide"),
            new TestTimeProvider(FixedNow));

        var batch1 = await guideService.SubmitBatchAttendanceAsync(
            tripId,
            new GuideBatchAttendanceRequest
            {
                ActivityId = activityId,
                ClientBatchId = "batch-001",
                MarkAllPresent = true
            });
        Assert.Equal(1, batch1.UpdatedCount);
        Assert.Equal("1/1 có mặt", batch1.ProgressText);

        // Gửi lại cùng ClientBatchId (offline retry) -> Idempotent (UpdatedCount = 0)
        var batchRetry = await guideService.SubmitBatchAttendanceAsync(
            tripId,
            new GuideBatchAttendanceRequest
            {
                ActivityId = activityId,
                ClientBatchId = "batch-001",
                MarkAllPresent = true
            });
        Assert.Equal(0, batchRetry.UpdatedCount);
        Assert.Equal("1/1 có mặt", batchRetry.ProgressText);

        // 3. Sửa trạng thái đã lưu mà không nhập lý do -> 400 REASON_REQUIRED_FOR_UPDATE
        var missingReasonEx = await Assert.ThrowsAsync<BadRequestException>(() =>
            guideService.SubmitBatchAttendanceAsync(
                tripId,
                new GuideBatchAttendanceRequest
                {
                    ActivityId = activityId,
                    MarkAllPresent = false,
                    Attendances =
                    [
                        new GuideAttendanceRecordItemRequest
                        {
                            ParticipantId = participantId,
                            Status = "Late",
                            Reason = null
                        }
                    ]
                }));
        Assert.Equal("REASON_REQUIRED_FOR_UPDATE", missingReasonEx.ErrorCode);

        // 4. Sửa trạng thái có lý do -> Thành công và ghi AuditLog
        var updatedBatch = await guideService.SubmitBatchAttendanceAsync(
            tripId,
            new GuideBatchAttendanceRequest
            {
                ActivityId = activityId,
                MarkAllPresent = false,
                Attendances =
                [
                    new GuideAttendanceRecordItemRequest
                    {
                        ParticipantId = participantId,
                        Status = "Late",
                        Reason = "Khách đến trễ 10 phút"
                    }
                ]
            });
        Assert.Equal(1, updatedBatch.UpdatedCount);
        Assert.Contains(uow.TripRepo.AddedAuditLogs, l => l.Action == "UPDATE_ATTENDANCE");
    }

    private sealed class TestCurrentUserService(Guid userId, string role) : ICurrentUserService
    {
        public Guid? UserId => userId;
        public Guid RequiredUserId => userId;
        public string? Jti => "test-jti";
        public string? Role => role;
        public bool IsAuthenticated => true;
    }

    private sealed class TestTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class TestUnitOfWork : IUnitOfWork
    {
        public IAdminOperationRepository AdminOperations => throw new NotSupportedException();
        IRefundCaseRepository IUnitOfWork.RefundCases => throw new NotSupportedException();

        public FakeRecurrenceRuleRepo RecurrenceRepo { get; } = new();
        public FakeTripRepo TripRepo { get; } = new();
        public FakeTripGroupRepo GroupRepo { get; } = new();
        public FakeBookingParticipantRepo ParticipantRepo { get; } = new();
        public FakeActivityAttendanceRepo AttendanceRepo { get; }

        public TestUnitOfWork()
        {
            AttendanceRepo = new FakeActivityAttendanceRepo(TripRepo);
        }

        public ITourRecurrenceRuleRepository TourRecurrenceRules => RecurrenceRepo;
        public ITripRepository Trips => TripRepo;
        public ITripGroupRepository TripGroups => GroupRepo;
        public IBookingParticipantRepository BookingParticipants => ParticipantRepo;
        public IActivityAttendanceRepository ActivityAttendances => AttendanceRepo;

        public IAccountRepository Accounts => throw new NotSupportedException();
        public IRoleRepository Roles => throw new NotSupportedException();
        public IRefreshTokenRepository RefreshTokens => throw new NotSupportedException();
        public IAccessTokenRepository AccessTokens => throw new NotSupportedException();
        public ITourRepository Tours => throw new NotSupportedException();
        public IAppliedPromotionRepository AppliedPromotions => throw new NotSupportedException();
        public IAssetAssignmentRepository AssetAssignments => throw new NotSupportedException();
        public IAuditLogRepository AuditLogs => TestProxy.Create<IAuditLogRepository>();
        public ICategoryRepository Categories => throw new NotSupportedException();
        public IEquipmentAssetRepository EquipmentAssets => throw new NotSupportedException();
        public ILocationRepository Locations => throw new NotSupportedException();
        public INotificationRepository Notifications => throw new NotSupportedException();
        public IOrderRepository Orders => throw new NotSupportedException();
        public IParticipantPostReadRepository ParticipantPostReads => throw new NotSupportedException();
        public IPasswordResetTokenRepository PasswordResetTokens => throw new NotSupportedException();
        public IPaymentRepository Payments => throw new NotSupportedException();
        public IPostRepository Posts => throw new NotSupportedException();
        public IPostSoftSkillRepository PostSoftSkills => throw new NotSupportedException();
        public IPostTagRepository PostTags => throw new NotSupportedException();
        public IProductRepository Products => throw new NotSupportedException();
        public IProductBundleRepository ProductBundles => throw new NotSupportedException();
        public IProductBundleItemRepository ProductBundleItems => throw new NotSupportedException();
        public IProductVariantRepository ProductVariants => throw new NotSupportedException();
        public IProductVariantImageRepository ProductVariantImages => throw new NotSupportedException();
        public IProfileRepository Profiles => throw new NotSupportedException();
        public IPromotionRepository Promotions => throw new NotSupportedException();
        public IPromotionTierRepository PromotionTiers => throw new NotSupportedException();
        public IRefundPolicyRepository RefundPolicies => throw new NotSupportedException();
        public IRefundPolicyRuleRepository RefundPolicyRules => throw new NotSupportedException();
        public IRentalBundleItemSnapshotRepository RentalBundleItemSnapshots => throw new NotSupportedException();
        public IRentalChargeRepository RentalCharges => throw new NotSupportedException();
        public IRentalOrderRepository RentalOrders => throw new NotSupportedException();
        public IRentalOrderItemRepository RentalOrderItems => throw new NotSupportedException();
        public IRentalPriceTierRepository RentalPriceTiers => throw new NotSupportedException();
        public IRentalPolicyRepository RentalPolicies => throw new NotSupportedException();
        public IRentalEvidencePhotoRepository RentalEvidencePhotos => throw new NotSupportedException();
        public IRentalReportRepository RentalReports => throw new NotSupportedException();
        public IRentalSettlementRepository RentalSettlements => throw new NotSupportedException();
        public IReviewRepository Reviews => throw new NotSupportedException();
        public ISoftSkillRepository SoftSkills => throw new NotSupportedException();
        public ISystemConfigurationRepository SystemConfigurations => throw new NotSupportedException();
        public ITagRepository Tags => throw new NotSupportedException();
        public ITourActivityRepository TourActivities => throw new NotSupportedException();
        public ITourImageRepository TourImages => throw new NotSupportedException();
        public ITourPostRepository TourPosts => throw new NotSupportedException();
        public ITourPreparationItemRepository TourPreparationItems => throw new NotSupportedException();
        public ITourProvidedItemRepository TourProvidedItems => throw new NotSupportedException();
        public ITripBookingRepository TripBookings => TestProxy.Create<ITripBookingRepository>(("GetTripForUpdateAsync", _ => Task.FromResult(GroupRepo.Trip)));
        public ITripBookingOrderRepository TripBookingOrders => throw new NotSupportedException();
        public ITripProvidedItemRepository TripProvidedItems => throw new NotSupportedException();
        public IUserExternalLoginRepository UserExternalLogins => throw new NotSupportedException();
        public IGenericRepository<TEntity> Repository<TEntity>() where TEntity : class => throw new NotSupportedException();

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task<bool> TrySaveChangesIgnoringUniqueViolationAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task BeginTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Dispose() { }
    }

    private abstract class BaseFakeGenericRepo<T> : IGenericRepository<T> where T : class
    {
        public virtual Task<T?> GetByIdAsync(object id, CancellationToken cancellationToken = default) => Task.FromResult<T?>(null);
        public virtual Task AddAsync(T entity, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public virtual Task AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Update(T entity) { }
        public void Remove(T entity) { }
        public void RemoveRange(IEnumerable<T> entities) { }
    }

    private sealed class FakeRecurrenceRuleRepo : BaseFakeGenericRepo<TourRecurrenceRule>, ITourRecurrenceRuleRepository
    {
        public Tour? Tour { get; set; }
        public TourRecurrenceRule? Rule { get; set; }
        public RefundPolicy? RefundPolicy { get; set; }
        public List<Trip> ExistingTrips { get; } = [];

        public Task<Tour?> GetTourByIdAsync(Guid tourId, CancellationToken cancellationToken = default) => Task.FromResult(Tour);
        public Task<IReadOnlyList<TourRecurrenceRule>> GetByTourIdAsync(Guid tourId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TourRecurrenceRule>>(Rule is null ? [] : [Rule]);
        public Task<TourRecurrenceRule?> GetRuleWithTourAsync(Guid ruleId, CancellationToken cancellationToken = default) => Task.FromResult(Rule);
        public Task<TourRecurrenceRule?> GetRuleForUpdateAsync(Guid ruleId, CancellationToken cancellationToken = default) => Task.FromResult(Rule);
        public Task<IReadOnlyList<Trip>> GetTripsByTourAndRangeAsync(Guid tourId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Trip>>(ExistingTrips);
        public Task<RefundPolicy?> GetDefaultOrSpecificRefundPolicyAsync(Guid? refundPolicyId, CancellationToken cancellationToken = default)
            => Task.FromResult(RefundPolicy);
    }

    private sealed class FakeTripRepo : BaseFakeGenericRepo<Trip>, ITripRepository
    {
        Task<Trip?> ITripRepository.GetTripWithTourAndRequirementsAsync(Guid tripId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Trip? Trip { get; set; }
        public int SoldSlots { get; set; }
        public List<Trip> AddedTrips { get; } = [];
        public List<TripProvidedItem> AddedProvidedItems { get; } = [];
        public List<TripBookingOrder> Bookings { get; } = [];
        public List<TripGroup> Groups { get; } = [];
        public List<BookingParticipant> Participants { get; } = [];
        public List<TripProvidedItem> ProvidedItems { get; } = [];
        public List<Payment> AddedPayments { get; } = [];
        public List<AuditLog> AddedAuditLogs { get; } = [];

        public override Task AddRangeAsync(IEnumerable<Trip> entities, CancellationToken cancellationToken = default)
        {
            AddedTrips.AddRange(entities);
            return Task.CompletedTask;
        }

        public Task<(IReadOnlyList<Trip> Items, int TotalCount)> GetPagedAdminTripsAsync(
            Guid? tourId, string? status, DateTimeOffset? fromStartAt, DateTimeOffset? toStartAt, Guid? managedByUserId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
            => Task.FromResult<(IReadOnlyList<Trip>, int)>((Trip is null ? [] : [Trip], Trip is null ? 0 : 1));

        public Task<Dictionary<Guid, int>> GetSoldSlotsByTripIdsAsync(IReadOnlyCollection<Guid> tripIds, DateTimeOffset utcNow, CancellationToken cancellationToken = default)
            => Task.FromResult(tripIds.ToDictionary(id => id, _ => SoldSlots));

        public Task<Trip?> GetAdminTripDetailAsync(Guid tripId, CancellationToken cancellationToken = default) => Task.FromResult(Trip);
        public Task<Trip?> GetTripForUpdateAsync(Guid tripId, CancellationToken cancellationToken = default) => Task.FromResult(Trip);
        public Task<Tour?> GetTourWithProvidedItemsAsync(Guid tourId, CancellationToken cancellationToken = default) => Task.FromResult<Tour?>(null);
        public Task<RefundPolicy?> GetActiveRefundPolicyAsync(Guid refundPolicyId, CancellationToken cancellationToken = default) => Task.FromResult<RefundPolicy?>(null);
        public Task<Account?> GetAccountByIdAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult<Account?>(null);
        public Task<int> GetSoldSlotsCountAsync(Guid tripId, DateTimeOffset utcNow, CancellationToken cancellationToken = default) => Task.FromResult(SoldSlots);
        public Task<IReadOnlyList<TripBookingOrder>> GetTripBookingsWithDetailsAsync(Guid tripId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TripBookingOrder>>(Bookings);
        public Task<IReadOnlyList<TripBookingOrder>> GetTripBookingsForUpdateAsync(Guid tripId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TripBookingOrder>>(Bookings);
        public Task<IReadOnlyList<TripGroup>> GetActiveTripGroupsAsync(Guid tripId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TripGroup>>(Groups);
        public Task<IReadOnlyList<TripGroup>> GetActiveTripGroupsForUpdateAsync(Guid tripId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TripGroup>>(Groups);
        public Task<IReadOnlyList<BookingParticipant>> GetActiveParticipantsByTripAsync(Guid tripId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<BookingParticipant>>(Participants);
        public Task<IReadOnlyList<TripProvidedItem>> GetTripProvidedItemsWithAssetsAsync(Guid tripId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TripProvidedItem>>(ProvidedItems);
        public Task<IReadOnlyList<TripProvidedItem>> GetTripProvidedItemsForUpdateAsync(Guid tripId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TripProvidedItem>>(ProvidedItems);
        public Task<IReadOnlyList<Payment>> GetSucceededPaymentsByOrderIdsAsync(IReadOnlyCollection<Guid> orderIds, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Payment>>([]);
        public Task AddPaymentsAsync(IEnumerable<Payment> payments, CancellationToken cancellationToken = default)
        {
            AddedPayments.AddRange(payments);
            return Task.CompletedTask;
        }
        public Task AddTripProvidedItemsAsync(IEnumerable<TripProvidedItem> items, CancellationToken cancellationToken = default)
        {
            AddedProvidedItems.AddRange(items);
            return Task.CompletedTask;
        }
        public Task AddAuditLogAsync(AuditLog log, CancellationToken cancellationToken = default)
        {
            AddedAuditLogs.Add(log);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTripGroupRepo : BaseFakeGenericRepo<TripGroup>, ITripGroupRepository
    {
        Task ITripGroupRepository.AcquireGuideLockAsync(Guid guideUserId, CancellationToken cancellationToken) => throw new NotSupportedException();
        Task ITripGroupRepository.AcquireRecurrenceRuleLockAsync(Guid recurrenceRuleId, CancellationToken cancellationToken) => throw new NotSupportedException();
        Task ITripGroupRepository.AcquireTripLockAsync(Guid tripId, CancellationToken cancellationToken) => Task.CompletedTask;
        Task<Account?> ITripGroupRepository.GetAccountWithRoleAsync(Guid userId, CancellationToken cancellationToken) => throw new NotSupportedException();
        Task<IReadOnlyList<TourRecurrenceRule>> ITripGroupRepository.GetActiveRecurrenceRulesAsync(Guid? tourId, CancellationToken cancellationToken) => throw new NotSupportedException();
        Task<Guid?> ITripGroupRepository.GetActiveTourRefundPolicyIdAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        Task<IReadOnlyList<Guid>> ITripGroupRepository.GetAdminAccountIdsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        Task<Guid?> ITripGroupRepository.GetBookingTripIdAsync(Guid orderId, CancellationToken cancellationToken) => throw new NotSupportedException();
        Task<Order?> ITripGroupRepository.GetBookingWithOrderAsync(Guid orderId, CancellationToken cancellationToken) => throw new NotSupportedException();
        Task<Guid?> ITripGroupRepository.GetDefaultManagerUserIdAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        Task<TripGroup?> ITripGroupRepository.GetGuideGroupByIdAsync(Guid groupId, Guid guideUserId, CancellationToken cancellationToken) => throw new NotSupportedException();
        Task<IReadOnlyList<TripGroup>> ITripGroupRepository.GetGuideGroupsAsync(Guid guideUserId, CancellationToken cancellationToken) => throw new NotSupportedException();
        Task<IReadOnlyList<TourRecurrenceRule>> ITripGroupRepository.GetRecurrenceRulesByTourAsync(Guid tourId, CancellationToken cancellationToken) => throw new NotSupportedException();
        Task<Tour?> ITripGroupRepository.GetTourForWriteAsync(Guid tourId, CancellationToken cancellationToken) => throw new NotSupportedException();
        Task<Trip?> ITripGroupRepository.GetTripWorkspaceGraphAsync(Guid tripId, bool asTracking, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Trip? Trip { get; set; }
        public TripBookingOrder? Booking { get; set; }
        public List<TripGroup> Groups { get; } = [];
        public List<BookingParticipant> Participants { get; } = [];

        public Task<Trip?> GetTripAsync(Guid tripId, CancellationToken cancellationToken = default) => Task.FromResult(Trip);
        public Task<IReadOnlyList<TripGroup>> GetGroupsWithDetailsByTripAsync(Guid tripId, CancellationToken cancellationToken = default)
        {
            foreach (var g in Groups)
                g.BookingParticipants = Participants.Where(p => p.TripGroupId == g.TripGroupId).ToList();
            return Task.FromResult<IReadOnlyList<TripGroup>>(Groups);
        }
        public Task<IReadOnlyList<TripGroup>> GetGroupsForUpdateByTripAsync(Guid tripId, CancellationToken cancellationToken = default)
        {
            foreach (var g in Groups)
                g.BookingParticipants = Participants.Where(p => p.TripGroupId == g.TripGroupId).ToList();
            return Task.FromResult<IReadOnlyList<TripGroup>>(Groups);
        }
        public Task<TripGroup?> GetGroupForUpdateAsync(Guid tripId, Guid groupId, CancellationToken cancellationToken = default)
            => Task.FromResult(Groups.FirstOrDefault(g => g.TripGroupId == groupId));
        public Task<bool> GroupNameExistsInTripAsync(Guid tripId, string groupName, Guid? excludeGroupId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(Groups.Any(g => g.GroupName.Equals(groupName, StringComparison.OrdinalIgnoreCase) && g.TripGroupId != excludeGroupId));
        public List<Account> Accounts { get; } = [];
        public Task<Account?> GetGuideWithRoleAndProfileAsync(Guid guideUserId, CancellationToken cancellationToken = default)
            => Task.FromResult(Accounts.FirstOrDefault(a => a.UserId == guideUserId));
        public Task<bool> HasOverlappingGuideAssignmentAsync(Guid guideUserId, Guid currentTripId, DateTimeOffset startAt, DateTimeOffset endAt, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
        public Task<IReadOnlyList<BookingParticipant>> GetActiveParticipantsWithOrderByTripAsync(Guid tripId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<BookingParticipant>>(Participants);
        public Task<IReadOnlyList<BookingParticipant>> GetActiveParticipantsForUpdateByTripAsync(Guid tripId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<BookingParticipant>>(Participants);
        public Task<BookingParticipant?> GetParticipantForUpdateAsync(Guid tripId, Guid participantId, CancellationToken cancellationToken = default)
            => Task.FromResult(Participants.FirstOrDefault(p => p.ParticipantId == participantId));
        public Task<int> GetGroupParticipantCountAsync(Guid groupId, CancellationToken cancellationToken = default)
            => Task.FromResult(Participants.Count(p => p.TripGroupId == groupId));
        public Task<TripBookingOrder?> GetBookingOrderForUpdateAsync(Guid tripId, Guid bookingId, CancellationToken cancellationToken = default)
            => Task.FromResult(Booking);
        public Task<IReadOnlyList<TripGroup>> GetGuideAssignedGroupsWithTripAndLocationAsync(Guid guideUserId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TripGroup>>(Groups.Where(g => g.GuideUserId == guideUserId).ToList());
        public Task<TripGroup?> GetGuideGroupForTripAsync(Guid tripId, Guid guideUserId, CancellationToken cancellationToken = default)
            => Task.FromResult(Groups.FirstOrDefault(g => g.TripId == tripId && g.GuideUserId == guideUserId));
    }

    private sealed class FakeBookingParticipantRepo : BaseFakeGenericRepo<BookingParticipant>, IBookingParticipantRepository
    {
        public BookingParticipant? Participant { get; set; }
        public Task<BookingParticipant?> GetParticipantForGuideAsync(Guid participantId, CancellationToken cancellationToken = default)
            => Task.FromResult(Participant);
    }

    private sealed class FakeActivityAttendanceRepo(FakeTripRepo tripRepo) : BaseFakeGenericRepo<ActivityAttendance>, IActivityAttendanceRepository
    {
        public Trip? Trip { get; set; }
        public List<BookingParticipant> Participants { get; } = [];
        public List<ActivityAttendance> Attendances { get; } = [];

        public override Task AddAsync(ActivityAttendance entity, CancellationToken cancellationToken = default)
        {
            Attendances.Add(entity);
            return Task.CompletedTask;
        }

        public Task<Trip?> GetTripWithTourAndActivitiesAsync(Guid tripId, CancellationToken cancellationToken = default) => Task.FromResult(Trip);
        public Task<IReadOnlyList<BookingParticipant>> GetTripParticipantsWithGroupsAsync(Guid tripId, Guid? groupId = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<BookingParticipant>>(Participants);
        public Task<IReadOnlyList<ActivityAttendance>> GetAttendancesByTripAsync(Guid tripId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ActivityAttendance>>(Attendances);
        public Task<IReadOnlyList<ActivityAttendance>> GetAttendancesForUpdateAsync(Guid activityId, IReadOnlyCollection<Guid> participantIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ActivityAttendance>>(Attendances.Where(a => a.TourActivityId == activityId && participantIds.Contains(a.ParticipantId)).ToList());
        public Task<IReadOnlyList<AuditLog>> GetAttendanceAuditHistoryAsync(Guid tripId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AuditLog>>(tripRepo.AddedAuditLogs);
        public Task<bool> ExistsAuditLogByBatchIdAsync(Guid tripId, string clientBatchId, CancellationToken cancellationToken = default)
        {
            var marker = $"[BatchId:{clientBatchId.Trim()}]";
            return Task.FromResult(tripRepo.AddedAuditLogs.Any(l => l.EntityId == tripId && l.Details.Contains(marker)));
        }
    }
}
