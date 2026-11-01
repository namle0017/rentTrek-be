namespace RenTrek.BLL.DTOs.Refund.Internal;

public sealed record RefundPolicySnapshotResult(Guid PolicyId, int Version, Guid RuleId, DateTimeOffset EvaluatedAt,
    DateTimeOffset ServiceStartAt, decimal PolicyBase, decimal Percentage, decimal Entitlement);
