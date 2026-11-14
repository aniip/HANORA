using System.Net;
using Hanora.Domain.Enums;

namespace Hanora.Domain.Entities;

public sealed class Income
{
    public Guid Id { get; set; }
    public Guid? OrderId { get; set; }
    public Order? Order { get; set; }
    public Guid? PaymentId { get; set; }
    public Payment? Payment { get; set; }

    public string Source { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateOnly OccurredAt { get; set; }

    public Guid? CreatedBy { get; set; }
    public User? CreatedByUser { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class Expense
{
    public Guid Id { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateOnly OccurredAt { get; set; }
    public string? Note { get; set; }

    public Guid CreatedBy { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class TransactionHistory
{
    public long Id { get; set; }
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public OrderStatus? FromStatus { get; set; }
    public OrderStatus ToStatus { get; set; }
    public ActorType ActorType { get; set; }

    public Guid? ChangedBy { get; set; }
    public User? ChangedByUser { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class AuditLog
{
    public long Id { get; set; }
    public Guid? UserId { get; set; }
    public User? User { get; set; }

    public string Action { get; set; } = string.Empty;
    public string Entity { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? Metadata { get; set; }
    public IPAddress? Ip { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class Setting
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;

    public Guid? UpdatedBy { get; set; }
    public User? UpdatedByUser { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class DailyCounter
{
    public DateOnly Day { get; set; }
    public int LastNumber { get; set; }
}
