using System.Globalization;
using AutoMapper;
using Catalog.Common.DTOs;
using Catalog.GRPC;
using Catalog.GRPC.Mappings;

namespace Catalog.Tests.Unit;

/// <summary>
/// The gRPC contract carries decimals as strings, so this profile is the seam where a
/// formatting or parsing slip silently corrupts market numbers between Catalog and Game.
/// </summary>
public class CatalogGrpcMappingProfileTests
{
    private readonly IMapper _mapper = new MapperConfiguration(
        cfg => cfg.AddProfile<CatalogGrpcMappingProfile>(),
        NullLoggerFactory.Instance
    ).CreateMapper();

    [Fact]
    public void Maps_an_event_onto_the_price_response()
    {
        var dto = new EventDto
        {
            Id = Guid.NewGuid(),
            Pot = 11m,
            PoolYes = 2m,
            PoolNo = 3m,
            PriceYes = 0.6m,
            PriceNo = 0.4m,
        };

        var response = _mapper.Map<GetEventPriceResponse>(dto);

        Assert.Equal("11", response.Pot);
        Assert.Equal("2", response.PoolYes);
        Assert.Equal("3", response.PoolNo);
    }

    [Fact]
    public void Reads_an_update_request_into_the_repository_dto()
    {
        var eventId = Guid.NewGuid();
        var request = new UpdateEventPriceRequest
        {
            EventId = eventId.ToString(),
            Pot = "11",
            PoolYes = "0.0909090909",
            PoolNo = "11",
        };

        var dto = _mapper.Map<UpdateEventDto>(request);

        Assert.Equal(eventId, dto.Id);
        Assert.Equal(11m, dto.Pot);
        Assert.Equal(0.0909090909m, dto.PoolYes);
        Assert.Equal(11m, dto.PoolNo);
    }

    [Fact]
    public void Parses_request_decimals_culture_invariantly()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            var request = new UpdateEventPriceRequest
            {
                EventId = Guid.NewGuid().ToString(),
                Pot = "1.5",
                PoolYes = "0.5",
                PoolNo = "2.0",
            };

            var dto = _mapper.Map<UpdateEventDto>(request);

            Assert.Equal(1.5m, dto.Pot);
            Assert.Equal(0.5m, dto.PoolYes);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact(
        Skip = "Bug: EventDto -> GetEventPriceResponse leaves decimal->string to AutoMapper's "
            + "default ToString(), which uses the ambient culture, while Game parses with "
            + "InvariantCulture. See Catalog/Catalog.GRPC/Mappings/CatalogGrpcMappingProfile.cs"
    )]
    public void Formats_response_decimals_culture_invariantly()
    {
        // Game parses these strings with InvariantCulture. If Catalog writes them with the
        // ambient culture, a de-DE host sends "0,5" and Game reads back 5 - a tenfold error
        // in a pool value, with no exception anywhere to notice it.
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            var dto = new EventDto
            {
                Id = Guid.NewGuid(),
                Pot = 1.5m,
                PoolYes = 0.5m,
                PoolNo = 2m,
            };

            var response = _mapper.Map<GetEventPriceResponse>(dto);

            Assert.Equal("1.5", response.Pot);
            Assert.Equal("0.5", response.PoolYes);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact(
        Skip = "Bug: ConstructUsing parses Pot/PoolYes/PoolNo unconditionally, so an unset "
            + "field arrives as proto3's default \"\" and throws FormatException instead of mapping "
            + "to null. See Catalog/Catalog.GRPC/Mappings/CatalogGrpcMappingProfile.cs"
    )]
    public void Accepts_a_partial_update_that_touches_only_the_pools()
    {
        // UpdateEventDto makes every value optional and EventRepository honours that, but a
        // proto3 string defaults to "" rather than null, so an unset Pot reaches decimal.Parse
        // as an empty string.
        var request = new UpdateEventPriceRequest
        {
            EventId = Guid.NewGuid().ToString(),
            PoolYes = "2",
            PoolNo = "3",
        };

        var dto = _mapper.Map<UpdateEventDto>(request);

        Assert.Null(dto.Pot);
        Assert.Equal(2m, dto.PoolYes);
        Assert.Equal(3m, dto.PoolNo);
    }
}
