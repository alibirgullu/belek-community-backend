using BelekCommunity.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace BelekCommunity.Api.Hubs
{
    [Authorize]
    public class CommunityChatHub : Hub
    {
        private readonly ICommunityChatService _chatService;
        private readonly ICommunityMemberService _memberService;
        private readonly PresenceTracker _presence;

        public CommunityChatHub(
            ICommunityChatService chatService,
            ICommunityMemberService memberService,
            PresenceTracker presence)
        {
            _chatService = chatService;
            _memberService = memberService;
            _presence = presence;
        }

        private int? GetUserId()
        {
            var userIdString = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdString, out var id) ? id : null;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = GetUserId();
            if (userId.HasValue)
            {
                var isFirstConnection = await _presence.UserConnected(userId.Value, Context.ConnectionId);

                var communityIds = await _memberService.GetUserActiveCommunityIdsAsync(userId.Value);
                foreach (var cid in communityIds)
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, $"Community_{cid}");
                }

                if (isFirstConnection && communityIds.Length > 0)
                {
                    foreach (var cid in communityIds)
                    {
                        await Clients.OthersInGroup($"Community_{cid}")
                            .SendAsync("UserOnline", new { userId = userId.Value, communityId = cid });
                    }
                }
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = GetUserId();
            if (userId.HasValue)
            {
                var communityIds = await _memberService.GetUserActiveCommunityIdsAsync(userId.Value);
                var isLastConnection = await _presence.UserDisconnected(userId.Value, Context.ConnectionId);

                if (isLastConnection && communityIds.Length > 0)
                {
                    foreach (var cid in communityIds)
                    {
                        await Clients.OthersInGroup($"Community_{cid}")
                            .SendAsync("UserOffline", new { userId = userId.Value, communityId = cid });
                    }
                }
            }

            await base.OnDisconnectedAsync(exception);
        }

        public async Task JoinCommunityGroup(int communityId)
        {
            var userId = GetUserId();
            if (!userId.HasValue)
            {
                await Clients.Caller.SendAsync("Error", "Kullanıcı doğrulanamadı.");
                return;
            }

            var isMember = await _memberService.IsActiveMemberAsync(communityId, userId.Value);
            if (!isMember)
            {
                await Clients.Caller.SendAsync("Error", "Bu topluluğa erişim izniniz yok.");
                return;
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, $"Community_{communityId}");
        }

        public async Task LeaveCommunityGroup(int communityId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Community_{communityId}");
        }

        public async Task SendMessageToCommunity(int communityId, string content)
        {
            try
            {
                var userId = GetUserId();
                if (!userId.HasValue)
                {
                    await Clients.Caller.SendAsync("Error", "Kullanıcı doğrulanamadı.");
                    return;
                }

                var result = await _chatService.SaveMessageAsync(communityId, userId.Value, content);

                if (result.IsSuccess && result.Data != null)
                {
                    await Clients.Group($"Community_{communityId}").SendAsync("ReceiveMessage", result.Data);
                }
                else
                {
                    await Clients.Caller.SendAsync("Error", result.Message);
                }
            }
            catch (Exception ex)
            {
                var msg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                await Clients.Caller.SendAsync("Error", "Sunucu hatası: " + msg);
            }
        }

        public async Task MarkAsRead(int communityId, List<Guid> messageIds)
        {
            var userId = GetUserId();
            if (!userId.HasValue)
            {
                await Clients.Caller.SendAsync("Error", "Kullanıcı doğrulanamadı.");
                return;
            }

            var result = await _chatService.MarkMessagesAsReadAsync(communityId, userId.Value, messageIds);
            if (!result.IsSuccess)
            {
                await Clients.Caller.SendAsync("Error", result.Message);
                return;
            }

            if (result.MarkedIds.Count > 0)
            {
                await Clients.Group($"Community_{communityId}").SendAsync("MessagesRead", new
                {
                    communityId,
                    userId = userId.Value,
                    messageIds = result.MarkedIds
                });
            }
        }

        public async Task StartTyping(int communityId)
        {
            var userId = GetUserId();
            if (!userId.HasValue) return;

            await Clients.OthersInGroup($"Community_{communityId}").SendAsync("UserTyping", new
            {
                communityId,
                userId = userId.Value
            });
        }

        public async Task StopTyping(int communityId)
        {
            var userId = GetUserId();
            if (!userId.HasValue) return;

            await Clients.OthersInGroup($"Community_{communityId}").SendAsync("UserStoppedTyping", new
            {
                communityId,
                userId = userId.Value
            });
        }

        public async Task EditMessage(int communityId, Guid messageId, string newContent)
        {
            var userId = GetUserId();
            if (!userId.HasValue)
            {
                await Clients.Caller.SendAsync("Error", "Kullanıcı doğrulanamadı.");
                return;
            }

            var result = await _chatService.EditMessageAsync(messageId, userId.Value, newContent);
            if (!result.IsSuccess || result.Data == null)
            {
                await Clients.Caller.SendAsync("Error", result.Message);
                return;
            }

            await Clients.Group($"Community_{communityId}").SendAsync("MessageEdited", result.Data);
        }

        public async Task DeleteMessage(int communityId, Guid messageId)
        {
            var userId = GetUserId();
            if (!userId.HasValue)
            {
                await Clients.Caller.SendAsync("Error", "Kullanıcı doğrulanamadı.");
                return;
            }

            var result = await _chatService.DeleteMessageAsync(messageId, userId.Value);
            if (!result.IsSuccess)
            {
                await Clients.Caller.SendAsync("Error", result.Message);
                return;
            }

            await Clients.Group($"Community_{communityId}").SendAsync("MessageDeleted", new
            {
                communityId,
                messageId
            });
        }
    }
}
