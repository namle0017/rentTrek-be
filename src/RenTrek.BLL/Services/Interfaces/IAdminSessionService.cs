namespace RenTrek.BLL.Services.Interfaces;

public interface IAdminSessionService
{
    Task<bool> IsSessionValidAsync(Guid userId, string jti, string role, CancellationToken cancellationToken = default);
}
