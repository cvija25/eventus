# Catalog.Common

Shared library for the Catalog domain. Contains entities, DTOs, repository interfaces and implementations, EF Core context, and AutoMapper profiles.

Consumed by `Catalog.API` and any future service that needs access to the catalog database.

## Structure

```
Data/          — EventContext, IEventContext, EventContextFactory
Entities/      — Event
DTOs/          — CreateEventDto, UpdateEventDto, EventDto
Repositories/  — IEventRepository, EventRepository
Mappings/      — EventMappingProfile
Extensions/    — ServiceCollectionExtensions (AddCatalogCommon)
Migrations/    — EF Core migration files
```

## Registration

In your startup project:

```csharp
builder.Services.AddCatalogCommon(builder.Configuration);
```

Requires a `DefaultConnection` connection string in config:

```json
"ConnectionStrings": {
  "DefaultConnection": "Host=...;Database=eventus_catalog;Username=postgres;Password=postgres"
}
```

## Migrations

Migrations live in this project. The `EventContextFactory` reads config at design time so no startup project is needed.

**Add a migration** (run from repo root):
```bash
dotnet ef migrations add <MigrationName> --project Services/Catalog/Catalog.Common
```

**Apply to database:**
```bash
dotnet ef database update --project Services/Catalog/Catalog.Common
```

**Rollback to a previous migration:**
```bash
dotnet ef database update <MigrationName> --project Services/Catalog/Catalog.Common
```

**Remove the last unapplied migration:**
```bash
dotnet ef migrations remove --project Services/Catalog/Catalog.Common
```
