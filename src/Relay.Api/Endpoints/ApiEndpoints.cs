using Relay.Api.Auth;
using Relay.Api.Chat;
using Relay.Conversations.Contracts;
using Relay.Delivery.Contracts;
using Relay.Identity.Contracts;
using Relay.Presence.Contracts;

namespace Relay.Api.Endpoints;

public sealed record StartSessionRequest(string UserId, string? DeviceId);

public sealed record OpenConversationRequest(string PartnerUserId);

public sealed record PostMessageRequest(string? ClientMessageId, string Body);

public sealed record ConversationDto(string ConversationId, UserSummary Partner, long LastSeq, DateTimeOffset? LastMessageAt);

public sealed record ReceiptDto(string MessageId, long Seq, string SenderId, DateTimeOffset SentAt, string Status);

public sealed record QueueDto(string DeviceId, int Pending);

internal static class ApiEndpoints
{
    public static void MapRelayApi(this WebApplication app)
    {
        var identity = app.MapGroup("/api/identity").WithTags("Identity");
        identity.MapGet("/users", (IIdentity ids) => ids.ListUsers());
        if (app.Environment.IsDevelopment())
        {
            identity.MapPost("/demo-sessions", StartSession);
        }

        var conversations = app.MapGroup("/api/conversations").WithTags("Conversations").RequireAuthorization();
        conversations.MapGet("/", ListConversations);
        conversations.MapPost("/", OpenConversation);
        conversations.MapGet("/{conversationId}/receipts", ListReceipts);
        conversations.MapPost("/{conversationId}/messages", PostMessage);

        app.MapGet("/api/presence/{userId}", PresenceOf).WithTags("Presence").RequireAuthorization();

        var delivery = app.MapGroup("/api/delivery").WithTags("Delivery").RequireAuthorization();
        delivery.MapGet("/queue", (HttpContext http, IDelivery d) =>
        {
            var me = http.User.ToPrincipal();
            return new QueueDto(me.DeviceId, d.PendingCount(me.DeviceId));
        });
        delivery.MapGet("/stats", (IDelivery d) => d.Stats());
    }

    private static IResult StartSession(StartSessionRequest request, IIdentity identity)
    {
        var result = identity.StartDemoSession(request.UserId, request.DeviceId);
        return result.Outcome switch
        {
            SessionOutcome.Started => Results.Ok(result.Session),
            SessionOutcome.UnknownUser => Results.Problem(statusCode: 404, title: "Unknown user", detail: "Pick one of GET /api/identity/users."),
            _ => Results.Problem(statusCode: 400, title: "Invalid device ID", detail: "Device IDs are 3-40 lower-case letters, digits or dashes."),
        };
    }

    private static IEnumerable<ConversationDto> ListConversations(HttpContext http, IConversations conversations, IIdentity identity)
    {
        var me = http.User.ToPrincipal();
        return conversations.ListFor(me.UserId).Select(c => ToDto(c, me.UserId, identity));
    }

    private static IResult OpenConversation(OpenConversationRequest request, HttpContext http, IConversations conversations, IIdentity identity)
    {
        var me = http.User.ToPrincipal();
        if (identity.FindUser(request.PartnerUserId) is null)
        {
            return Results.Problem(statusCode: 404, title: "Unknown user");
        }

        var result = conversations.OpenDirect(me.UserId, request.PartnerUserId);
        return result.Outcome switch
        {
            OpenOutcome.Opened => Results.Created($"/api/conversations/{result.Conversation!.ConversationId}", ToDto(result.Conversation, me.UserId, identity)),
            OpenOutcome.AlreadyExisted => Results.Ok(ToDto(result.Conversation!, me.UserId, identity)),
            _ => Results.Problem(statusCode: 400, title: "You can't open a chat with yourself"),
        };
    }

    private static IResult ListReceipts(string conversationId, HttpContext http, IConversations conversations, long afterSeq = 0, int limit = 100)
    {
        var receipts = conversations.ListReceipts(http.User.ToPrincipal().UserId, conversationId, afterSeq, limit);
        return receipts is null
            ? Results.Problem(statusCode: 404, title: "Conversation not found")
            : Results.Ok(receipts.Select(r => new ReceiptDto(r.MessageId, r.Seq, r.SenderId, r.SentAt, StatusNames.Of(r.Status))));
    }

    /// <summary>REST fallback for sending (same rules as the hub). Retry-safe: the client message ID (or Idempotency-Key header) makes it idempotent.</summary>
    private static async Task<IResult> PostMessage(string conversationId, PostMessageRequest request, HttpContext http, ChatCoordinator chat)
    {
        var id = request.ClientMessageId ?? http.Request.Headers["Idempotency-Key"].ToString();
        var outcome = await chat.SendAsync(http.User.ToPrincipal(), conversationId, id, request.Body);
        var body = outcome.ToResult();
        return outcome.Status switch
        {
            SendStatus.Accepted => Results.Created($"/api/conversations/{conversationId}/receipts?afterSeq={body.Seq - 1}", body),
            SendStatus.Duplicate => Results.Ok(body),
            SendStatus.NotFound => Results.Problem(statusCode: 404, title: "Conversation not found", type: outcome.Error),
            SendStatus.Forbidden => Results.Problem(statusCode: 403, title: "Not a member of this conversation", type: outcome.Error),
            SendStatus.Conflict => Results.Problem(statusCode: 409, title: "Message ID already used for a different message", type: outcome.Error),
            _ => Results.Problem(statusCode: 400, title: "Invalid message", detail: outcome.Error, type: outcome.Error),
        };
    }

    private static IResult PresenceOf(string userId, IPresence presence, IIdentity identity)
    {
        if (identity.FindUser(userId) is null)
        {
            return Results.Problem(statusCode: 404, title: "Unknown user");
        }

        var s = presence.StatusOf(userId);
        return Results.Ok(new PresenceDto(s.UserId, s.Online, s.LastSeenAt));
    }

    private static ConversationDto ToDto(ConversationSummary c, string me, IIdentity identity)
    {
        var partnerId = c.MemberIds.First(m => m != me);
        var partner = identity.FindUser(partnerId) ?? new UserSummary(partnerId, partnerId, "?");
        return new ConversationDto(c.ConversationId, partner, c.LastSeq, c.LastMessageAt);
    }
}
