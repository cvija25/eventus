using Common.Enums;

namespace Catalog.Common.DTOs;

public record ResolveEventDto(Guid Id, MarketOutcome Outcome);
