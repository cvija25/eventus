using AutoMapper;
using Catalog.Common.DTOs;
using Catalog.Common.Entities;
using Catalog.Common.Mappings;
using Common.Enums;

namespace Catalog.Tests.Unit;

public class EventMappingProfileTests
{
    private readonly IMapper _mapper = new MapperConfiguration(
        cfg => cfg.AddProfile<EventMappingProfile>(),
        NullLoggerFactory.Instance
    ).CreateMapper();

    private static Event EventWith(decimal poolYes, decimal poolNo) =>
        new()
        {
            Id = Guid.NewGuid(),
            Title = "Will it rain?",
            OwnerId = Guid.NewGuid(),
            PoolYes = poolYes,
            PoolNo = poolNo,
            Pot = 1m,
        };

    [Fact]
    public void Configuration_is_valid()
    {
        new MapperConfiguration(
            cfg => cfg.AddProfile<EventMappingProfile>(),
            NullLoggerFactory.Instance
        ).AssertConfigurationIsValid();
    }

    [Fact]
    public void A_balanced_market_prices_both_sides_at_a_half()
    {
        var dto = _mapper.Map<EventDto>(EventWith(1m, 1m));

        Assert.Equal(0.5m, dto.PriceYes);
        Assert.Equal(0.5m, dto.PriceNo);
    }

    [Fact]
    public void Price_of_a_side_is_driven_by_the_opposing_pool()
    {
        // A small Yes pool means Yes is scarce, so Yes should be the expensive side.
        var dto = _mapper.Map<EventDto>(EventWith(poolYes: 1m, poolNo: 3m));

        Assert.Equal(0.75m, dto.PriceYes);
        Assert.Equal(0.25m, dto.PriceNo);
    }

    [Fact]
    public void Prices_always_sum_to_one()
    {
        var dto = _mapper.Map<EventDto>(EventWith(0.125m, 8m));

        Assert.Equal(1m, dto.PriceYes!.Value + dto.PriceNo!.Value);
    }

    [Fact]
    public void An_empty_market_prices_at_zero_rather_than_dividing_by_zero()
    {
        // decimal division by zero throws rather than returning NaN, so this guard is the
        // difference between a 0 price and a 500 on every event listing.
        var dto = _mapper.Map<EventDto>(EventWith(0m, 0m));

        Assert.Equal(0m, dto.PriceYes);
        Assert.Equal(0m, dto.PriceNo);
    }

    [Fact]
    public void Carries_the_identity_and_outcome_across()
    {
        var entity = EventWith(1m, 1m);
        entity.Outcome = MarketOutcome.Yes;

        var dto = _mapper.Map<EventDto>(entity);

        Assert.Equal(entity.Id, dto.Id);
        Assert.Equal(entity.Title, dto.Title);
        Assert.Equal(entity.OwnerId, dto.OwnerId);
        Assert.Equal(entity.Pot, dto.Pot);
        Assert.Equal(MarketOutcome.Yes, dto.Outcome);
    }
}
