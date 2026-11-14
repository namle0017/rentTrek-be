using RenTrek.DAL.Entities;

namespace RenTrek.DAL.Repositories.Interfaces;

public interface IRentalEvidencePhotoRepository : IGenericRepository<RentalEvidencePhoto>
{
    Task<IReadOnlyList<RentalEvidencePhoto>> GetByOrderAsync(Guid rentalOrderId, CancellationToken cancellationToken = default);

    /// <summary>Số ảnh (chưa xoá) theo từng phân bổ tài sản ở một giai đoạn.</summary>
    Task<Dictionary<Guid, int>> CountByAssignmentAsync(Guid rentalOrderId, string stage, CancellationToken cancellationToken = default);
}
