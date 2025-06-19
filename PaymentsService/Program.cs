using Shared;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MassTransit;
using System.ComponentModel.DataAnnotations;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<PaymentsDbContext>(opt =>
    opt.UseInMemoryDatabase("payments-db"));
builder.Services.AddHostedService<InboxProcessorService>();
builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host("rabbitmq", h =>
        {
            h.Username("guest");
            h.Password("guest");
        });
        cfg.ReceiveEndpoint("payments-service", e =>
        {
            e.Consumer<PaymentTaskConsumer>(ctx);
        });
    });
    x.AddConsumer<PaymentTaskConsumer>();
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapPost("/accounts", async (PaymentsDbContext db, AccountDto dto) =>
{
    if (await db.Accounts.AnyAsync(a => a.UserId == dto.UserId))
        return Results.BadRequest("Account already exists");
    db.Accounts.Add(new Account { UserId = dto.UserId, Balance = 0 });
    await db.SaveChangesAsync();
    return Results.Ok();
});

app.MapPost("/accounts/topup", async (PaymentsDbContext db, AccountDto dto) =>
{
    var acc = await db.Accounts.FirstOrDefaultAsync(a => a.UserId == dto.UserId);
    if (acc == null) return Results.NotFound();
    acc.Balance += dto.Balance;
    await db.SaveChangesAsync();
    return Results.Ok();
});

app.MapGet("/accounts/{userId}", async (PaymentsDbContext db, Guid userId) =>
{
    var acc = await db.Accounts.FirstOrDefaultAsync(a => a.UserId == userId);
    if (acc == null) return Results.NotFound();
    return Results.Ok(new AccountDto { UserId = acc.UserId, Balance = acc.Balance });
});

app.Run();


public class Account
{
    [Key]
    public Guid UserId { get; set; }
    public decimal Balance { get; set; }
}

public class InboxMessage
{
    public Guid Id { get; set; }
    public DateTime OccurredOn { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public bool Processed { get; set; }
}

public class OutboxMessage
{
    public Guid Id { get; set; }
    public DateTime OccurredOn { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public bool Processed { get; set; }
}

public class PaymentsDbContext : DbContext
{
    public PaymentsDbContext(DbContextOptions<PaymentsDbContext> options) : base(options) { }
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
}

public class InboxProcessorService : BackgroundService
{
    private readonly IServiceProvider _sp;
    public InboxProcessorService(IServiceProvider sp) => _sp = sp;
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
            var unprocessed = await db.InboxMessages.Where(x => !x.Processed).ToListAsync(stoppingToken);
            foreach (var msg in unprocessed)
            {
                if (msg.Type == nameof(PaymentTaskEvent))
                {
                    var task = System.Text.Json.JsonSerializer.Deserialize<PaymentTaskEvent>(msg.Payload);
                    var acc = await db.Accounts.FirstOrDefaultAsync(a => a.UserId == task.UserId, stoppingToken);
                    PaymentResultEvent result;
                    if (acc == null)
                    {
                        result = new PaymentResultEvent { OrderId = task.OrderId, Success = false, Reason = "No account" };
                    }
                    else if (acc.Balance < task.Amount)
                    {
                        result = new PaymentResultEvent { OrderId = task.OrderId, Success = false, Reason = "Insufficient funds" };
                    }
                    else
                    {
                        acc.Balance -= task.Amount;
                        result = new PaymentResultEvent { OrderId = task.OrderId, Success = true };
                    }
                    db.OutboxMessages.Add(new OutboxMessage
                    {
                        Id = Guid.NewGuid(),
                        OccurredOn = DateTime.UtcNow,
                        Type = nameof(PaymentResultEvent),
                        Payload = System.Text.Json.JsonSerializer.Serialize(result),
                        Processed = false
                    });
                    msg.Processed = true;
                }
            }
            await db.SaveChangesAsync(stoppingToken);
            await Task.Delay(2000, stoppingToken);
        }
    }
}

public class PaymentTaskConsumer : IConsumer<PaymentTaskEvent>
{
    private readonly PaymentsDbContext _db;
    public PaymentTaskConsumer(PaymentsDbContext db) => _db = db;
    public async Task Consume(ConsumeContext<PaymentTaskEvent> context)
    {
        var evt = context.Message;
        
        if (await _db.InboxMessages.AnyAsync(x => x.Type == nameof(PaymentTaskEvent) && x.Payload.Contains(evt.OrderId.ToString()) && x.Processed))
            return;
        _db.InboxMessages.Add(new InboxMessage
        {
            Id = Guid.NewGuid(),
            OccurredOn = DateTime.UtcNow,
            Type = nameof(PaymentTaskEvent),
            Payload = System.Text.Json.JsonSerializer.Serialize(evt),
            Processed = false
        });
        await _db.SaveChangesAsync();
    }
}

public class OutboxPublisherService : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly IPublishEndpoint _bus;
    public OutboxPublisherService(IServiceProvider sp, IPublishEndpoint bus)
    {
        _sp = sp;
        _bus = bus;
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
            var unprocessed = await db.OutboxMessages.Where(x => !x.Processed).ToListAsync(stoppingToken);
            foreach (var msg in unprocessed)
            {
                if (msg.Type == nameof(PaymentResultEvent))
                {
                    var evt = System.Text.Json.JsonSerializer.Deserialize<PaymentResultEvent>(msg.Payload);
                    await _bus.Publish(evt, stoppingToken);
                }
                msg.Processed = true;
            }
            await db.SaveChangesAsync(stoppingToken);
            await Task.Delay(2000, stoppingToken);
        }
    }
}