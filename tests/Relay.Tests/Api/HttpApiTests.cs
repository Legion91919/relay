using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Relay.Api;
using Xunit;

namespace Relay.Api.Tests;

/// <summary>Seam tests: the real app on a free local port, real HTTP (no test-host package needed).</summary>
public class HttpApiTests
{
    private static async Task<(WebApplication App, HttpClient Http)> StartAsync()
    {
        var app = RelayApp.Build(["--environment=Development", "--urls=http://127.0.0.1:0"]);
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        return (app, new HttpClient { BaseAddress = new Uri(address) });
    }

    private static async Task<string> SignInAsync(HttpClient http, string userId)
    {
        var res = await http.PostAsJsonAsync("/api/identity/demo-sessions", new { userId });
        res.EnsureSuccessStatusCode();
        var session = await res.Content.ReadFromJsonAsync<SessionBody>();
        return session!.Token;
    }

    private sealed record SessionBody(string Token, string UserId, string DeviceId);

    private sealed record SendBody(bool Ok, string? MessageId, long? Seq, string? Status, bool Duplicate);

    [Fact]
    public async Task HealthEndpointsAnswer()
    {
        var (app, http) = await StartAsync();
        await using (app)
        {
            Assert.Equal(HttpStatusCode.OK, (await http.GetAsync("/health/live")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await http.GetAsync("/health/ready")).StatusCode);
        }
    }

    [Fact]
    public async Task ConversationsNeedASession()
    {
        var (app, http) = await StartAsync();
        await using (app)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await http.GetAsync("/api/conversations")).StatusCode);
        }
    }

    [Fact]
    public async Task RestSendIsIdempotentOnClientMessageId()
    {
        var (app, http) = await StartAsync();
        await using (app)
        {
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await SignInAsync(http, "u_thandi"));
            var msg = new { clientMessageId = "8c5e1f00-0000-4000-8000-000000000001", body = "Sharp sharp" };

            var first = await http.PostAsJsonAsync("/api/conversations/dm.u_sipho.u_thandi/messages", msg);
            var retry = await http.PostAsJsonAsync("/api/conversations/dm.u_sipho.u_thandi/messages", msg);
            var receipts = await http.GetFromJsonAsync<List<Dictionary<string, object>>>("/api/conversations/dm.u_sipho.u_thandi/receipts");

            Assert.Equal(HttpStatusCode.Created, first.StatusCode);
            Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
            Assert.True((await retry.Content.ReadFromJsonAsync<SendBody>())!.Duplicate);
            Assert.Single(receipts!);
        }
    }

    [Fact]
    public async Task ErrorsAreProblemDetails()
    {
        var (app, http) = await StartAsync();
        await using (app)
        {
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await SignInAsync(http, "u_pule"));

            var res = await http.PostAsJsonAsync("/api/conversations/dm.u_sipho.u_thandi/messages", new { clientMessageId = "8c5e1f00-0000-4000-8000-000000000002", body = "hi" });

            Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
            Assert.Equal("application/problem+json", res.Content.Headers.ContentType!.MediaType);
        }
    }
}
