using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RenTrek.DAL.Data;
using RenTrek.DAL.Repositories.Implementations;
using RenTrek.DAL.Repositories.Interfaces;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.DAL;

public static class DependencyInjection
{
    public static IServiceCollection AddDataAccessLayer(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Thiếu ConnectionStrings:DefaultConnection trong cấu hình.");

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString, sql =>
                sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));

        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IAccessTokenRepository, AccessTokenRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWorks.UnitOfWork>();

        // Trip booking
        services.AddScoped<ITripBookingRepository, TripBookingRepository>();
        services.AddScoped<RenTrek.DAL.UnitOfWork.IUnitOfWork, RenTrek.DAL.UnitOfWork.UnitOfWork>();

        return services;
    }
}
