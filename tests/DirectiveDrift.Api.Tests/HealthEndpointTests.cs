using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace DirectiveDrift.Api.Tests;

public sealed class HealthEndpointTests(WebApplicationFactory<Program> application)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task HealthEndpointReportsHealthy(string path)
    {
        using var client = application.CreateClient();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        using var response = await client.GetAsync(path, cancellation.Token);
        var body = await response.Content.ReadAsStringAsync(cancellation.Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", body);
    }
}
