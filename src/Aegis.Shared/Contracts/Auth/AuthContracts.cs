using NodaTime;

namespace Aegis.Shared.Contracts.Auth;

public sealed record LoginRequest(string Username, string Password);

public sealed record SessionView(
    string Username,
    bool IsAuthenticated,
    Instant AuthenticatedAtUtc);
