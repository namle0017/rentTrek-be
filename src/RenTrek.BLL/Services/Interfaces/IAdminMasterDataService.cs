using RenTrek.BLL.DTOs.Admin.Requests;
using RenTrek.BLL.DTOs.Admin.Responses;

namespace RenTrek.BLL.Services.Interfaces;

/// <summary>Danh mục, địa điểm, tag và kỹ năng mềm.</summary>
public interface IAdminMasterDataService
{
    Task<List<AdminCategoryResponse>> GetCategoriesAsync(AdminQueryRequest request, CancellationToken cancellationToken);
    Task<Guid> CreateCategoryAsync(CreateCategoryRequest request, CancellationToken cancellationToken);
    Task<Guid> UpdateCategoryAsync(Guid id, UpdateCategoryRequest request, CancellationToken cancellationToken);
    Task DeleteCategoryAsync(Guid id, CancellationToken cancellationToken);
    Task<List<AdminLocationResponse>> GetLocationsAsync(AdminQueryRequest request, CancellationToken cancellationToken);
    Task<Guid> CreateLocationAsync(CreateLocationRequest request, CancellationToken cancellationToken);
    Task<Guid> UpdateLocationAsync(Guid id, UpdateLocationRequest request, CancellationToken cancellationToken);
    Task DeleteLocationAsync(Guid id, CancellationToken cancellationToken);
    Task<List<AdminTagResponse>> GetTagsAsync(AdminQueryRequest request, CancellationToken cancellationToken);
    Task<Guid> CreateTagAsync(CreateTagRequest request, CancellationToken cancellationToken);
    Task<Guid> UpdateTagAsync(Guid id, UpdateTagRequest request, CancellationToken cancellationToken);
    Task DeleteTagAsync(Guid id, CancellationToken cancellationToken);
    Task<List<AdminSoftSkillResponse>> GetSoftSkillsAsync(AdminQueryRequest request, CancellationToken cancellationToken);
    Task<Guid> CreateSoftSkillAsync(CreateSoftSkillRequest request, CancellationToken cancellationToken);
    Task<Guid> UpdateSoftSkillAsync(Guid id, UpdateSoftSkillRequest request, CancellationToken cancellationToken);
    Task DeleteSoftSkillAsync(Guid id, CancellationToken cancellationToken);
}
