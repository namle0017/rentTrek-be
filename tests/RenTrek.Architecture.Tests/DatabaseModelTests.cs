using Microsoft.EntityFrameworkCore;
using RenTrek.DAL.Data;
using System.Text.Json;

namespace RenTrek.Architecture.Tests;

public sealed class DatabaseModelTests
{
    [Fact]
    public void EfCoreAndPostgreSqlProviderPackagesUseNet8CompatibleMajorVersions()
    {
        var packages = new List<string>();
        foreach (var file in Directory.GetFiles(Path.Combine(SourceConventionsTests.RepositoryRoot, "src"), "packages.lock.json", SearchOption.AllDirectories)
            .Where(file => !file.Replace('\\', '/').Split('/').Any(part => part is "bin" or "obj")))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(file));
            foreach (var framework in document.RootElement.GetProperty("dependencies").EnumerateObject())
            {
                foreach (var package in framework.Value.EnumerateObject().Where(package => package.Name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
                    || package.Name == "Npgsql.EntityFrameworkCore.PostgreSQL"))
                {
                    var version = Version.Parse(package.Value.GetProperty("resolved").GetString()!);
                    Assert.True(version.Major == 8 && version.Minor == 0, $"{file}: {package.Name} {version} is incompatible with the documented EF Core 8.0 stack.");
                    packages.Add(package.Name);
                }
            }
        }
        Assert.Contains("Npgsql.EntityFrameworkCore.PostgreSQL", packages);
        Assert.Contains("Microsoft.EntityFrameworkCore.Design", packages);
    }

    [Fact]
    public void DateTimeOffsetColumnsAreConvertedToUtcBeforeWrite()
    {
        // Npgsql từ chối DateTimeOffset có offset khác 0 (FE gửi +07:00 => 500 khi SaveChanges)
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql().Options;
        using var context = new AppDbContext(options);
        var vn = new DateTimeOffset(2026, 12, 6, 6, 0, 0, TimeSpan.FromHours(7));
        var props = context.Model.GetEntityTypes().SelectMany(e => e.GetProperties())
            .Where(p => p.ClrType == typeof(DateTimeOffset) || p.ClrType == typeof(DateTimeOffset?)).ToList();
        Assert.Contains(props, p => p.ClrType == typeof(DateTimeOffset?));
        foreach (var p in props)
        {
            var stored = (DateTimeOffset)p.GetTypeMapping().Converter!.ConvertToProvider(vn)!;
            Assert.Equal(TimeSpan.Zero, stored.Offset);
            Assert.Equal(vn, stored);
        }
    }

    [Fact]
    public void PostgreSqlModelIsValidOfflineAndUsesSnakeCaseTablesAndColumns()
    {
        // No connection opened, no application host/seed service, no database credentials.
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql().Options;
        using var context = new AppDbContext(options);
        var model = context.Model.GetRelationalModel();
        Assert.NotEmpty(model.Tables);
        foreach (var table in model.Tables)
        {
            Assert.Matches("^[a-z][a-z0-9_]*$", table.Name);
            Assert.NotNull(table.PrimaryKey);
            foreach (var column in table.Columns)
                Assert.Matches("^[a-z][a-z0-9_]*$", column.Name);
        }
    }
}
