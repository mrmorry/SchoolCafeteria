using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace SchoolCafeteria.IntegrationTests;

/// <summary>Exercises the real authentication/authorization pipeline end to end against an
/// in-memory SQLite-backed instance of the API.</summary>
public class AuthorizationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    public AuthorizationTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task HealthLive_ReturnsOk_WithoutAuthentication()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/v1/students");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_Returns422WithProblemDetails()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "nadie@demo.local", password = "wrong", mfaCode = (string?)null });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
