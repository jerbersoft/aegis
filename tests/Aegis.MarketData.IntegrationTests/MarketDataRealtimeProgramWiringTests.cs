using Aegis.Adapters.Alpaca.Services;
using Aegis.Backend.MarketData;
using Aegis.Shared.Ports.MarketData;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Aegis.MarketData.IntegrationTests;

public sealed class MarketDataRealtimeProgramWiringTests(
    WebApplicationFactory<Program> factory,
    PostgresTestContainer postgres)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<PostgresTestContainer>
{
    [Fact]
    public async Task Program_ShouldResolveSharedRealtimeProviderAndKeepRuntimeDisabledByDefault()
    {
        await using var testFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configBuilder) =>
            {
                configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = postgres.ConnectionString,
                    ["ConnectionStrings:Universe"] = postgres.ConnectionString,
                    ["ConnectionStrings:MarketData"] = postgres.ConnectionString,
                    ["Alpaca:SymbolReference:UseFakeProvider"] = "true",
                    ["Alpaca:HistoricalData:UseFakeProvider"] = "true"
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ISymbolReferenceProvider>();
                services.AddScoped<ISymbolReferenceProvider, FakeSymbolReferenceProvider>();
                services.RemoveAll<IHistoricalBarProvider>();
                services.AddScoped<IHistoricalBarProvider, FakeHistoricalBarProvider>();
            });
        });

        using var _ = testFactory.CreateClient();

        testFactory.Services.GetRequiredService<IRealtimeMarketDataProvider>().ShouldBeOfType<AlpacaRealtimeMarketDataProvider>();
        testFactory.Services.GetServices<IHostedService>().ShouldContain(service => service is MarketDataRealtimeProviderRunner);
        testFactory.Services.GetRequiredService<IOptions<MarketDataRealtimeOptions>>().Value.EnableProviderRuntime.ShouldBeFalse();
    }
}
