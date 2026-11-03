using RenTrek.BLL.DTOs.Common.Responses;
using RenTrek.BLL.DTOs.Post.Requests;
using RenTrek.BLL.DTOs.Post.Responses;

namespace RenTrek.BLL.Services.Interfaces;

public interface IPostService
{
    // Công khai (C17, C18)
    Task<PagedResponse<PostListItemResponse>> GetPublishedPostsAsync(PostQueryRequest query, CancellationToken cancellationToken = default);
    Task<PostDetailResponse> GetPublishedPostBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task<PostTaxonomyResponse> GetTaxonomyAsync(CancellationToken cancellationToken = default);

    // Vận hành (O32)
    Task<PagedResponse<AdminPostListItemResponse>> GetAdminPostsAsync(AdminPostQueryRequest query, CancellationToken cancellationToken = default);
    Task<AdminPostDetailResponse> GetAdminPostByIdAsync(Guid postId, CancellationToken cancellationToken = default);
    Task<AdminPostDetailResponse> CreateAsync(UpsertPostRequest request, CancellationToken cancellationToken = default);
    Task<AdminPostDetailResponse> UpdateAsync(Guid postId, UpsertPostRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid postId, CancellationToken cancellationToken = default);
}
