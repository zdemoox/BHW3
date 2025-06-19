namespace OrdersService.Tests;
using OrdersService;
using Microsoft.EntityFrameworkCore;
using Shared;

public class OrdersServiceTests
{
    [Fact]
    public async Task CanCreateAndGetOrder()
    {
        var options = new DbContextOptionsBuilder<OrdersDbContext>()
            .UseInMemoryDatabase(databaseName: "orders-test-db")
            .Options;
        using var db = new OrdersDbContext(options);
        var order = new Order
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Amount = 123.45m,
            Description = "Test order",
            Status = OrderStatus.New
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        var loaded = await db.Orders.FindAsync(order.Id);
        Assert.NotNull(loaded);
        Assert.Equal(order.Amount, loaded.Amount);
        Assert.Equal(order.Description, loaded.Description);
        Assert.Equal(OrderStatus.New, loaded.Status);
    }

    [Fact]
    public async Task CanCreateMultipleOrders()
    {
        var options = new DbContextOptionsBuilder<OrdersDbContext>()
            .UseInMemoryDatabase(databaseName: "orders-test-db-multi")
            .Options;
        using var db = new OrdersDbContext(options);
        var orders = Enumerable.Range(0, 5).Select(i => new Order
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Amount = 100 + i,
            Description = $"Order {i}",
            Status = OrderStatus.New
        }).ToList();
        db.Orders.AddRange(orders);
        await db.SaveChangesAsync();
        var all = await db.Orders.ToListAsync();
        Assert.Equal(5, all.Count);
    }

    [Fact]
    public async Task CanFilterOrdersByStatus()
    {
        var options = new DbContextOptionsBuilder<OrdersDbContext>()
            .UseInMemoryDatabase(databaseName: "orders-test-db-status")
            .Options;
        using var db = new OrdersDbContext(options);
        db.Orders.Add(new Order { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Amount = 1, Description = "A", Status = OrderStatus.New });
        db.Orders.Add(new Order { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Amount = 2, Description = "B", Status = OrderStatus.Finished });
        await db.SaveChangesAsync();
        var finished = await db.Orders.Where(o => o.Status == OrderStatus.Finished).ToListAsync();
        Assert.Single(finished);
        Assert.Equal(OrderStatus.Finished, finished[0].Status);
    }

    [Fact]
    public async Task GetOrder_NotFound()
    {
        var options = new DbContextOptionsBuilder<OrdersDbContext>()
            .UseInMemoryDatabase(databaseName: "orders-test-db-notfound")
            .Options;
        using var db = new OrdersDbContext(options);
        var notFound = await db.Orders.FindAsync(Guid.NewGuid());
        Assert.Null(notFound);
    }
}