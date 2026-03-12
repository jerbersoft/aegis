using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using Aegis.Adapters.Alpaca.Services;
using Aegis.Shared.Contracts.Auth;
using Aegis.Shared.Contracts.Common;
using Aegis.Shared.Contracts.Universe;
using Aegis.Shared.Ports.MarketData;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NodaTime;
using Shouldly;

namespace Aegis.Universe.IntegrationTests;

public sealed class UniverseApiTests : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<PostgresTestContainer>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly WebApplicationFactory<Program> _realProviderFactory;

    public UniverseApiTests(WebApplicationFactory<Program> factory, PostgresTestContainer postgres)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configBuilder) =>
            {
                configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = postgres.ConnectionString,
                    ["ConnectionStrings:Universe"] = postgres.ConnectionString,
                    ["ConnectionStrings:MarketData"] = postgres.ConnectionString,
                    ["Alpaca:SymbolReference:UseFakeProvider"] = "true"
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ISymbolReferenceProvider>();
                services.AddScoped<ISymbolReferenceProvider, FakeSymbolReferenceProvider>();
                services.RemoveAll<IHistoricalBarProvider>();
                services.AddScoped<IHistoricalBarProvider, TestHistoricalBarProvider>();
            });
        });

        _realProviderFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configBuilder) =>
            {
                configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = postgres.ConnectionString,
                    ["ConnectionStrings:Universe"] = postgres.ConnectionString,
                    ["ConnectionStrings:MarketData"] = postgres.ConnectionString,
                    ["Alpaca:SymbolReference:UseFakeProvider"] = "false",
                    ["Alpaca:SymbolReference:ApiKey"] = "invalid-key",
                    ["Alpaca:SymbolReference:ApiSecret"] = "invalid-secret"
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ISymbolReferenceProvider>();
                services.AddScoped<ISymbolReferenceProvider, UnavailableSymbolReferenceProvider>();
                services.RemoveAll<IHistoricalBarProvider>();
                services.AddScoped<IHistoricalBarProvider, TestHistoricalBarProvider>();
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

        var session = await loginResponse.Content.ReadAegisJsonAsync<SessionView>();
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

        var watchlists = await response.Content.ReadAegisJsonAsync<List<WatchlistSummaryView>>();
        watchlists.ShouldNotBeNull();
        watchlists.Any(x => x.IsExecution && x.Name == "Execution").ShouldBeTrue();
    }

    [Fact]
    public async Task CreateWatchlist_ThenAddSymbol_ShouldReturnCreatedViews()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var createResponse = await client.PostAsJsonAsync("/api/universe/watchlists", new CreateWatchlistRequest("Growth"));
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var createdWatchlist = await createResponse.Content.ReadAegisJsonAsync<WatchlistDetailView>();
        createdWatchlist.ShouldNotBeNull();

        var addSymbolResponse = await client.PostAsJsonAsync($"/api/universe/watchlists/{createdWatchlist.WatchlistId}/symbols", new AddSymbolToWatchlistRequest("aapl"));
        addSymbolResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var createdMembership = await addSymbolResponse.Content.ReadAegisJsonAsync<WatchlistItemView>();
        createdMembership.ShouldNotBeNull();
        createdMembership.Ticker.ShouldBe("AAPL");
    }

    [Fact]
    public async Task DeleteExecutionWatchlist_ShouldReturnConflict()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var watchlists = await client.GetAegisJsonAsync<List<WatchlistSummaryView>>("/api/universe/watchlists");
        var execution = watchlists!.Single(x => x.IsExecution);

        var deleteResponse = await client.DeleteAsync($"/api/universe/watchlists/{execution.WatchlistId}");
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var error = await deleteResponse.Content.ReadFromJsonAsync<ApiErrorResponse>();
        error.ShouldNotBeNull();
        error.Code.ShouldBe("watchlist_is_system_owned");
    }

    [Fact]
    public async Task AddSymbol_ShouldReturnServiceUnavailable_WhenRealProviderIsConfiguredButUnavailable()
    {
        using var client = await CreateAuthenticatedClientAsync(_realProviderFactory);

        var createResponse = await client.PostAsJsonAsync("/api/universe/watchlists", new CreateWatchlistRequest("ProviderCheck"));
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var createdWatchlist = await createResponse.Content.ReadAegisJsonAsync<WatchlistDetailView>();
        createdWatchlist.ShouldNotBeNull();

        var addSymbolResponse = await client.PostAsJsonAsync($"/api/universe/watchlists/{createdWatchlist.WatchlistId}/symbols", new AddSymbolToWatchlistRequest("TSLA"));
        addSymbolResponse.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);

        var error = await addSymbolResponse.Content.ReadFromJsonAsync<ApiErrorResponse>();
        error.ShouldNotBeNull();
        error.Code.ShouldBe("symbol_reference_unavailable");
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync(WebApplicationFactory<Program>? factory = null)
    {
        var client = (factory ?? _factory).CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("demo", "demo"));
        loginResponse.EnsureSuccessStatusCode();
        return client;
    }

    private sealed class TestHistoricalBarProvider : IHistoricalBarProvider
    {
        public Task<HistoricalBarBatchResult> GetDailyBarsAsync(HistoricalBarRequest request, CancellationToken cancellationToken)
        {
            HistoricalBarRecord[] bars =
            [
                new(request.Symbol, "1day", Instant.FromUtc(2026, 3, 10, 0, 0), 100, 105, 99, 104, 1000, "regular", new LocalDate(2026, 3, 10), "reconciled", true)
            ];

            return Task.FromResult(HistoricalBarBatchResult.Success(request.Symbol, "1day", bars, "fake", "iex"));
        }
    }

    private sealed class UnavailableSymbolReferenceProvider : ISymbolReferenceProvider
    {
        public Task<ValidatedSymbolResult> ValidateSymbolAsync(ValidateSymbolRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(ValidatedSymbolResult.Invalid("symbol_reference_unavailable", "fake"));
    }
}

public sealed class PostgresTestContainer : IAsyncLifetime
{
    private readonly string _containerName = $"aegis-tests-{Guid.NewGuid():N}";
    private readonly int _hostPort = GetFreePort();

    public string ConnectionString => $"Host=localhost;Port={_hostPort};Database=aegis;Username=postgres;Password=postgres";

    public async Task InitializeAsync()
    {
        await RunDockerAsync($"run -d --name {_containerName} -e POSTGRES_USER=postgres -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=aegis -p {_hostPort}:5432 postgres:17");

        for (var attempt = 0; attempt < 30; attempt++)
        {
            var result = await RunDockerAsync($"exec {_containerName} pg_isready -U postgres -d aegis", throwOnFailure: false);
            if (result == 0)
            {
                return;
            }

            await Task.Delay(1000);
        }

        throw new InvalidOperationException("PostgreSQL test container did not become ready in time.");
    }

    public async Task DisposeAsync()
    {
        await RunDockerAsync($"rm -f {_containerName}", throwOnFailure: false);
    }

    private static async Task<int> RunDockerAsync(string arguments, bool throwOnFailure = true)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        await process.WaitForExitAsync();

        if (throwOnFailure && process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync();
            throw new InvalidOperationException($"Docker command failed: docker {arguments}\n{error}");
        }

        return process.ExitCode;
    }

    private static int GetFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
