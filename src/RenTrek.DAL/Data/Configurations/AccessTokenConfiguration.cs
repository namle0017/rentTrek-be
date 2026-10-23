using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Data.Configurations;

public class AccessTokenConfiguration : IEntityTypeConfiguration<AccessToken>
{
    public void Configure(EntityTypeBuilder<AccessToken> builder)
    {
        // RefreshToken already cascades from User; avoid a second cascade path on SQL Server.
        builder.HasOne(token => token.User)
            .WithMany(user => user.AccessTokens)
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
