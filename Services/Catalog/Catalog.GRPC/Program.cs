using System.Globalization;
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
            decimal.Parse(src.Pot, CultureInfo.InvariantCulture),
            decimal.Parse(src.PoolYes, CultureInfo.InvariantCulture),
            decimal.Parse(src.PoolNo, CultureInfo.InvariantCulture)
        ))
        .ForAllMembers(opt => opt.Ignore());
});

var app = builder.Build();
await app.MigrateCatalogDatabase();

app.MapGrpcService<CatalogService>();
app.MapGrpcReflectionService();

app.Run();
