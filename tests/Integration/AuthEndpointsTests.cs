using System.Net;
using System.Net.Http.Json;

namespace ProjectVTT.Tests.Integration;

public class AuthEndpointsTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    [Fact]
    public async Task Register_ReturnsToken_AndMeReturnsUser()
    {
        var client = factory.CreateClient();
        var userName = TestClientExtensions.NewUserName();

        var response = await client.PostAsJsonAsync("/api/v1/auth/register",
            new { userName, password = TestClientExtensions.DefaultPassword });

        var json = await response.EnsureStatusAsync(HttpStatusCode.Created);
        var token = json.GetProperty("token").GetString();
        var userId = json.GetProperty("user").GetProperty("id").GetString();
        Assert.False(string.IsNullOrEmpty(token));
        Assert.Equal(userName, json.GetProperty("user").GetProperty("userName").GetString());

        client.SetBearerToken(token!);
        var me = await client.GetAsync("/api/v1/auth/me");
        var meJson = await me.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal(userId, meJson.GetProperty("id").GetString());
        Assert.Equal(userName, meJson.GetProperty("userName").GetString());
    }

    [Fact]
    public async Task Register_DuplicateUserName_Returns409()
    {
        var client = factory.CreateClient();
        var userName = TestClientExtensions.NewUserName();
        var payload = new { userName, password = TestClientExtensions.DefaultPassword };

        await (await client.PostAsJsonAsync("/api/v1/auth/register", payload))
            .EnsureStatusAsync(HttpStatusCode.Created);

        var duplicate = await client.PostAsJsonAsync("/api/v1/auth/register", payload);
        await duplicate.EnsureStatusAsync(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Register_WeakPassword_Returns400()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/register",
            new { userName = TestClientExtensions.NewUserName(), password = "corta" });

        var json = await response.EnsureStatusAsync(HttpStatusCode.BadRequest);
        Assert.Equal("Solicitud inválida.", json.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Register_InvalidUserName_Returns400()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/register",
            new { userName = "ab", password = TestClientExtensions.DefaultPassword });

        await response.EnsureStatusAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var client = factory.CreateClient();
        var userName = TestClientExtensions.NewUserName();
        await client.RegisterUserAsync(userName);

        var response = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { userName, password = "OtraClave123" });

        await response.EnsureStatusAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_UnknownUser_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { userName = TestClientExtensions.NewUserName(), password = "NoExiste123" });

        await response.EnsureStatusAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_Returns401ProblemDetails()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/campaigns");

        var json = await response.EnsureStatusAsync(HttpStatusCode.Unauthorized);
        Assert.Equal("No autenticado.", json.GetProperty("title").GetString());
    }

    [Fact]
    public async Task UnknownRoute_Returns404WithProblemDetailsBody()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/no-existe");

        var json = await response.EnsureStatusAsync(HttpStatusCode.NotFound);
        Assert.Equal("No encontrado.", json.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Logout_ThenTokenIsRejected()
    {
        var client = factory.CreateClient();
        var token = await client.RegisterUserAsync(TestClientExtensions.NewUserName());
        client.SetBearerToken(token);

        var logout = await client.PostAsync("/api/v1/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var me = await client.GetAsync("/api/v1/auth/me");
        await me.EnsureStatusAsync(HttpStatusCode.Unauthorized);
    }
}
