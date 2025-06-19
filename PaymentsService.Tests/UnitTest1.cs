namespace PaymentsService.Tests;
using PaymentsService;
using Microsoft.EntityFrameworkCore;
using Shared;

public class PaymentsServiceTests
{
    [Fact]
    public async Task CanCreateAndTopupAccount()
    {
        var options = new DbContextOptionsBuilder<PaymentsDbContext>()
            .UseInMemoryDatabase(databaseName: "payments-test-db")
            .Options;
        using var db = new PaymentsDbContext(options);
        var userId = Guid.NewGuid();
        db.Accounts.Add(new Account { UserId = userId, Balance = 0 });
        await db.SaveChangesAsync();
        var acc = await db.Accounts.FirstOrDefaultAsync(a => a.UserId == userId);
        Assert.NotNull(acc);
        Assert.Equal(0, acc.Balance);
        acc.Balance += 1000;
        await db.SaveChangesAsync();
        var acc2 = await db.Accounts.FirstOrDefaultAsync(a => a.UserId == userId);
        Assert.Equal(1000, acc2.Balance);
    }

    [Fact]
    public async Task CannotCreateDuplicateAccount()
    {
        var options = new DbContextOptionsBuilder<PaymentsDbContext>()
            .UseInMemoryDatabase(databaseName: "payments-test-db-dup")
            .Options;
        using var db = new PaymentsDbContext(options);
        var userId = Guid.NewGuid();
        db.Accounts.Add(new Account { UserId = userId, Balance = 0 });
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(async () => {
            db.Accounts.Add(new Account { UserId = userId, Balance = 0 });
            await db.SaveChangesAsync();
        });
    }

    [Fact]
    public async Task TopupNonexistentAccount_ReturnsNull()
    {
        var options = new DbContextOptionsBuilder<PaymentsDbContext>()
            .UseInMemoryDatabase(databaseName: "payments-test-db-nonexistent")
            .Options;
        using var db = new PaymentsDbContext(options);
        var acc = await db.Accounts.FirstOrDefaultAsync(a => a.UserId == Guid.NewGuid());
        Assert.Null(acc);
    }

    [Fact]
    public async Task MultipleTopups_AccumulatesBalance()
    {
        var options = new DbContextOptionsBuilder<PaymentsDbContext>()
            .UseInMemoryDatabase(databaseName: "payments-test-db-multitopup")
            .Options;
        using var db = new PaymentsDbContext(options);
        var userId = Guid.NewGuid();
        db.Accounts.Add(new Account { UserId = userId, Balance = 0 });
        await db.SaveChangesAsync();
        var acc = await db.Accounts.FirstOrDefaultAsync(a => a.UserId == userId);
        acc.Balance += 100;
        await db.SaveChangesAsync();
        acc.Balance += 200;
        await db.SaveChangesAsync();
        var acc2 = await db.Accounts.FirstOrDefaultAsync(a => a.UserId == userId);
        Assert.Equal(300, acc2.Balance);
    }
}