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
            src.HasPriceYes ? (int)src.PriceYes : null,
            src.HasPriceNo ? (int)src.PriceNo : null,
            src.HasPotSize ? (int)src.PotSize : null
        ));
});

var app = builder.Build();
await app.MigrateCatalogDatabase();

app.MapGrpcService<CatalogService>();
app.MapGrpcReflectionService();

app.Run();
