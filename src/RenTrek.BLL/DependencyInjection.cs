using Microsoft.Extensions.DependencyInjection;
using RenTrek.BLL.Abstractions;
using RenTrek.BLL.Security;
using RenTrek.BLL.Services.Implementations;
using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.BLL;

public static class DependencyInjection
{
    public static IServiceCollection AddBusinessLayer(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IParticipantInviteTokenService, ParticipantInviteTokenService>();
        services.AddScoped<ITripBookingService, TripBookingService>();
        return services;
    }
}
