using Catalog.Common.DTOs;
using Catalog.Common.Extensions;
using Catalog.GRPC;
using Catalog.GRPC.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddGrpc();
builder.Services.AddGrpcReflection();
builder.Services.AddCatalogCommon(builder.Configuration);
builder.Services.AddAutoMapper(configuration =>
{
    configuration.CreateMap<EventDto, GetEventPriceResponse>().ReverseMap();
    configuration
        .CreateMap<UpdateEventPriceRequest, UpdateEventDto>()
        .ConstructUsing(src => new UpdateEventDto(
            Guid.Parse(src.EventId),
            (int)src.PriceYes,
            (int)src.PriceNo,
            (int)src.PotSize
        ));
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapGrpcService<CatalogService>();
app.MapGrpcReflectionService();
app.MapGet(
    "/",
    () =>
        "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909"
);

app.Run();
