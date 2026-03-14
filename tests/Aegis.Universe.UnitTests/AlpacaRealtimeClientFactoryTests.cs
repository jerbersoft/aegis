using Aegis.Adapters.Alpaca.Configuration;
using Aegis.Adapters.Alpaca.Services;
using Shouldly;

namespace Aegis.Universe.UnitTests;

public sealed class AlpacaRealtimeClientFactoryTests
{
    [Theory]
    [InlineData("paper", "iex", "wss://stream.data.alpaca.markets/v2/iex")]
    [InlineData("paper", "sip", "wss://stream.data.alpaca.markets/v2/sip")]
    [InlineData("paper", "otc", "wss://stream.data.alpaca.markets/v2/otc")]
    [InlineData("live", "iex", "wss://stream.data.alpaca.markets/v2/iex")]
    [InlineData("live", "sip", "wss://stream.data.alpaca.markets/v2/sip")]
    public void BuildDataStreamingClientConfiguration_ShouldHonorConfiguredFeed(string environment, string feed, string expectedEndpoint)
    {
        var configuration = AlpacaRealtimeClientFactory.BuildDataStreamingClientConfiguration(new AlpacaRealtimeOptions
        {
            ApiKey = "key",
            ApiSecret = "secret",
            Environment = environment,
            Feed = feed
        });

        configuration.ApiEndpoint.ShouldBe(new Uri(expectedEndpoint));
    }
}
