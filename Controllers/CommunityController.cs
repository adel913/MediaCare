using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MedicalApp.API.DTOs.Community;
using MedicalApp.API.Services.Interfaces;

namespace MedicalApp.API.Controllers
{
    [Authorize]
    public class CommunityController : BaseApiController
    {
        private readonly ICommunityService _communityService;

        public CommunityController(ICommunityService communityService)
        {
            _communityService = communityService;
        }

        /// <summary>
        /// Create a new community post/question.
        /// </summary>
        [HttpPost("posts")]
        public async Task<IActionResult> CreatePost([FromBody] CreatePostDto dto)
        {
            var result = await _communityService.CreatePostAsync(GetUserId(), dto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Get all community posts with optional specialization chip filter and search query.
        /// </summary>
        [HttpGet("posts")]
        public async Task<IActionResult> GetPosts([FromQuery] string? specialization, [FromQuery] string? search)
        {
            var result = await _communityService.GetPostsAsync(specialization, search);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("posts/{postId}/comments")]
        public async Task<IActionResult> CreateComment(int postId, [FromBody] CreateCommentDto dto)
        {
            var result = await _communityService.CreateCommentAsync(postId, GetUserId(), dto);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("posts/{postId}/comments")]
        public async Task<IActionResult> GetPostComments(int postId)
        {
            var result = await _communityService.GetPostCommentsAsync(postId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Delete a community post (only post author or admins).
        /// </summary>
        [HttpDelete("posts/{id}")]
        public async Task<IActionResult> DeletePost(int id)
        {
            var result = await _communityService.DeletePostAsync(id, GetUserId());
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Delete a comment (only comment author, post author, or admins).
        /// </summary>
        [HttpDelete("comments/{id}")]
        public async Task<IActionResult> DeleteComment(int id)
        {
            var result = await _communityService.DeleteCommentAsync(id, GetUserId());
            return StatusCode(result.StatusCode, result);
        }
    }
}
