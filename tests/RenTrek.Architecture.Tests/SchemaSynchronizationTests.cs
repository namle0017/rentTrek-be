using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using RenTrek.DAL.Data;
using RenTrek.DAL.Entities;

namespace RenTrek.Architecture.Tests;

public sealed class SchemaSynchronizationTests
{
    [Theory]
    [InlineData("Payment", "ProviderOrderId", "character varying(100)")]
    [InlineData("TourActivity", "StartTime", "time(0) without time zone")]
    [InlineData("TourActivity", "DayTitle", "character varying(255)")]
    [InlineData("TourActivity", "Description", "text")]
    [InlineData("Category", "Name", "character varying(255)")]
    [InlineData("Category", "Description", "text")]
    [InlineData("Location", "Name", "character varying(255)")]
    [InlineData("Product", "Name", "character varying(255)")]
    [InlineData("Product", "Status", "character varying(50)")]
    [InlineData("ProductVariant", "Status", "character varying(50)")]
    [InlineData("ProductVariantImage", "ImageUrl", "text")]
    [InlineData("Tour", "Description", "text")]
    [InlineData("TourImage", "ImageUrl", "text")]
    [InlineData("TourProvidedItem", "ItemName", "character varying(255)")]
    public void ColumnMappingsMatchDatabaseTypes(string entityName, string propertyName, string expectedType)
    {
        using var context = CreateContext();
        var property = FindEntity(context, entityName).FindProperty(propertyName);

        Assert.NotNull(property);
        Assert.Equal(expectedType, property.GetColumnType());
    }

    [Theory]
    [InlineData("Product", "UpdatedAt")]
    [InlineData("ProductVariant", "UpdatedAt")]
    [InlineData("ProductVariantImage", "UpdatedAt")]
    [InlineData("RentalPriceTier", "UpdatedAt")]
    [InlineData("Tour", "CreatedByUserId")]
    [InlineData("TourProvidedItem", "ProductVariantId")]
    public void RequiredDatabaseColumnsRemainRequiredInEf(string entityName, string propertyName)
    {
        using var context = CreateContext();

        Assert.False(FindEntity(context, entityName).FindProperty(propertyName)!.IsNullable);
    }

    [Fact]
    public void DatabaseDefaultsAndConstraintsPreserveApplicationModelBehavior()
    {
        using var context = CreateContext();
        var payment = FindEntity(context, "Payment");
        Assert.Equal("provider_order_id", payment.FindProperty("ProviderOrderId")!.GetColumnName());
        Assert.Equal("now()", payment.FindProperty("CreatedAt")!.GetDefaultValueSql());
        Assert.Contains(payment.GetForeignKeys(), key => key.Properties.Single().Name == "OriginalPaymentId"
            && key.PrincipalEntityType == payment && key.DeleteBehavior == DeleteBehavior.NoAction);
        Assert.Contains(payment.GetIndexes(), index => index.GetDatabaseName() == "ux_payments_one_pending_collection"
            && index.IsUnique && index.GetFilter() != null);

        var refundCase = FindEntity(context, "RefundCase");
        Assert.True(refundCase.FindProperty("RowVersion")!.IsConcurrencyToken);
        Assert.Contains(refundCase.GetCheckConstraints(), check => check.Name == "ck_refund_cases_payout_details");
        Assert.Contains(refundCase.GetForeignKeys(), key => key.Properties.Single().Name == "ApprovedByAdminUserId");
        Assert.True(FindEntity(context, "Trip").FindProperty("RowVersion")!.IsConcurrencyToken);
        Assert.NotNull(FindEntity(context, "Order").GetQueryFilter());
        Assert.NotNull(FindEntity(context, "Product").GetQueryFilter());
        Assert.Equal(typeof(string), FindEntity(context, "Product").FindProperty("Status")!.GetTypeMapping().Converter!.ProviderClrType);
        Assert.Equal(typeof(TimeSpan), FindEntity(context, "TourActivity").FindProperty("StartTime")!.ClrType);
        Assert.Null(context.Model.FindEntityType("RenTrek.DAL.Entities.IdempotencyKey"));
    }

    private static AppDbContext CreateContext()
        => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql().Options);

    [Fact]
    public void RefundHistoryCanTrackMultipleCasesForTheSameOrder()
    {
        using var context = CreateContext();
        var order = new Order { OrderId = Guid.NewGuid() };
        var completed = new RefundCase
        {
            RefundCaseId = Guid.NewGuid(), Order = order, OrderId = order.OrderId,
            RefundPaymentId = Guid.NewGuid(), Status = "Completed"
        };
        var pending = new RefundCase
        {
            RefundCaseId = Guid.NewGuid(), Order = order, OrderId = order.OrderId,
            RefundPaymentId = Guid.NewGuid(), Status = "PendingApproval"
        };

        context.AttachRange(completed, pending);
        context.ChangeTracker.DetectChanges();

        Assert.Same(order, completed.Order);
        Assert.Same(order, pending.Order);
        Assert.Equal(order.OrderId, completed.OrderId);
        Assert.Equal(EntityState.Unchanged, context.Entry(completed).State);
        var entity = FindEntity(context, "RefundCase");
        Assert.False(entity.GetForeignKeys().Single(key => key.Properties.Single().Name == "OrderId").IsUnique);
        Assert.Contains(entity.GetIndexes(), index => index.GetDatabaseName() == "ux_refund_cases_open_order"
            && index.IsUnique && index.GetFilter() != null);
    }

    private static IEntityType FindEntity(AppDbContext context, string name)
        => context.GetService<IDesignTimeModel>().Model.FindEntityType($"RenTrek.DAL.Entities.{name}")!;
}
