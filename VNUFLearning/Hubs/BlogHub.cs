using Microsoft.AspNetCore.SignalR;

namespace VNUFLearning.Hubs
{
    public class BlogHub : Hub
    {
        public async Task NotifyNewPost(string html)
        {
            await Clients.All.SendAsync("ReceivePost", html);
        }

        public async Task NotifyNewComment(int postId, string html)
        {
            await Clients.All.SendAsync("ReceiveComment", postId, html);
        }

        public async Task NotifyNewReply(int commentId, string html)
        {
            await Clients.All.SendAsync("ReceiveReply", commentId, html);
        }

        public async Task NotifyLike(int postId, int count)
        {
            await Clients.All.SendAsync("ReceiveLike", postId, count);
        }

        public async Task NotifyDeletePost(int postId)
        {
            await Clients.All.SendAsync("DeletePost", postId);
        }

        public async Task NotifyDeleteComment(int commentId)
        {
            await Clients.All.SendAsync("DeleteComment", commentId);
        }
    }
}