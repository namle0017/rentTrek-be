using System.Text.Json;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Admin.Requests;
using RenTrek.BLL.DTOs.Admin.Responses;
using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Helpers.Interfaces;
using RenTrek.BLL.Services.Interfaces;
using RenTrek.DAL.Entities;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Services.Implementations;

/// <summary>Danh mục, địa điểm, tag và kỹ năng mềm.</summary>
/// <remarks>Logic gốc: TIGER.ER (commit 6cf600b "Task Admin"). Tri Tin chỉ tách từ partial AdminService, không đổi hành vi.</remarks>
public sealed class AdminMasterDataService(
    IUnitOfWork unitOfWork, ICurrentUserService currentUser, TimeProvider timeProvider)
    : AdminServiceBase(unitOfWork, currentUser, timeProvider), IAdminMasterDataService
{
    public async Task<List<AdminCategoryResponse>> GetCategoriesAsync(AdminQueryRequest request, CancellationToken cancellationToken)
    {
        var items = await _unitOfWork.Categories.SearchActiveAsync(request.Search, request.CategoryType, cancellationToken);
        return items.Select(x => new AdminCategoryResponse(x.Id, x.Name, x.CategoryType == "TOUR" ? "Tour" : x.CategoryType == "EQUIPMENT" ? "Equipment" : x.CategoryType == "BUNDLE" ? "Bundle" : x.CategoryType, x.Description, x.Status == "Active", x.CreatedAt)).ToList();
    }

    public Task<Guid> CreateCategoryAsync(CreateCategoryRequest request, CancellationToken cancellationToken) => SaveCategoryAsync(null, request, cancellationToken);

    public Task<Guid> UpdateCategoryAsync(Guid id, UpdateCategoryRequest request, CancellationToken cancellationToken) => SaveCategoryAsync(id, request, cancellationToken);

    private async Task<Guid> SaveCategoryAsync(Guid? id, CreateCategoryRequest request, CancellationToken cancellationToken)
    {
        RequireAdmin();
        var entityId = id ?? Guid.NewGuid();
        await AtomicAsync("master:Categories", async () =>
        {
            var isNew = !id.HasValue;
            var entity = isNew ? new Category { Id = entityId, CreatedAt = Now } :
                ActiveOrNull(await _unitOfWork.Categories.GetByIdAsync(entityId, cancellationToken), x => x.IsDeleted)
                    ?? throw new NotFoundException("Không tìm thấy dữ liệu");
            var before = new { entity.Name };
            entity.Name = request.Name.Trim(); entity.Description = request.Description?.Trim(); entity.Status = request.IsActive ? "Active" : "Inactive";
            if (isNew) { Check(request.CategoryType != null, "categoryType", "Loại danh mục là bắt buộc khi tạo mới"); entity.CategoryType = request.CategoryType!.ToUpperInvariant(); }
            if (await _unitOfWork.Categories.NameExistsAsync(entity.Name, entityId, cancellationToken)) throw new ConflictException("Tên đã tồn tại");

            entity.UpdatedAt = Now;
            if (isNew) await _unitOfWork.Categories.AddAsync(entity, cancellationToken);
            await AuditAsync(isNew ? "Create" : "Update", "Category", entityId, new { before, after = request }, cancellationToken);
        }, cancellationToken);
        return entityId;
    }

    public async Task DeleteCategoryAsync(Guid id, CancellationToken cancellationToken)
    {
        RequireAdmin();
        await AtomicAsync("master:Categories", async () =>
        {
            var entity = ActiveOrNull(await _unitOfWork.Categories.GetByIdAsync(id, cancellationToken), x => x.IsDeleted)
                ?? throw new NotFoundException("Không tìm thấy dữ liệu");

            entity.IsDeleted = true; entity.UpdatedAt = Now;
            await AuditAsync("Delete", "Category", id, new { entity.Name }, cancellationToken);
        }, cancellationToken);
    }

    public async Task<List<AdminLocationResponse>> GetLocationsAsync(AdminQueryRequest request, CancellationToken cancellationToken)
    {
        var items = await _unitOfWork.Locations.SearchActiveAsync(request.Search, cancellationToken);
        return items.Select(x => new AdminLocationResponse(x.Id, x.Name, x.Address, x.Latitude, x.Longitude, x.TourCount, x.CreatedAt)).ToList();
    }

    public Task<Guid> CreateLocationAsync(CreateLocationRequest request, CancellationToken cancellationToken) => SaveLocationAsync(null, request, cancellationToken);

    public Task<Guid> UpdateLocationAsync(Guid id, UpdateLocationRequest request, CancellationToken cancellationToken) => SaveLocationAsync(id, request, cancellationToken);

    private async Task<Guid> SaveLocationAsync(Guid? id, CreateLocationRequest request, CancellationToken cancellationToken)
    {
        RequireAdmin();
        var entityId = id ?? Guid.NewGuid();
        await AtomicAsync("master:Locations", async () =>
        {
            var isNew = !id.HasValue;
            var entity = isNew ? new Location { Id = entityId, CreatedAt = Now } :
                ActiveOrNull(await _unitOfWork.Locations.GetByIdAsync(entityId, cancellationToken), x => x.IsDeleted)
                    ?? throw new NotFoundException("Không tìm thấy dữ liệu");
            var before = new { entity.Name };
            entity.Name = request.Name.Trim(); entity.Address = request.Address.Trim(); entity.Latitude = request.Latitude; entity.Longitude = request.Longitude;
            if (await _unitOfWork.Locations.NameExistsAsync(entity.Name, entityId, cancellationToken)) throw new ConflictException("Tên đã tồn tại");

            entity.UpdatedAt = Now;
            if (isNew) await _unitOfWork.Locations.AddAsync(entity, cancellationToken);
            await AuditAsync(isNew ? "Create" : "Update", "Location", entityId, new { before, after = request }, cancellationToken);
        }, cancellationToken);
        return entityId;
    }

    public async Task DeleteLocationAsync(Guid id, CancellationToken cancellationToken)
    {
        RequireAdmin();
        await AtomicAsync("master:Locations", async () =>
        {
            var entity = ActiveOrNull(await _unitOfWork.Locations.GetByIdAsync(id, cancellationToken), x => x.IsDeleted)
                ?? throw new NotFoundException("Không tìm thấy dữ liệu");
            if (await _unitOfWork.Tours.AnyByMainLocationAsync(id, cancellationToken)) throw new BadRequestException("Địa điểm đang được tour sử dụng", "LOCATION_IN_USE");
            entity.IsDeleted = true; entity.UpdatedAt = Now;
            await AuditAsync("Delete", "Location", id, new { entity.Name }, cancellationToken);
        }, cancellationToken);
    }

    public async Task<List<AdminTagResponse>> GetTagsAsync(AdminQueryRequest request, CancellationToken cancellationToken)
    {
        var items = await _unitOfWork.Tags.SearchActiveAsync(request.Search, cancellationToken);
        return items.Select(x => new AdminTagResponse(x.TagId, x.Name, x.Slug, x.PostCount)).ToList();
    }

    public Task<Guid> CreateTagAsync(CreateTagRequest request, CancellationToken cancellationToken) => SaveTagAsync(null, request, cancellationToken);

    public Task<Guid> UpdateTagAsync(Guid id, UpdateTagRequest request, CancellationToken cancellationToken) => SaveTagAsync(id, request, cancellationToken);

    private async Task<Guid> SaveTagAsync(Guid? id, CreateTagRequest request, CancellationToken cancellationToken)
    {
        RequireAdmin();
        var entityId = id ?? Guid.NewGuid();
        await AtomicAsync("master:Tags", async () =>
        {
            var isNew = !id.HasValue;
            var entity = isNew ? new Tag { TagId = entityId, CreatedAt = Now } :
                ActiveOrNull(await _unitOfWork.Tags.GetByIdAsync(entityId, cancellationToken), x => x.IsDeleted)
                    ?? throw new NotFoundException("Không tìm thấy dữ liệu");
            var before = new { entity.Name };
            entity.Name = request.Name.Trim(); entity.Slug = request.Slug.Trim();
            if (await _unitOfWork.Tags.NameExistsAsync(entity.Name, entityId, cancellationToken)) throw new ConflictException("Tên đã tồn tại");
            if (await _unitOfWork.Tags.SlugExistsAsync(entity.Slug, entityId, cancellationToken)) throw new ConflictException("Slug đã tồn tại");
            entity.UpdatedAt = Now;
            if (isNew) await _unitOfWork.Tags.AddAsync(entity, cancellationToken);
            await AuditAsync(isNew ? "Create" : "Update", "Tag", entityId, new { before, after = request }, cancellationToken);
        }, cancellationToken);
        return entityId;
    }

    public async Task DeleteTagAsync(Guid id, CancellationToken cancellationToken)
    {
        RequireAdmin();
        await AtomicAsync("master:Tags", async () =>
        {
            var entity = ActiveOrNull(await _unitOfWork.Tags.GetByIdAsync(id, cancellationToken), x => x.IsDeleted)
                ?? throw new NotFoundException("Không tìm thấy dữ liệu");

            entity.IsDeleted = true; entity.UpdatedAt = Now;
            await AuditAsync("Delete", "Tag", id, new { entity.Name }, cancellationToken);
        }, cancellationToken);
    }

    public async Task<List<AdminSoftSkillResponse>> GetSoftSkillsAsync(AdminQueryRequest request, CancellationToken cancellationToken)
    {
        var items = await _unitOfWork.SoftSkills.SearchActiveAsync(request.Search, cancellationToken);
        return items.Select(x => new AdminSoftSkillResponse(x.SoftSkillId, x.Name, x.Description)).ToList();
    }

    public Task<Guid> CreateSoftSkillAsync(CreateSoftSkillRequest request, CancellationToken cancellationToken) => SaveSoftSkillAsync(null, request, cancellationToken);

    public Task<Guid> UpdateSoftSkillAsync(Guid id, UpdateSoftSkillRequest request, CancellationToken cancellationToken) => SaveSoftSkillAsync(id, request, cancellationToken);

    private async Task<Guid> SaveSoftSkillAsync(Guid? id, CreateSoftSkillRequest request, CancellationToken cancellationToken)
    {
        RequireAdmin();
        var entityId = id ?? Guid.NewGuid();
        await AtomicAsync("master:SoftSkills", async () =>
        {
            var isNew = !id.HasValue;
            var entity = isNew ? new SoftSkill { SoftSkillId = entityId, CreatedAt = Now } :
                ActiveOrNull(await _unitOfWork.SoftSkills.GetByIdAsync(entityId, cancellationToken), x => x.IsDeleted)
                    ?? throw new NotFoundException("Không tìm thấy dữ liệu");
            var before = new { entity.Name };
            entity.Name = request.Name.Trim(); entity.Description = request.Description?.Trim(); entity.Status = "Active";
            if (await _unitOfWork.SoftSkills.NameExistsAsync(entity.Name, entityId, cancellationToken)) throw new ConflictException("Tên đã tồn tại");

            entity.UpdatedAt = Now;
            if (isNew) await _unitOfWork.SoftSkills.AddAsync(entity, cancellationToken);
            await AuditAsync(isNew ? "Create" : "Update", "SoftSkill", entityId, new { before, after = request }, cancellationToken);
        }, cancellationToken);
        return entityId;
    }

    public async Task DeleteSoftSkillAsync(Guid id, CancellationToken cancellationToken)
    {
        RequireAdmin();
        await AtomicAsync("master:SoftSkills", async () =>
        {
            var entity = ActiveOrNull(await _unitOfWork.SoftSkills.GetByIdAsync(id, cancellationToken), x => x.IsDeleted)
                ?? throw new NotFoundException("Không tìm thấy dữ liệu");

            entity.IsDeleted = true; entity.UpdatedAt = Now;
            await AuditAsync("Delete", "SoftSkill", id, new { entity.Name }, cancellationToken);
        }, cancellationToken);
    }

    // GetByIdAsync có tracking; bản ghi đã xoá mềm coi như không tồn tại
    private static T? ActiveOrNull<T>(T? entity, Func<T, bool> isDeleted) where T : class
        => entity is null || isDeleted(entity) ? null : entity;
}
