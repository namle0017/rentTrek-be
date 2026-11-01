using Microsoft.EntityFrameworkCore;
using Npgsql;
using RenTrek.DAL.Data;

namespace RenTrek.IntegrationTests;

[Collection("postgres")]
public sealed class PaymentMigrationTests(PostgresFixture fixture)
{
    private async Task<(NpgsqlConnection Connection, Guid OrderId)> LegacyDatabase(bool duplicateBareIntent = false)
    {
        var name = "migration_" + Guid.NewGuid().ToString("N");
        await using (var admin = new NpgsqlConnection(fixture.ConnectionString))
        {
            await admin.OpenAsync(); await new NpgsqlCommand($"CREATE DATABASE {name}", admin).ExecuteNonQueryAsync();
        }
        var connection = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(fixture.ConnectionString) { Database = name }.ConnectionString);
        await connection.OpenAsync();
        await new NpgsqlCommand(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures/pre-ledger-schema.sql")), connection).ExecuteNonQueryAsync();
        await new NpgsqlCommand(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures/base-prerequisite.sql")), connection).ExecuteNonQueryAsync();
        var orderId = Guid.NewGuid();
        const string sql = """
            INSERT INTO roles(role_id,role_name) VALUES (@role,'Customer');
            INSERT INTO accounts(user_id,role_id,email,full_name,status) VALUES (@user,@role,'legacy@example.test','Legacy Customer','Active');
            INSERT INTO refund_policies(refund_policy_id,created_by_user_id,policy_type,name,status,policy_family_id,version,effective_from)
              VALUES (@policy,@user,'Rental','Legacy policy','Active',@policy,1,now());
            INSERT INTO orders(order_id,customer_user_id,order_type,total_amount,status,order_code,subtotal_amount,payment_status,refund_policy_id)
              VALUES (@order,@user,'Rental',1000,'Confirmed','LEGACY-ORDER',1000,'Paid',@policy);
            INSERT INTO payments(payment_id,order_id,payment_type,amount,payment_method,provider_transaction_id,status,purpose) VALUES
              (@paid,@order,'Collection',1000,'BankTransfer','SEPAY:old-paid','Succeeded','RentalFee'),
              (@intent,@order,'Collection',1000,'BankTransfer',NULL,'Pending','RentalFee'),
              (@partial,@order,'Collection',100,'BankTransfer',@reference,'Pending','RentalFee');
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (key, value) in new Dictionary<string, Guid> { ["role"] = Guid.NewGuid(), ["user"] = Guid.NewGuid(), ["policy"] = Guid.NewGuid(), ["order"] = orderId, ["paid"] = Guid.NewGuid(), ["intent"] = Guid.NewGuid(), ["partial"] = Guid.NewGuid() }) command.Parameters.AddWithValue(key, value);
        command.Parameters.AddWithValue("reference", NpgsqlTypes.NpgsqlDbType.Text, duplicateBareIntent ? DBNull.Value : "SEPAY:old-partial");
        await command.ExecuteNonQueryAsync();
        return (connection, orderId);
    }

    [Fact]
    public async Task Migration_PreservesLegacyPendingAndPaidWithoutInventingTransactions()
    {
        var (connection, orderId) = await LegacyDatabase(); await using var cleanup = connection;
        await new NpgsqlCommand(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures/payment-ledger-migration.sql")), connection).ExecuteNonQueryAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
        Assert.Equal("Paid", (await db.Orders.SingleAsync(x => x.OrderId == orderId)).PaymentStatus);
        var payments = await db.Payments.Where(x => x.OrderId == orderId).ToListAsync();
        Assert.Equal(3, payments.Count); Assert.Equal(2, payments.Count(x => x.Status == "Pending"));
        Assert.All(payments, p => Assert.Matches("^RTP[0-9A-F]{16}$", p.TransferCode!));
        Assert.Equal(0L, (long)(await new NpgsqlCommand("SELECT count(*) FROM bank_transactions", connection).ExecuteScalarAsync())!);
    }

    [Fact]
    public async Task ConflictingLegacyIntents_FailMigrationAtomicallyWithoutDeletingMoney()
    {
        var (connection, _) = await LegacyDatabase(duplicateBareIntent: true); await using var cleanup = connection;
        await Assert.ThrowsAsync<PostgresException>(() => new NpgsqlCommand(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures/payment-ledger-migration.sql")), connection).ExecuteNonQueryAsync());
        await new NpgsqlCommand("ROLLBACK", connection).ExecuteNonQueryAsync();
        Assert.Equal(3L, (long)(await new NpgsqlCommand("SELECT count(*) FROM payments", connection).ExecuteScalarAsync())!);
        Assert.Equal(0L, (long)(await new NpgsqlCommand("SELECT count(*) FROM information_schema.columns WHERE table_schema='public' AND table_name='payments' AND column_name='transfer_code'", connection).ExecuteScalarAsync())!);
    }
}
