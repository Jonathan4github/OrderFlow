namespace OrderFlow.Domain.Enums;

/// <summary>Lifecycle of an <see cref="Orders.Order"/>.</summary>
public enum OrderStatus
{
    /// <summary>Order has been placed and stock reserved; awaiting payment.</summary>
    Pending = 0,

    /// <summary>Payment has been processed and inventory confirmed.</summary>
    Confirmed = 1,

    /// <summary>Processing failed at some stage (e.g. payment rejection).</summary>
    Failed = 2,

    /// <summary>Order was cancelled before it could be confirmed.</summary>
    Cancelled = 3
}
