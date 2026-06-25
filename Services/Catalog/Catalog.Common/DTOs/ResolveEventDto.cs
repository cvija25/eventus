using Catalog.Common.Entities;

namespace Catalog.Common.DTOs;

public record ResolveEventDto(Guid Id, EventOutcome Outcome);
