using AutoMapper;
using Catalog.Common.DTOs;
using Catalog.Common.Entities;
using Catalog.Common.Extensions;
using Catalog.GRPC;
using Catalog.GRPC.Mappings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Catalog.Tests.Unit;

/// <summary>
/// Catalog.GRPC composes its mapper from two sources: AddCatalogCommon registers the entity
/// profile, and the host adds the wire profile on top. CatalogService needs both in one IMapper
/// - it maps an entity to a DTO through the repository, then that DTO onto the proto response -
/// so a registration that replaced rather than merged would fail only at runtime, on the first
/// bet anyone places.
/// </summary>
public class CatalogGrpcCompositionTests
{
    private static IMapper BuildMapperAsTheGrpcHostDoes()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:CatalogDb"] =
                        "Host=localhost;Database=unused;Username=postgres;Password=postgres",
                }
            )
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCatalogCommon(configuration);
        services.AddAutoMapper(cfg => cfg.AddProfile<CatalogGrpcMappingProfile>());

        return services.BuildServiceProvider().GetRequiredService<IMapper>();
    }

    [Fact]
    public void The_resolved_mapper_knows_both_catalog_profiles()
    {
        var mapper = BuildMapperAsTheGrpcHostDoes();

        var dto = mapper.Map<EventDto>(
            new Event
            {
                Id = Guid.NewGuid(),
                Title = "Will it rain?",
                OwnerId = Guid.NewGuid(),
                PoolYes = 1m,
                PoolNo = 3m,
                Pot = 4m,
            }
        );
        var response = mapper.Map<GetEventPriceResponse>(dto);

        Assert.Equal(0.75m, dto.PriceYes);
        Assert.Equal("4", response.Pot);
        Assert.Equal("1", response.PoolYes);
    }
}
