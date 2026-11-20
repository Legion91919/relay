using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Relay.Api.Auth;

namespace Relay.Api.Chat;

/// <summary>
/// Live connection for phones at <c>/hubs/chat</c>. Thin: every rule lives in <see cref="ChatCoordinator"/> and the modules.
/// Client flow: connect, call <see cref="SyncPending"/>, ack with <see cref="AckDelivered"/>; send with <see cref="Send"/>.
/// </summary>
[Authorize]
public sealed class ChatHub(ChatCoordinator chat) : Hub<IChatClient>
{
    public override async Task OnConnectedAsync()
    {
        var me = Context.User!.ToPrincipal();
        await Groups.AddToGroupAsync(Context.ConnectionId, HubGroups.Device(me.DeviceId));
        await Groups.AddToGroupAsync(Context.ConnectionId, HubGroups.User(me.UserId));
        await chat.ConnectedAsync(me, Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await chat.DisconnectedAsync(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    public async Task<SendMessageResult> Send(SendMessageRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var outcome = await chat.SendAsync(Context.User!.ToPrincipal(), request.ConversationId, request.ClientMessageId, request.Body);
        return outcome.ToResult();
    }

    public IReadOnlyList<EnvelopeDto> SyncPending() => chat.Pending(Context.User!.ToPrincipal());

    public Task<int> AckDelivered(string[] messageIds) => chat.AckDeliveredAsync(Context.User!.ToPrincipal(), messageIds ?? []);

    public Task<int> MarkRead(string conversationId, long upToSeq) => chat.MarkReadAsync(Context.User!.ToPrincipal(), conversationId, upToSeq);
}
