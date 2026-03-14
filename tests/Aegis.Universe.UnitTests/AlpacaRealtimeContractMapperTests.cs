using Alpaca.Markets;
using Aegis.Adapters.Alpaca.Configuration;
using Aegis.Adapters.Alpaca.Services;
using NodaTime;
using NSubstitute;
using Shouldly;

namespace Aegis.Universe.UnitTests;

public sealed class AlpacaRealtimeContractMapperTests
{
    [Fact]
    public void BuildCapabilities_ShouldExposeSupportedRealtimeFeatureSet()
    {
        var capabilities = AlpacaRealtimeContractResolver.BuildCapabilities(new AlpacaRealtimeOptions
        {
            Feed = "sip"
        });

        capabilities.ProviderName.ShouldBe("alpaca");
        capabilities.DefaultFeed.ShouldBe("sip");
        capabilities.SupportedFeeds.ShouldBe(["iex", "sip", "otc"]);
        capabilities.SupportedIntervals.ShouldBe(["1min", "1day"]);
        capabilities.SupportsHistoricalBatches.ShouldBeTrue();
        capabilities.SupportsRevisionEvents.ShouldBeTrue();
        capabilities.SupportsIncrementalSubscriptionChanges.ShouldBeTrue();
        capabilities.SupportsPartialSubscriptionFailures.ShouldBeFalse();
        capabilities.MaxSymbolsPerIncrementalSubscriptionChange.ShouldBeNull();
        capabilities.SupportsTrades.ShouldBeTrue();
        capabilities.SupportsQuotes.ShouldBeTrue();
        capabilities.SupportsMinuteBars.ShouldBeTrue();
        capabilities.SupportsUpdatedBars.ShouldBeTrue();
        capabilities.SupportsDailyBars.ShouldBeTrue();
        capabilities.SupportsTradingStatuses.ShouldBeTrue();
        capabilities.SupportsTradeCorrections.ShouldBeTrue();
        capabilities.SupportsTradeCancels.ShouldBeTrue();
        capabilities.SupportsMarketStatus.ShouldBeTrue();
        capabilities.SupportsProviderStatus.ShouldBeTrue();
        capabilities.SupportsErrorSignals.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null, "paper")]
    [InlineData("paper", "paper")]
    [InlineData("live", "live")]
    [InlineData(" IEX ", "iex")]
    [InlineData("sip", "sip")]
    public void Resolver_ShouldNormalizeEnvironmentAndFeedValues(string? input, string expected)
    {
        if (expected is "paper" or "live")
        {
            AlpacaRealtimeContractResolver.NormalizeEnvironment(input).ShouldBe(expected);
            return;
        }

        AlpacaRealtimeContractResolver.NormalizeFeed(input).ShouldBe(expected);
    }

    [Fact]
    public void MapTrade_ShouldProjectSdkTradeIntoSharedContract()
    {
        var trade = Substitute.For<ITrade>();
        trade.Symbol.Returns("aapl");
        trade.TimestampUtc.Returns(new DateTime(2026, 3, 14, 14, 30, 0, DateTimeKind.Utc));
        trade.Price.Returns(101.25m);
        trade.Size.Returns(25m);
        trade.TradeId.Returns(12345UL);
        trade.Exchange.Returns("Q");
        trade.Tape.Returns("C");
        trade.Update.Returns("none");
        trade.Conditions.Returns(["@"]);

        var receivedUtc = Instant.FromUtc(2026, 3, 14, 14, 30, 1);
        var mapped = AlpacaRealtimeContractMapper.MapTrade(trade, "sip", receivedUtc);

        mapped.EventType.ShouldBe("trade");
        mapped.ProviderName.ShouldBe("alpaca");
        mapped.ProviderFeed.ShouldBe("sip");
        mapped.Trade.Symbol.ShouldBe("AAPL");
        mapped.Trade.TradeTimeUtc.ShouldBe(Instant.FromUtc(2026, 3, 14, 14, 30));
        mapped.Trade.Price.ShouldBe(101.25m);
        mapped.Trade.Size.ShouldBe(25m);
        mapped.Trade.TradeId.ShouldBe("12345");
        mapped.Trade.Exchange.ShouldBe("Q");
        mapped.Trade.Tape.ShouldBe("C");
        mapped.Trade.UpdateReason.ShouldBe("none");
        mapped.Trade.Conditions.ShouldBe(["@"]);
        mapped.ReceivedUtc.ShouldBe(receivedUtc);
    }

    [Fact]
    public void MapQuote_ShouldProjectSdkQuoteIntoSharedContract()
    {
        var quote = Substitute.For<IQuote>();
        quote.Symbol.Returns("msft");
        quote.TimestampUtc.Returns(new DateTime(2026, 3, 14, 14, 31, 0, DateTimeKind.Utc));
        quote.BidPrice.Returns(300.10m);
        quote.AskPrice.Returns(300.15m);
        quote.BidSize.Returns(11m);
        quote.AskSize.Returns(13m);
        quote.BidExchange.Returns("V");
        quote.AskExchange.Returns("Q");
        quote.Tape.Returns("C");
        quote.Conditions.Returns(["R"]);

        var mapped = AlpacaRealtimeContractMapper.MapQuote(quote, "iex", Instant.FromUtc(2026, 3, 14, 14, 31, 1));

        mapped.Symbol.ShouldBe("MSFT");
        mapped.ProviderFeed.ShouldBe("iex");
        mapped.QuoteTimeUtc.ShouldBe(Instant.FromUtc(2026, 3, 14, 14, 31));
        mapped.BidPrice.ShouldBe(300.10m);
        mapped.AskPrice.ShouldBe(300.15m);
        mapped.BidSize.ShouldBe(11m);
        mapped.AskSize.ShouldBe(13m);
        mapped.BidExchange.ShouldBe("V");
        mapped.AskExchange.ShouldBe("Q");
        mapped.Tape.ShouldBe("C");
        mapped.Conditions.ShouldBe(["R"]);
    }

    [Fact]
    public void MapUpdatedBar_ShouldProjectSdkBarIntoSharedContract()
    {
        var bar = Substitute.For<IBar>();
        bar.Symbol.Returns("spy");
        bar.TimeUtc.Returns(new DateTime(2026, 3, 14, 14, 32, 0, DateTimeKind.Utc));
        bar.Open.Returns(500.0m);
        bar.High.Returns(501.0m);
        bar.Low.Returns(499.5m);
        bar.Close.Returns(500.5m);
        bar.Volume.Returns(1_250L);
        bar.Vwap.Returns(500.4m);
        bar.TradeCount.Returns(42UL);

        var mapped = AlpacaRealtimeContractMapper.MapUpdatedBar(bar, "sip", Instant.FromUtc(2026, 3, 14, 14, 32, 1));

        mapped.Symbol.ShouldBe("SPY");
        mapped.Interval.ShouldBe("1min");
        mapped.BarTimeUtc.ShouldBe(Instant.FromUtc(2026, 3, 14, 14, 32));
        mapped.Open.ShouldBe(500.0m);
        mapped.High.ShouldBe(501.0m);
        mapped.Low.ShouldBe(499.5m);
        mapped.Close.ShouldBe(500.5m);
        mapped.Volume.ShouldBe(1_250L);
        mapped.Vwap.ShouldBe(500.4m);
        mapped.TradeCount.ShouldBe(42L);
    }

    [Fact]
    public void MapMinuteAndDailyBars_ShouldProjectFinalizedBarIntervals()
    {
        var bar = Substitute.For<IBar>();
        bar.Symbol.Returns("qqq");
        bar.TimeUtc.Returns(new DateTime(2026, 3, 14, 14, 35, 0, DateTimeKind.Utc));
        bar.Open.Returns(450m);
        bar.High.Returns(451m);
        bar.Low.Returns(449m);
        bar.Close.Returns(450.5m);
        bar.Volume.Returns(5_000L);

        var minute = AlpacaRealtimeContractMapper.MapMinuteBar(bar, "iex", Instant.FromUtc(2026, 3, 14, 14, 35, 1));
        var daily = AlpacaRealtimeContractMapper.MapDailyBar(bar, "iex", Instant.FromUtc(2026, 3, 14, 21, 0, 1));

        minute.Interval.ShouldBe("1min");
        daily.Interval.ShouldBe("1day");
        minute.Symbol.ShouldBe("QQQ");
        daily.Symbol.ShouldBe("QQQ");
    }

    [Fact]
    public void MapTradeCorrection_ShouldPreserveOriginalAndCorrectedTradeShapes()
    {
        var originalTrade = Substitute.For<ITrade>();
        originalTrade.Symbol.Returns("tsla");
        originalTrade.TimestampUtc.Returns(new DateTime(2026, 3, 14, 14, 33, 0, DateTimeKind.Utc));
        originalTrade.Price.Returns(200.0m);
        originalTrade.Size.Returns(5m);
        originalTrade.TradeId.Returns(77UL);
        originalTrade.Conditions.Returns([]);

        var correctedTrade = Substitute.For<ITrade>();
        correctedTrade.Symbol.Returns("tsla");
        correctedTrade.TimestampUtc.Returns(new DateTime(2026, 3, 14, 14, 33, 0, DateTimeKind.Utc));
        correctedTrade.Price.Returns(199.5m);
        correctedTrade.Size.Returns(5m);
        correctedTrade.TradeId.Returns(77UL);
        correctedTrade.Conditions.Returns(["C"]);

        var correction = Substitute.For<ICorrection>();
        correction.OriginalTrade.Returns(originalTrade);
        correction.CorrectedTrade.Returns(correctedTrade);

        var mapped = AlpacaRealtimeContractMapper.MapTradeCorrection(correction, "sip", Instant.FromUtc(2026, 3, 14, 14, 33, 1));

        mapped.OriginalTrade.Symbol.ShouldBe("TSLA");
        mapped.OriginalTrade.Price.ShouldBe(200.0m);
        mapped.CorrectedTrade.Price.ShouldBe(199.5m);
        mapped.CorrectedTrade.Conditions.ShouldBe(["C"]);
    }

    [Fact]
    public void MapTradeCancel_ShouldReuseNormalizedTradeShape()
    {
        var trade = Substitute.For<ITrade>();
        trade.Symbol.Returns("amd");
        trade.TimestampUtc.Returns(new DateTime(2026, 3, 14, 14, 36, 0, DateTimeKind.Utc));
        trade.Price.Returns(175.25m);
        trade.Size.Returns(10m);
        trade.TradeId.Returns(998UL);
        trade.Conditions.Returns(["I"]);

        var mapped = AlpacaRealtimeContractMapper.MapTradeCancel(trade, "sip", Instant.FromUtc(2026, 3, 14, 14, 36, 1));

        mapped.CancelledTrade.Symbol.ShouldBe("AMD");
        mapped.CancelledTrade.TradeId.ShouldBe("998");
        mapped.CancelledTrade.Conditions.ShouldBe(["I"]);
    }

    [Fact]
    public void MapMarketStatus_ShouldProjectClockIntoSharedContract()
    {
        var clock = Substitute.For<global::Alpaca.Markets.IClock>();
        clock.IsOpen.Returns(true);
        clock.TimestampUtc.Returns(new DateTime(2026, 3, 14, 14, 37, 0, DateTimeKind.Utc));
        clock.NextOpenUtc.Returns(new DateTime(2026, 3, 15, 13, 30, 0, DateTimeKind.Utc));
        clock.NextCloseUtc.Returns(new DateTime(2026, 3, 14, 20, 0, 0, DateTimeKind.Utc));

        var mapped = AlpacaRealtimeContractMapper.MapMarketStatus(clock, "iex", Instant.FromUtc(2026, 3, 14, 14, 37, 1));

        mapped.IsOpen.ShouldBeTrue();
        mapped.StatusTimeUtc.ShouldBe(Instant.FromUtc(2026, 3, 14, 14, 37));
        mapped.NextOpenUtc.ShouldBe(Instant.FromUtc(2026, 3, 15, 13, 30));
        mapped.NextCloseUtc.ShouldBe(Instant.FromUtc(2026, 3, 14, 20, 0));
    }

    [Theory]
    [InlineData("Connected", true, false, false)]
    [InlineData("AuthenticationSuccess", false, true, false)]
    [InlineData("AuthenticationFailed", false, false, true)]
    public void MapProviderStatus_ShouldNormalizeConnectionLifecycleSignals(string status, bool expectedConnected, bool expectedAuthenticated, bool expectedTerminal)
    {
        var mapped = AlpacaRealtimeContractMapper.MapProviderStatus(status, "iex", Instant.FromUtc(2026, 3, 14, 14, 34));

        mapped.StatusCode.ShouldBe(status);
        mapped.IsConnected.ShouldBe(expectedConnected);
        mapped.IsAuthenticated.ShouldBe(expectedAuthenticated);
        mapped.IsTerminal.ShouldBe(expectedTerminal);
    }

    [Fact]
    public void MapProviderError_ShouldNormalizeExceptionShape()
    {
        var mapped = AlpacaRealtimeContractMapper.MapProviderError(new TimeoutException("stream timed out"), "sip", Instant.FromUtc(2026, 3, 14, 14, 38), "nvda");

        mapped.ErrorCode.ShouldBe("timeout");
        mapped.ErrorMessage.ShouldBe("stream timed out");
        mapped.IsTransient.ShouldBeTrue();
        mapped.Symbol.ShouldBe("NVDA");
        mapped.ProviderFeed.ShouldBe("sip");
    }

    [Theory]
    [InlineData("Too many requests from websocket client", "rate_limited", true)]
    [InlineData("subscription limit exceeded for feed", "subscription_limit_exceeded", false)]
    [InlineData("invalid symbol requested", "subscription_rejected", false)]
    public void MapProviderError_ShouldNormalizeRateLimitAndSubscriptionFailures(string message, string errorCode, bool isTransient)
    {
        var mapped = AlpacaRealtimeContractMapper.MapProviderError(new Exception(message), "iex", Instant.FromUtc(2026, 3, 14, 14, 39), "aapl");

        mapped.ErrorCode.ShouldBe(errorCode);
        mapped.IsTransient.ShouldBe(isTransient);
        mapped.Symbol.ShouldBe("AAPL");
    }
}
