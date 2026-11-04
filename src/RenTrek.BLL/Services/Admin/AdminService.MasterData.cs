using Microsoft.EntityFrameworkCore;
using RenTrek.BLL.DTOs.Admin.Requests;
using RenTrek.BLL.DTOs.Admin.Responses;
using RenTrek.BLL.Exceptions;
using RenTrek.DAL.Entities;

namespace RenTrek.BLL.Services.Admin;

public sealed partial class AdminService
{

    public async Task<List<AdminCategoryResponse>> GetCategoriesAsync(AdminQueryRequest request, CancellationToken cancellationToken)
    {
        var query = _unitOfWork.Categories.Query().Where(x => !x.IsDeleted);
        if (!string.IsNullOrWhiteSpace(request.Search)) { var search = request.Search.Trim().ToLower(); query = query.Where(x => x.Name.ToLower().Contains(search)); }
        if (!string.IsNullOrWhiteSpace(request.CategoryType)) query = query.Where(x => x.CategoryType.ToLower() == request.CategoryType.ToLower());
        return await query.OrderBy(x => x.Name).Select(x => new AdminCategoryResponse(x.Id, x.Name, x.CategoryType == "TOUR" ? "Tour" : x.CategoryType == "EQUIPMENT" ? "Equipment" : x.CategoryType == "BUNDLE" ? "Bundle" : x.CategoryType, x.Description, x.Status == "Active", x.CreatedAt)).ToListAsync(cancellationToken);
    }

    public async Task<Guid> SaveCategoryAsync(Guid? id, CategoryWriteRequest request, CancellationToken cancellationToken)
    {
        RequireAdmin();
        var entityId = id ?? Guid.NewGuid();
        await AtomicAsync("master:Categories", async () =>
        {
            var isNew = !id.HasValue;
            var entity = isNew ? new Category { Id = entityId, CreatedAt = Now } :
                await _unitOfWork.Categories.QueryTracking().SingleOrDefaultAsync(x => x.Id == entityId && !x.IsDeleted, cancellationToken)
                    ?? throw new NotFoundException("Không tìm thấy dữ liệu");
            var before = new { entity.Name };
            entity.Name = Required(request.Name, "name", 100); entity.Description = request.Description?.Trim(); entity.Status = request.IsActive ? "Active" : "Inactive"; if (isNew) entity.CategoryType = Choice(request.CategoryType, "categoryType", "Tour", "Equipment", "Bundle").ToUpperInvariant();
            if (await _unitOfWork.Categories.Query().AnyAsync(x => !x.IsDeleted && x.Id != entityId && x.Name.ToLower() == entity.Name.ToLower(), cancellationToken)) throw new ConflictException("Tên đã tồn tại");
            
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
            var entity = await _unitOfWork.Categories.QueryTracking().SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy dữ liệu");
            
            entity.IsDeleted = true; entity.UpdatedAt = Now;
            await AuditAsync("Delete", "Category", id, new { entity.Name }, cancellationToken);
        }, cancellationToken);
    }

    public async Task<List<AdminLocationResponse>> GetLocationsAsync(AdminQueryRequest request, CancellationToken cancellationToken)
    {
        var query = _unitOfWork.Locations.Query().Where(x => !x.IsDeleted);
        if (!string.IsNullOrWhiteSpace(request.Search)) { var search = request.Search.Trim().ToLower(); query = query.Where(x => x.Name.ToLower().Contains(search)); }
        
        return await query.OrderBy(x => x.Name).Select(x => new AdminLocationResponse(x.Id, x.Name, x.Address, x.Latitude, x.Longitude, x.Tours.Count(t => !t.IsDeleted), x.CreatedAt)).ToListAsync(cancellationToken);
    }

    public async Task<Guid> SaveLocationAsync(Guid? id, LocationWriteRequest request, CancellationToken cancellationToken)
    {
        RequireAdmin();
        var entityId = id ?? Guid.NewGuid();
        await AtomicAsync("master:Locations", async () =>
        {
            var isNew = !id.HasValue;
            var entity = isNew ? new Location { Id = entityId, CreatedAt = Now } :
                await _unitOfWork.Locations.QueryTracking().SingleOrDefaultAsync(x => x.Id == entityId && !x.IsDeleted, cancellationToken)
                    ?? throw new NotFoundException("Không tìm thấy dữ liệu");
            var before = new { entity.Name };
            entity.Name = Required(request.Name, "name", 200); entity.Address = Required(request.Address, "address", 500); Check(request.Latitude is >= -90 and <= 90, "latitude", "Vĩ độ không hợp lệ"); Check(request.Longitude is >= -180 and <= 180, "longitude", "Kinh độ không hợp lệ"); entity.Latitude = request.Latitude; entity.Longitude = request.Longitude;
            if (await _unitOfWork.Locations.Query().AnyAsync(x => !x.IsDeleted && x.Id != entityId && x.Name.ToLower() == entity.Name.ToLower(), cancellationToken)) throw new ConflictException("Tên đã tồn tại");
            
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
            var entity = await _unitOfWork.Locations.QueryTracking().SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy dữ liệu");
            if (await _unitOfWork.Tours.Query().AnyAsync(x => x.MainLocationId == id, cancellationToken)) throw new BadRequestException("Địa điểm đang được tour sử dụng", "LOCATION_IN_USE");
            entity.IsDeleted = true; entity.UpdatedAt = Now;
            await AuditAsync("Delete", "Location", id, new { entity.Name }, cancellationToken);
        }, cancellationToken);
    }

    public async Task<List<AdminTagResponse>> GetTagsAsync(AdminQueryRequest request, CancellationToken cancellationToken)
    {
        var query = _unitOfWork.Tags.Query().Where(x => !x.IsDeleted);
        if (!string.IsNullOrWhiteSpace(request.Search)) { var search = request.Search.Trim().ToLower(); query = query.Where(x => x.Name.ToLower().Contains(search)); }
        
        return await query.OrderBy(x => x.Name).Select(x => new AdminTagResponse(x.TagId, x.Name, x.Slug, x.PostTags.Count(t => !t.Post.IsDeleted))).ToListAsync(cancellationToken);
    }

    public async Task<Guid> SaveTagAsync(Guid? id, TagWriteRequest request, CancellationToken cancellationToken)
    {
        RequireAdmin();
        var entityId = id ?? Guid.NewGuid();
        await AtomicAsync("master:Tags", async () =>
        {
            var isNew = !id.HasValue;
            var entity = isNew ? new Tag { TagId = entityId, CreatedAt = Now } :
                await _unitOfWork.Tags.QueryTracking().SingleOrDefaultAsync(x => x.TagId == entityId && !x.IsDeleted, cancellationToken)
                    ?? throw new NotFoundException("Không tìm thấy dữ liệu");
            var before = new { entity.Name };
            entity.Name = Required(request.Name, "name", 100); entity.Slug = Required(request.Slug, "slug", 200); Check(System.Text.RegularExpressions.Regex.IsMatch(entity.Slug, "^[a-z0-9]+(?:-[a-z0-9]+)*$"), "slug", "Slug chỉ gồm chữ thường, số và dấu gạch ngang");
            if (await _unitOfWork.Tags.Query().AnyAsync(x => !x.IsDeleted && x.TagId != entityId && x.Name.ToLower() == entity.Name.ToLower(), cancellationToken)) throw new ConflictException("Tên đã tồn tại");
            if (await _unitOfWork.Tags.Query().AnyAsync(x => x.TagId != entityId && x.Slug == entity.Slug, cancellationToken)) throw new ConflictException("Slug đã tồn tại");
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
            var entity = await _unitOfWork.Tags.QueryTracking().SingleOrDefaultAsync(x => x.TagId == id && !x.IsDeleted, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy dữ liệu");
            
            entity.IsDeleted = true; entity.UpdatedAt = Now;
            await AuditAsync("Delete", "Tag", id, new { entity.Name }, cancellationToken);
        }, cancellationToken);
    }

    public async Task<List<AdminSoftSkillResponse>> GetSoftSkillsAsync(AdminQueryRequest request, CancellationToken cancellationToken)
    {
        var query = _unitOfWork.SoftSkills.Query().Where(x => !x.IsDeleted);
        if (!string.IsNullOrWhiteSpace(request.Search)) { var search = request.Search.Trim().ToLower(); query = query.Where(x => x.Name.ToLower().Contains(search)); }
        
        return await query.OrderBy(x => x.Name).Select(x => new AdminSoftSkillResponse(x.SoftSkillId, x.Name, x.Description)).ToListAsync(cancellationToken);
    }

    public async Task<Guid> SaveSoftSkillAsync(Guid? id, SoftSkillWriteRequest request, CancellationToken cancellationToken)
    {
        RequireAdmin();
        var entityId = id ?? Guid.NewGuid();
        await AtomicAsync("master:SoftSkills", async () =>
        {
            var isNew = !id.HasValue;
            var entity = isNew ? new SoftSkill { SoftSkillId = entityId, CreatedAt = Now } :
                await _unitOfWork.SoftSkills.QueryTracking().SingleOrDefaultAsync(x => x.SoftSkillId == entityId && !x.IsDeleted, cancellationToken)
                    ?? throw new NotFoundException("Không tìm thấy dữ liệu");
            var before = new { entity.Name };
            entity.Name = Required(request.Name, "name", 100); entity.Description = request.Description?.Trim(); entity.Status = "Active";
            if (await _unitOfWork.SoftSkills.Query().AnyAsync(x => !x.IsDeleted && x.SoftSkillId != entityId && x.Name.ToLower() == entity.Name.ToLower(), cancellationToken)) throw new ConflictException("Tên đã tồn tại");
            
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
            var entity = await _unitOfWork.SoftSkills.QueryTracking().SingleOrDefaultAsync(x => x.SoftSkillId == id && !x.IsDeleted, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy dữ liệu");
            
            entity.IsDeleted = true; entity.UpdatedAt = Now;
            await AuditAsync("Delete", "SoftSkill", id, new { entity.Name }, cancellationToken);
        }, cancellationToken);
    }
}
