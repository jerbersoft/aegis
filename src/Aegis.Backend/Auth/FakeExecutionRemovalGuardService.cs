using Aegis.Universe.Application.Abstractions;

namespace Aegis.Backend.Auth;

public sealed class FakeExecutionRemovalGuardService : IExecutionRemovalGuardService
{
    public Task<ExecutionRemovalGuardState> GetRemovalGuardStateAsync(Guid symbolId, CancellationToken cancellationToken) =>
        Task.FromResult(new ExecutionRemovalGuardState(
            GuardAvailable: true,
            HasAssignedStrategy: false,
            HasActiveStrategy: false,
            HasOpenPosition: false,
            HasOpenOrders: false));

    public Task<bool> DetachInactiveStrategyAssignmentAsync(Guid symbolId, CancellationToken cancellationToken) =>
        Task.FromResult(true);
}
