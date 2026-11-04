using System.Linq.Expressions;
using RenTrek.BLL.DTOs.Warehouse.Requests;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Services.Warehouse.Implementations;
using RenTrek.DAL.Entities;
using RenTrek.DAL.Models;
using RenTrek.DAL.Repositories.Interfaces;
using RenTrek.DAL.UnitOfWorks;
using Xunit;

namespace RenTrek.BLL.Tests.Warehouse;

public class FulfillmentServiceTests
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

    private class FakeRentalOrderRepo : FakeGenericRepo<RentalOrder>, IRentalOrderRepository
    {
        Task IRentalOrderRepository.AcquireFulfillmentLockAsync(Guid orderId, CancellationToken cancellationToken) => throw new NotSupportedException();
        Task<RentalOrderDetailRow?> IRentalOrderRepository.GetDetailAsync(Guid orderId, CancellationToken cancellationToken) => throw new NotSupportedException();
        Task<Order?> IRentalOrderRepository.GetForFulfillmentAsync(Guid orderId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public RentalOrder? ReturnOrder { get; set; }
        public Task<RentalOrder?> GetByOrderCodeWithFulfillmentAsync(string orderCode, CancellationToken cancellationToken = default)
            => Task.FromResult(ReturnOrder);

        public Task AcquireIdempotencyLockAsync(Guid customerUserId, string idempotencyKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<Order?> GetByIdempotencyKeyAsync(Guid customerUserId, string idempotencyKey, CancellationToken cancellationToken = default) => Task.FromResult<Order?>(null);
        public Task<RentalOrderDetailRow?> GetOwnedDetailAsync(Guid orderId, Guid customerUserId, CancellationToken cancellationToken = default) => Task.FromResult<RentalOrderDetailRow?>(null);
        public Task<bool> OrderCodeExistsAsync(string orderCode, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task AddOrderAsync(Order order, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private class FakeRentalChargeRepo : FakeGenericRepo<RentalCharge>, IRentalChargeRepository
    {
        public List<RentalCharge> ReturnCharges { get; set; } = [];
        public RentalCharge? ReturnCharge { get; set; }

        public Task<List<RentalCharge>> GetChargesByOrderCodeAsync(string orderCode, CancellationToken cancellationToken = default)
            => Task.FromResult(ReturnCharges);

        public Task<RentalCharge?> GetChargeByIdWithDetailsAsync(Guid chargeId, CancellationToken cancellationToken = default)
            => Task.FromResult(ReturnCharge);
    }

    private class FakeAssetRepo : FakeGenericRepo<EquipmentAsset>, IEquipmentAssetRepository
    {
        public EquipmentAsset? ReturnAsset { get; set; }
        public Task<EquipmentAsset?> GetByCodeWithDetailsAsync(string assetCode, CancellationToken cancellationToken = default)
            => Task.FromResult(ReturnAsset);

        public Task<EquipmentAsset?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<EquipmentAsset?>(null);
        public Task<string> GenerateNextAssetCodeAsync(string prefix, CancellationToken cancellationToken = default) => Task.FromResult($"{prefix}0001");

        public Task<Dictionary<Guid, int>> GetAvailableCountsAsync(IReadOnlyCollection<Guid> variantIds, AssetAvailabilityFilter filter, CancellationToken cancellationToken = default)
            => Task.FromResult(new Dictionary<Guid, int>());

        public Task<List<Guid>> GetAvailableAssetIdsAsync(Guid variantId, AssetAvailabilityFilter filter, int take, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<Guid>());

        public Task AcquireVariantLocksAsync(IEnumerable<Guid> variantIds, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<(IReadOnlyList<EquipmentAsset> Items, int TotalCount)> GetPagedAssetsAsync(
            string? search, Guid? productVariantId, string? assetStatus, int pageNumber, int pageSize, string sortBy, bool isDescending, CancellationToken cancellationToken = default)
            => Task.FromResult< (IReadOnlyList<EquipmentAsset>, int) >((new List<EquipmentAsset>(), 0));

        public Task<IReadOnlyList<EquipmentAsset>> GetAssetsForGanttAsync(
            DateTime from, DateTime to, Guid? categoryId, Guid? productVariantId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<EquipmentAsset>>(new List<EquipmentAsset>());
    }

    private class FakeFulfillmentUow : IUnitOfWork
    {
        IRefundCaseRepository IUnitOfWork.RefundCases => throw new NotSupportedException();

        public FakeRentalOrderRepo RentalOrdersRepo { get; } = new();
        public FakeRentalChargeRepo RentalChargesRepo { get; } = new();
        public FakeAssetRepo EquipmentAssetsRepo { get; } = new();
        public FakeGenericRepo<AssetAssignment> AssetAssignmentsRepo { get; } = new();

        public IRentalOrderRepository RentalOrders => RentalOrdersRepo;
        public IRentalChargeRepository RentalCharges => RentalChargesRepo;
        public IEquipmentAssetRepository EquipmentAssets => EquipmentAssetsRepo;
        public IAssetAssignmentRepository AssetAssignments => throw new NotImplementedException();

        public IAccessTokenRepository AccessTokens => throw new NotImplementedException();
        public IAccountRepository Accounts => throw new NotImplementedException();
        public IActivityAttendanceRepository ActivityAttendances => throw new NotImplementedException();
        public IAppliedPromotionRepository AppliedPromotions => throw new NotImplementedException();
        public IAuditLogRepository AuditLogs => throw new NotImplementedException();
        public IBookingParticipantRepository BookingParticipants => throw new NotImplementedException();
        public ICategoryRepository Categories => throw new NotImplementedException();
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
        public ITripRepository Trips => throw new NotImplementedException();
        public ITripBookingOrderRepository TripBookingOrders => throw new NotImplementedException();
        public ITripBookingRepository TripBookings => throw new NotImplementedException();
        public ITripGroupRepository TripGroups => throw new NotImplementedException();
        public ITripProvidedItemRepository TripProvidedItems => throw new NotImplementedException();
        public IUserExternalLoginRepository UserExternalLogins => throw new NotImplementedException();

        public IGenericRepository<TEntity> Repository<TEntity>() where TEntity : class => throw new NotImplementedException();
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task BeginTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Dispose() { }
    }

    [Fact]
    public async Task GetHandoverChecklist_ThrowsNotFound_WhenOrderNotFound()
    {
        var uow = new FakeFulfillmentUow();
        uow.RentalOrdersRepo.ReturnOrder = null;

        var service = new FulfillmentService(uow);

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetHandoverChecklistAsync("ORD-INVALID-999"));
    }

    [Fact]
    public async Task CompleteHandover_ThrowsValidation_WhenNotAllItemsScannedAndNoNote()
    {
        var uow = new FakeFulfillmentUow();
        var orderId = Guid.NewGuid();
        var rentalOrder = new RentalOrder
        {
            OrderId = orderId,
            Order = new Order { OrderId = orderId, OrderCode = "ORD-TEST-001", Status = "Confirmed" },
            RentalOrderItems =
            [
                new RentalOrderItem
                {
                    RentalOrderItemId = Guid.NewGuid(),
                    Quantity = 2,
                    AssetAssignments = []
                }
            ]
        };

        uow.RentalOrdersRepo.ReturnOrder = rentalOrder;

        var service = new FulfillmentService(uow);

        await Assert.ThrowsAsync<ValidationException>(() => service.CompleteHandoverAsync("ORD-TEST-001", new CompleteHandoverRequest { Note = null }, Guid.NewGuid()));
    }

    [Fact]
    public async Task CompleteRentalOrder_ThrowsValidation_WhenUnpaidChargesExist()
    {
        var uow = new FakeFulfillmentUow();
        var orderId = Guid.NewGuid();
        var rentalOrder = new RentalOrder
        {
            OrderId = orderId,
            Order = new Order { OrderId = orderId, OrderCode = "ORD-TEST-002", Status = "InFulfillment" },
            RentalOrderItems = [],
            RentalCharges =
            [
                new RentalCharge
                {
                    RentalChargeId = Guid.NewGuid(),
                    Amount = 200000m,
                    Status = "Assessed"
                }
            ]
        };

        uow.RentalOrdersRepo.ReturnOrder = rentalOrder;

        var service = new FulfillmentService(uow);

        await Assert.ThrowsAsync<ValidationException>(() => service.CompleteRentalOrderAsync("ORD-TEST-002"));
    }
}
