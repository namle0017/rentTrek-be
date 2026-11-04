using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using RenTrek.API.Common;
using RenTrek.API.Controllers;
using RenTrek.API.Extensions;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;
using RentalOpsRequest = RenTrek.BLL.DTOs.RentalOps.Requests.CreateRentalChargeRequest;
using WarehouseRequest = RenTrek.BLL.DTOs.Warehouse.Requests.CreateRentalChargeRequest;

namespace RenTrek.API.Tests;

public sealed class SwaggerTests
{
    [Fact]
    public async Task GetSwagger_AllControllers_GeneratesDocumentWithDistinctRentalChargeSchemas()
    {
        await using var app = CreateApp();

        var document = app.Services.GetRequiredService<ISwaggerProvider>().GetSwagger("v1");

        Assert.NotEmpty(document.Paths);
        Assert.Contains(typeof(RentalOpsRequest).FullName!, document.Components.Schemas.Keys);
        Assert.Contains(typeof(WarehouseRequest).FullName!, document.Components.Schemas.Keys);
        Assert.All(document.Components.Schemas.Keys, key => Assert.Matches("^[a-zA-Z0-9._-]+$", key));

        var upload = document.Paths["/api/v1/users/me/avatar"].Operations[OperationType.Post];
        var file = upload.RequestBody.Content["multipart/form-data"].Schema.Properties["file"];
        Assert.Equal("string", file.Type);
        Assert.Equal("binary", file.Format);
    }

    [Fact]
    public async Task GenerateSchema_GenericWrappers_PreservesDistinctDtoTypes()
    {
        await using var app = CreateApp();
        var generator = app.Services.GetRequiredService<ISchemaGenerator>();
        var repository = new SchemaRepository();

        var rentalOps = generator.GenerateSchema(typeof(ApiResponse<RentalOpsRequest>), repository);
        var warehouse = generator.GenerateSchema(typeof(ApiResponse<WarehouseRequest>), repository);

        Assert.NotEqual(rentalOps.Reference.Id, warehouse.Reference.Id);
        Assert.Equal(typeof(RentalOpsRequest).FullName,
            repository.Schemas[rentalOps.Reference.Id].Properties["data"].Reference.Id);
        Assert.Equal(typeof(WarehouseRequest).FullName,
            repository.Schemas[warehouse.Reference.Id].Properties["data"].Reference.Id);
        Assert.All(repository.Schemas.Keys, key => Assert.Matches("^[a-zA-Z0-9._-]+$", key));
    }

    private static WebApplication CreateApp()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddControllers().AddApplicationPart(typeof(TripBookingsController).Assembly);
        builder.Services.AddSwaggerWithJwt();

        var app = builder.Build();
        app.MapControllers();
        return app;
    }
}
