namespace RenTrek.BLL.Services.Admin;

public interface IAdminSessionService
{
    Task<bool> IsSessionValidAsync(Guid userId, string jti, string role, CancellationToken cancellationToken = default);
}
