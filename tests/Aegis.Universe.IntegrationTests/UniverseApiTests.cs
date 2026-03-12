using System.Net;
using System.Net.Http.Json;
using Aegis.Shared.Contracts.Auth;
using Aegis.Shared.Contracts.Common;
using Aegis.Shared.Contracts.Universe;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Shouldly;

namespace Aegis.Universe.IntegrationTests;

public sealed class UniverseApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public UniverseApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configBuilder) =>
            {
                configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Universe:UseInMemory"] = "true",
                    ["Universe:InMemoryDatabaseName"] = $"universe-api-tests-{Guid.NewGuid():N}"
                });
            });
        });
    }

    [Fact]
    public async Task Login_ShouldCreateAuthenticatedSession()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("demo", "demo"));
        loginResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var session = await loginResponse.Content.ReadFromJsonAsync<SessionView>();
        session.ShouldNotBeNull();
        session.IsAuthenticated.ShouldBeTrue();

        var sessionResponse = await client.GetAsync("/api/auth/session");
        sessionResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetWatchlists_ShouldIncludeSeededExecutionWatchlist_WhenAuthenticated()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/universe/watchlists");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var watchlists = await response.Content.ReadFromJsonAsync<List<WatchlistSummaryView>>();
        watchlists.ShouldNotBeNull();
        watchlists.Any(x => x.IsExecution && x.Name == "Execution").ShouldBeTrue();
    }

    [Fact]
    public async Task CreateWatchlist_ThenAddSymbol_ShouldReturnCreatedViews()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var createResponse = await client.PostAsJsonAsync("/api/universe/watchlists", new CreateWatchlistRequest("Growth"));
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var createdWatchlist = await createResponse.Content.ReadFromJsonAsync<WatchlistDetailView>();
        createdWatchlist.ShouldNotBeNull();

        var addSymbolResponse = await client.PostAsJsonAsync($"/api/universe/watchlists/{createdWatchlist.WatchlistId}/symbols", new AddSymbolToWatchlistRequest("aapl"));
        addSymbolResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var createdMembership = await addSymbolResponse.Content.ReadFromJsonAsync<WatchlistItemView>();
        createdMembership.ShouldNotBeNull();
        createdMembership.Ticker.ShouldBe("AAPL");
    }

    [Fact]
    public async Task DeleteExecutionWatchlist_ShouldReturnConflict()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var watchlists = await client.GetFromJsonAsync<List<WatchlistSummaryView>>("/api/universe/watchlists");
        var execution = watchlists!.Single(x => x.IsExecution);

        var deleteResponse = await client.DeleteAsync($"/api/universe/watchlists/{execution.WatchlistId}");
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var error = await deleteResponse.Content.ReadFromJsonAsync<ApiErrorResponse>();
        error.ShouldNotBeNull();
        error.Code.ShouldBe("watchlist_is_system_owned");
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("demo", "demo"));
        loginResponse.EnsureSuccessStatusCode();
        return client;
    }
}
