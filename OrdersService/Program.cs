using Shared;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MassTransit;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<OrdersDbContext>(opt =>
    opt.UseInMemoryDatabase("orders-db"));


builder.Services.AddHostedService<OutboxPublisherService>();

// MassTransit config
builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host("rabbitmq", h =>
        {
            h.Username("guest");
            h.Password("guest");
        });
        cfg.ReceiveEndpoint("orders-service", e =>
        {
            e.Consumer<PaymentResultConsumer>(ctx);
        });
    });
    x.AddConsumer<PaymentResultConsumer>();
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapPost("/orders", async (OrdersDbContext db, OrderDto dto) =>
{
    using var tx = await db.Database.BeginTransactionAsync();
    var order = new Order
    {
        Id = Guid.NewGuid(),
        UserId = dto.UserId,
        Amount = dto.Amount,
        Description = dto.Description,
        Status = OrderStatus.New
    };
    db.Orders.Add(order);
    var paymentTask = new PaymentTaskEvent
    {
        OrderId = order.Id,
        UserId = order.UserId,
        Amount = order.Amount
    };
    db.OutboxMessages.Add(new OutboxMessage
    {
        Id = Guid.NewGuid(),
        OccurredOn = DateTime.UtcNow,
        Type = nameof(PaymentTaskEvent),
        Payload = JsonSerializer.Serialize(paymentTask),
        Processed = false
    });
    await db.SaveChangesAsync();
    await tx.CommitAsync();
    return Results.Ok(new { order.Id });
});

app.MapGet("/orders", async (OrdersDbContext db) =>
{
    var orders = await db.Orders.ToListAsync();
    return Results.Ok(orders);
});

app.MapGet("/orders/{id}", async (OrdersDbContext db, Guid id) =>
{
    var order = await db.Orders.FindAsync(id);
    if (order == null) return Results.NotFound();
    return Results.Ok(order);
});


app.Run();


public class Order
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public OrderStatus Status { get; set; }
}

public class OutboxMessage
{
    public Guid Id { get; set; }
    public DateTime OccurredOn { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public bool Processed { get; set; }
}

public class OrdersDbContext : DbContext
{
    public OrdersDbContext(DbContextOptions<OrdersDbContext> options) : base(options) { }
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
}

public class PaymentResultConsumer : IConsumer<PaymentResultEvent>
{
    private readonly OrdersDbContext _db;
    public PaymentResultConsumer(OrdersDbContext db) => _db = db;
    public async Task Consume(ConsumeContext<PaymentResultEvent> context)
    {
        var evt = context.Message;
        var order = await _db.Orders.FindAsync(evt.OrderId);
        if (order != null)
        {
            order.Status = evt.Success ? OrderStatus.Finished : OrderStatus.Cancelled;
            await _db.SaveChangesAsync();
        }
    }
}

public class OutboxPublisherService : BackgroundService
{
    private readonly IServiceProvider _sp;
    public OutboxPublisherService(IServiceProvider sp)
    {
        _sp = sp;
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
            var bus = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();
            var unprocessed = await db.OutboxMessages.Where(x => !x.Processed).ToListAsync(stoppingToken);
            foreach (var msg in unprocessed)
            {
                if (msg.Type == nameof(PaymentTaskEvent))
                {
                    var evt = JsonSerializer.Deserialize<PaymentTaskEvent>(msg.Payload);
                    await bus.Publish(evt, stoppingToken);
                }
                msg.Processed = true;
            }
            await db.SaveChangesAsync(stoppingToken);
            await Task.Delay(2000, stoppingToken);
        }
    }
}
