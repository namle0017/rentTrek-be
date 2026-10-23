using Microsoft.EntityFrameworkCore;
using RenTrek.BLL.DTOs.Users;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Entities;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Services.Implementations;

public class UserService : IUserService
{
    private readonly IUnitOfWork _unitOfWork;

    public UserService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<UserDetailResponse> GetProfileAsync(long userId, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Repository<User>()
            .Query()
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

        if (user is null)
        {
            throw new NotFoundException($"Không tìm thấy người dùng với Id '{userId}'");
        }

        return MapToDetailResponse(user);
    }

    public async Task<UserDetailResponse> UpdateProfileAsync(long userId, UpdateProfileRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Repository<User>()
            .QueryTracking()
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

        if (user is null)
        {
            throw new NotFoundException($"Không tìm thấy người dùng với Id '{userId}'");
        }

        user.FullName = request.FullName.Trim();
        user.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        user.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.Repository<User>().Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDetailResponse(user);
    }

    private static UserDetailResponse MapToDetailResponse(User user)
    {
        return new UserDetailResponse
        {
            UserId = user.UserId,
            Email = user.Email,
            FullName = user.FullName,
            Phone = user.Phone,
            Role = user.Role?.RoleName ?? "Customer",
            Status = user.Status,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }
}
