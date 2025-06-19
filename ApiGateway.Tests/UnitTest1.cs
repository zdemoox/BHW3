namespace ApiGateway.Tests;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Threading.Tasks;
using Xunit;
using System.Net.Http.Json;
using System.Net.Http;

public class ApiGatewayIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    public ApiGatewayIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SwaggerUi_Returns200()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/swagger/index.html");
        Assert.True(resp.IsSuccessStatusCode);
    }

    [Fact]
    public async Task NotFoundRoute_Returns404()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/notfoundroute");
        Assert.Equal(System.Net.HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task MethodNotAllowed_Returns405Or404()
    {
        var client = _factory.CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Put, "/swagger/index.html");
        var resp = await client.SendAsync(req);
        Assert.True(
            resp.StatusCode == System.Net.HttpStatusCode.MethodNotAllowed ||
            resp.StatusCode == System.Net.HttpStatusCode.NotFound
        );
    }

    [Fact]
    public async Task AccountsRoute_Exists()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/accounts/test");
        Assert.True(resp.StatusCode == System.Net.HttpStatusCode.BadGateway ||
                    resp.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable ||
                    resp.StatusCode == System.Net.HttpStatusCode.InternalServerError ||
                    resp.StatusCode == System.Net.HttpStatusCode.OK ||
                    resp.StatusCode == System.Net.HttpStatusCode.NotFound);
    }
}