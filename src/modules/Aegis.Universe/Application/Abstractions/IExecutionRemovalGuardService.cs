namespace Aegis.Universe.Application.Abstractions;

public sealed record ExecutionRemovalGuardState(
    bool GuardAvailable,
    bool HasAssignedStrategy,
    bool HasActiveStrategy,
    bool HasOpenPosition,
    bool HasOpenOrders);

public interface IExecutionRemovalGuardService
{
    Task<ExecutionRemovalGuardState> GetRemovalGuardStateAsync(Guid symbolId, CancellationToken cancellationToken);

    Task<bool> DetachInactiveStrategyAssignmentAsync(Guid symbolId, CancellationToken cancellationToken);
}
