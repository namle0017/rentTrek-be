using RenTrek.DAL.Entities;
using RenTrek.DAL.Models;

namespace RenTrek.DAL.Repositories.Interfaces;

public interface IPostRepository : IGenericRepository<Post>
{
    Task<(IReadOnlyList<PostListRow> Items, int TotalCount)> GetPagedAsync(PostFilter filter, CancellationToken cancellationToken = default);

    /// <summary>Bài Published khác <paramref name="excludePostId"/> có ít nhất một tag trong danh sách, mới nhất trước.</summary>
    Task<List<PostListRow>> GetPublishedByTagsAsync(Guid excludePostId, IReadOnlyCollection<string> tagSlugs, int take, CancellationToken cancellationToken = default);

    /// <summary>Bài chưa xoá kèm tác giả, tag, kỹ năng, tour (gồm cả bản ghi liên kết đã xoá; service tự lọc). Không tracking.</summary>
    Task<Post?> GetDetailAsync(Guid postId, CancellationToken cancellationToken = default);

    /// <summary>Như <see cref="GetDetailAsync"/> nhưng tìm bài Published theo slug.</summary>
    Task<Post?> GetPublishedBySlugAsync(string slug, CancellationToken cancellationToken = default);

    /// <summary>Bài chưa xoá có tracking kèm PostTags, PostSoftSkills, TourPosts để đồng bộ liên kết.</summary>
    Task<Post?> GetForEditAsync(Guid postId, CancellationToken cancellationToken = default);

    /// <summary>Slug đã dùng bởi bài khác (kể cả bài đã xoá mềm, vì unique ở DB).</summary>
    Task<bool> SlugExistsAsync(string slug, Guid excludePostId, CancellationToken cancellationToken = default);

    /// <summary>Người tham gia chưa huỷ của các tour gắn bài, và số người đã đọc bài.</summary>
    Task<(int Total, int Read)> GetReadStatsAsync(Guid postId, CancellationToken cancellationToken = default);
}
