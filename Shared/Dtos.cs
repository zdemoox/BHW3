namespace Shared;

public class OrderDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public OrderStatus Status { get; set; }
}

public enum OrderStatus
{
    New,
    Finished,
    Cancelled
}

public class AccountDto
{
    public Guid UserId { get; set; }
    public decimal Balance { get; set; }
}

public class PaymentTaskEvent
{
    public Guid OrderId { get; set; }
    public Guid UserId { get; set; }
    public decimal Amount { get; set; }
}

public class PaymentResultEvent
{
    public Guid OrderId { get; set; }
    public bool Success { get; set; }
    public string? Reason { get; set; }
} 