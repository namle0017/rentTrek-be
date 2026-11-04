namespace RenTrek.BLL.Tests;

public sealed class AdminPostgresFactAttribute : FactAttribute
{
    public AdminPostgresFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RENTREK_TEST_POSTGRES")))
            Skip = "Set RENTREK_TEST_POSTGRES to a disposable PostgreSQL server connection (database creation required).";
    }
}
