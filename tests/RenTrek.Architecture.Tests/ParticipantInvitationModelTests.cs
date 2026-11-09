using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using RenTrek.DAL.Data;

namespace RenTrek.Architecture.Tests;

public sealed class ParticipantInvitationModelTests
{
    [Fact]
    public void ParticipantInvitationPreservesDatabaseColumnsAndConstraints()
    {
        using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql().Options);
        var entity = context.GetService<IDesignTimeModel>().Model
            .FindEntityType("RenTrek.DAL.Entities.ParticipantInvitation");

        Assert.NotNull(entity);
        Assert.Equal("participant_invitations", entity.GetTableName());
        var table = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
        var columns = entity.GetProperties().Select(property =>
            $"{property.GetColumnName(table)}:{property.GetColumnType()}:{property.IsNullable}").Order().ToArray();
        string[] expectedColumns =
        [
            "booking_order_id:uuid:False",
            "created_at:timestamp with time zone:False",
            "created_by_user_id:uuid:False",
            "expires_at:timestamp with time zone:False",
            "failed_attempts:integer:False",
            "participant_id:uuid:False",
            "participant_invitation_id:uuid:False",
            "redeemed_at:timestamp with time zone:True",
            "redeemed_by_user_id:uuid:True",
            "revoked_at:timestamp with time zone:True",
            "token_hash:character varying(64):False"
        ];
        Assert.Equal(expectedColumns, columns);

        var key = entity.FindPrimaryKey()!;
        Assert.Equal("participant_invitations_pkey", key.GetName());
        Assert.Equal("ParticipantInvitationId", Assert.Single(key.Properties).Name);
        Assert.Equal(ValueGenerated.Never, key.Properties[0].ValueGenerated);
        Assert.Equal("now()", entity.FindProperty("CreatedAt")!.GetDefaultValueSql());
        Assert.Equal(0, entity.FindProperty("FailedAttempts")!.GetDefaultValue());
        Assert.Equal(typeof(DateTimeOffset?), entity.FindProperty("RevokedAt")!.ClrType);

        var indexes = entity.GetIndexes().ToDictionary(index => index.GetDatabaseName()!);
        var bookingIndex = indexes["ix_participant_invitations_booking_order_id"];
        Assert.Equal("BookingOrderId", Assert.Single(bookingIndex.Properties).Name);
        Assert.False(bookingIndex.IsUnique);
        var tokenIndex = indexes["uq_participant_invitations_token_hash"];
        Assert.Equal("TokenHash", Assert.Single(tokenIndex.Properties).Name);
        Assert.True(tokenIndex.IsUnique);
        var activeIndex = indexes["ux_participant_invitations_active_participant"];
        Assert.Equal("ParticipantId", Assert.Single(activeIndex.Properties).Name);
        Assert.True(activeIndex.IsUnique);
        Assert.Equal("((redeemed_at IS NULL) AND (revoked_at IS NULL))", activeIndex.GetFilter());

        var foreignKeys = entity.GetForeignKeys().Select(foreignKey =>
            $"{foreignKey.GetConstraintName()}:{Assert.Single(foreignKey.Properties).Name}:" +
            $"{foreignKey.PrincipalEntityType.GetTableName()}:{Assert.Single(foreignKey.PrincipalKey.Properties).GetColumnName()}:" +
            $"{foreignKey.DeleteBehavior}").Order().ToArray();
        string[] expectedForeignKeys =
        [
            "fk_participant_invitations_booking_order:BookingOrderId:trip_booking_orders:order_id:Cascade",
            "fk_participant_invitations_created_by:CreatedByUserId:accounts:user_id:Restrict",
            "fk_participant_invitations_participant:ParticipantId:booking_participants:participant_id:Cascade",
            "fk_participant_invitations_redeemed_by:RedeemedByUserId:accounts:user_id:Restrict"
        ];
        Assert.Equal(expectedForeignKeys, foreignKeys);

        var checks = entity.GetCheckConstraints().ToDictionary(check => check.Name!, check => check.Sql);
        Assert.Equal(4, checks.Count);
        Assert.Equal("expires_at > created_at", checks["ck_participant_invitations_expiry"]);
        Assert.Equal("failed_attempts >= 0", checks["ck_participant_invitations_failed_attempts"]);
        Assert.Equal("((redeemed_at IS NULL) AND (redeemed_by_user_id IS NULL)) OR ((redeemed_at IS NOT NULL) AND (redeemed_by_user_id IS NOT NULL))",
            checks["ck_participant_invitations_redemption"]);
        Assert.Equal("length(token_hash::text) = 64", checks["ck_participant_invitations_token_hash_length"]);
        Assert.NotNull(typeof(AppDbContext).GetProperty("ParticipantInvitations"));
    }
}
