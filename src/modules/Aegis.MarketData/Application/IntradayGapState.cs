using NodaTime;

namespace Aegis.MarketData.Application;

public sealed record IntradayGapState(
    string? ActiveGapType,
    string? ReasonCode,
    Instant? ActiveGapStartUtc)
{
    public static IntradayGapState None { get; } = new(null, null, null);

    public bool HasGap => ActiveGapType is not null;
}
