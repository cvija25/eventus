using Catalog.Common.DTOs;
using Catalog.GRPC.Services;
using Catalog.Common.Extensions;
using Catalog.GRPC;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddGrpc();
builder.Services.AddGrpcReflection();
builder.Services.AddCatalogCommon(builder.Configuration);
builder.Services.AddAutoMapper(configuration =>
{
    configuration.CreateMap<EventDto, GetEventPriceResponse>().ReverseMap();
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapGrpcService<CatalogService>();
app.MapGrpcReflectionService();
app.MapGet("/",
    () =>
        "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

app.Run();