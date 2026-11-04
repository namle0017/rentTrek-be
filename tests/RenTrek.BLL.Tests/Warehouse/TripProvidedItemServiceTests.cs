using System.Linq.Expressions;
using RenTrek.BLL.DTOs.Warehouse.Requests;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Services.Warehouse.Implementations;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Repositories.Interfaces;
using RenTrek.DAL.UnitOfWorks;
using Xunit;

namespace RenTrek.BLL.Tests.Warehouse;

public class TripProvidedItemServiceTests
{
    private class FakeGenericRepo<T> : IGenericRepository<T> where T : class
    {
        public Task<T?> GetByIdAsync(object id, CancellationToken cancellationToken = default) => Task.FromResult<T?>(null);
        public IQueryable<T> Query() => new List<T>().AsQueryable();
        public IQueryable<T> QueryTracking() => new List<T>().AsQueryable();
        public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task AddAsync(T entity, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Update(T entity) { }
        public void Remove(T entity) { }
        public void RemoveRange(IEnumerable<T> entities) { }
    }

    private class FakeTripRepo : FakeGenericRepo<Trip>, ITripRepository
    {
        public Trip? ReturnTrip { get; set; }
        public Task<Trip?> GetTripWithTourAndRequirementsAsync(Guid tripId, CancellationToken cancellationToken = default)
            => Task.FromResult(ReturnTrip);
    }

    private class FakeAssignmentRepo : FakeGenericRepo<AssetAssignment>, IAssetAssignmentRepository
    {
        public AssetAssignment? ReturnAssignment { get; set; }
        public List<AssetAssignment> ReturnAssignmentsList { get; set; } = [];

        public Task<AssetAssignment?> GetAssignmentWithAssetAndTripItemAsync(Guid assignmentId, CancellationToken cancellationToken = default)
            => Task.FromResult(ReturnAssignment);

        public Task<List<AssetAssignment>> GetAssignmentsByTripIdAsync(Guid tripId, CancellationToken cancellationToken = default)
            => Task.FromResult(ReturnAssignmentsList);

        public Task<List<AssetAssignment>> GetAssignmentsByAssetCodesAndTripIdAsync(Guid tripId, IEnumerable<string> assetCodes, CancellationToken cancellationToken = default)
            => Task.FromResult(ReturnAssignmentsList);
    }

    private class FakeTripProvidedItemRepo : FakeGenericRepo<TripProvidedItem>, ITripProvidedItemRepository
    {
        public List<TripProvidedItem> ReturnItems { get; set; } = [];
        public TripProvidedItem? ReturnItem { get; set; }
        public List<EquipmentAsset> ReturnAvailableAssets { get; set; } = [];

        public Task<List<TripProvidedItem>> GetByTripIdWithDetailsAsync(Guid tripId, CancellationToken cancellationToken = default)
            => Task.FromResult(ReturnItems);

        public Task<TripProvidedItem?> GetByIdWithDetailsAsync(Guid tripId, Guid itemId, CancellationToken cancellationToken = default)
            => Task.FromResult(ReturnItem);

        public Task<List<EquipmentAsset>> GetAvailableAssetsForItemAsync(Guid productVariantId, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
            => Task.FromResult(ReturnAvailableAssets);
    }

    private class FakeUnitOfWork : IUnitOfWork
    {
        public FakeTripRepo TripsRepo { get; } = new();
        public FakeTripProvidedItemRepo TripProvidedItemsRepo { get; } = new();
        public FakeAssignmentRepo AssetAssignmentsRepo { get; } = new();

        public ITripRepository Trips => TripsRepo;
        public ITripProvidedItemRepository TripProvidedItems => TripProvidedItemsRepo;
        public IAssetAssignmentRepository AssetAssignments => AssetAssignmentsRepo;

        public IAccessTokenRepository AccessTokens => throw new NotImplementedException();
        public IAccountRepository Accounts => throw new NotImplementedException();
        public IActivityAttendanceRepository ActivityAttendances => throw new NotImplementedException();
        public IAppliedPromotionRepository AppliedPromotions => throw new NotImplementedException();
        public IAuditLogRepository AuditLogs => throw new NotImplementedException();
        public IBookingParticipantRepository BookingParticipants => throw new NotImplementedException();
        public ICategoryRepository Categories => throw new NotImplementedException();
        public IEquipmentAssetRepository EquipmentAssets => throw new NotImplementedException();
        public ILocationRepository Locations => throw new NotImplementedException();
        public INotificationRepository Notifications => throw new NotImplementedException();
        public IOrderRepository Orders => throw new NotImplementedException();
        public IParticipantPostReadRepository ParticipantPostReads => throw new NotImplementedException();
        public IPasswordResetTokenRepository PasswordResetTokens => throw new NotImplementedException();
        public IPaymentRepository Payments => throw new NotImplementedException();
        public IPostRepository Posts => throw new NotImplementedException();
        public IPostSoftSkillRepository PostSoftSkills => throw new NotImplementedException();
        public IPostTagRepository PostTags => throw new NotImplementedException();
        public IProductRepository Products => throw new NotImplementedException();
        public IProductBundleRepository ProductBundles => throw new NotImplementedException();
        public IProductBundleItemRepository ProductBundleItems => throw new NotImplementedException();
        public IProductVariantRepository ProductVariants => throw new NotImplementedException();
        public IProductVariantImageRepository ProductVariantImages => throw new NotImplementedException();
        public IProfileRepository Profiles => throw new NotImplementedException();
        public IPromotionRepository Promotions => throw new NotImplementedException();
        public IPromotionTierRepository PromotionTiers => throw new NotImplementedException();
        public IRefreshTokenRepository RefreshTokens => throw new NotImplementedException();
        public IRefundPolicyRepository RefundPolicies => throw new NotImplementedException();
        public IRefundPolicyRuleRepository RefundPolicyRules => throw new NotImplementedException();
        public IRentalBundleItemSnapshotRepository RentalBundleItemSnapshots => throw new NotImplementedException();
        public IRentalChargeRepository RentalCharges => throw new NotImplementedException();
        public IRentalOrderRepository RentalOrders => throw new NotImplementedException();
        public IRentalOrderItemRepository RentalOrderItems => throw new NotImplementedException();
        public IRentalPriceTierRepository RentalPriceTiers => throw new NotImplementedException();
        public IReviewRepository Reviews => throw new NotImplementedException();
        public IRoleRepository Roles => throw new NotImplementedException();
        public ISoftSkillRepository SoftSkills => throw new NotImplementedException();
        public ISystemConfigurationRepository SystemConfigurations => throw new NotImplementedException();
        public ITagRepository Tags => throw new NotImplementedException();
        public ITourRepository Tours => throw new NotImplementedException();
        public ITourActivityRepository TourActivities => throw new NotImplementedException();
        public ITourImageRepository TourImages => throw new NotImplementedException();
        public ITourPostRepository TourPosts => throw new NotImplementedException();
        public ITourPreparationItemRepository TourPreparationItems => throw new NotImplementedException();
        public ITourProvidedItemRepository TourProvidedItems => throw new NotImplementedException();
        public ITourRecurrenceRuleRepository TourRecurrenceRules => throw new NotImplementedException();
        public ITripBookingOrderRepository TripBookingOrders => throw new NotImplementedException();
        public ITripBookingRepository TripBookings => throw new NotImplementedException();
        public ITripGroupRepository TripGroups => throw new NotImplementedException();
        public IUserExternalLoginRepository UserExternalLogins => throw new NotImplementedException();

        public IGenericRepository<TEntity> Repository<TEntity>() where TEntity : class => throw new NotImplementedException();
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task BeginTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Dispose() { }
    }

    [Fact]
    public async Task GetProvidedItems_ThrowsNotFound_WhenTripDoesNotExist()
    {
        var uow = new FakeUnitOfWork();
        uow.TripsRepo.ReturnTrip = null;

        var service = new TripProvidedItemService(uow);

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetProvidedItemsByTripIdAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task UnassignAsset_ThrowsValidation_WhenAssetIsAlreadyIssued()
    {
        var uow = new FakeUnitOfWork();
        var tripId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var trip = new Trip { TripId = tripId };
        var tripItem = new TripProvidedItem { TripId = tripId, Trip = trip };

        uow.AssetAssignmentsRepo.ReturnAssignment = new AssetAssignment
        {
            AssetAssignmentId = assignmentId,
            Status = "Issued",
            TripProvidedItem = tripItem
        };

        var service = new TripProvidedItemService(uow);

        await Assert.ThrowsAsync<ValidationException>(() => service.UnassignAssetFromTripAsync(tripId, assignmentId));
    }

    [Fact]
    public async Task ReturnTripGear_UpdatesAssetStatusToDamaged_WhenConditionIsDamaged()
    {
        var uow = new FakeUnitOfWork();
        var tripId = Guid.NewGuid();
        var asset = new EquipmentAsset
        {
            EquipmentAssetId = Guid.NewGuid(),
            AssetCode = "TN-PETZL-0001",
            AssetStatus = "Available"
        };

        var assignment = new AssetAssignment
        {
            AssetAssignmentId = Guid.NewGuid(),
            Status = "Issued",
            EquipmentAsset = asset
        };

        uow.AssetAssignmentsRepo.ReturnAssignmentsList = [assignment];
        uow.TripProvidedItemsRepo.ReturnItems = [];

        var service = new TripProvidedItemService(uow);

        var request = new ReturnTripGearRequest
        {
            Returns =
            [
                new TripAssetReturnItemRequest
                {
                    AssetCode = "TN-PETZL-0001",
                    ReturnCondition = "Damaged",
                    Note = "Bị nứt quai đeo"
                }
            ]
        };

        var count = await service.ReturnTripGearAsync(tripId, request);

        Assert.Equal(1, count);
        Assert.Equal("Returned", assignment.Status);
        Assert.Equal("Damaged", asset.AssetStatus);
        Assert.Equal("Bị nứt quai đeo", asset.ConditionNote);
    }
}
