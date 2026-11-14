namespace Hanora.Domain.Enums;

public enum OrderStatus
{
    PendingPayment,
    PendingConfirmation,
    Processing,
    Ready,
    Completed,
    Cancelled
}

public enum PaymentStatus
{
    Pending,
    Paid,
    Failed,
    Expired,
    Refunded,
    Cancelled
}

public enum PaymentMethod
{
    Qris,
    Cash
}

public enum PaymentProvider
{
    Midtrans,
    Manual
}

public enum ActorType
{
    System,
    Cashier,
    Owner,
    Customer
}
