using System.Collections.Generic;
using System.Threading.Tasks;
using MedicalApp.API.DTOs.Community;
using MedicalApp.API.Helpers;

namespace MedicalApp.API.Services.Interfaces
{
    public interface ICommunityService
    {
        Task<ApiResponse<CommunityPostDto>> CreatePostAsync(int userId, CreatePostDto dto);
        Task<ApiResponse<List<CommunityPostDto>>> GetPostsAsync(string? specialization = null, string? search = null);
        Task<ApiResponse<CommunityCommentDto>> CreateCommentAsync(int postId, int userId, CreateCommentDto dto);
        Task<ApiResponse<List<CommunityCommentDto>>> GetPostCommentsAsync(int postId);
        Task<ApiResponse<bool>> DeletePostAsync(int postId, int userId);
        Task<ApiResponse<bool>> DeleteCommentAsync(int commentId, int userId);
    }
}
