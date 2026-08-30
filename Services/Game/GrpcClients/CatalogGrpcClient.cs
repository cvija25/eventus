using Catalog.GRPC;
using CatalogProto = Catalog.GRPC.Catalog;

namespace Game.GrpcClients;

public class CatalogGrpcClient(CatalogProto.CatalogClient client) : ICatalogGrpcClient
{
    public async Task<GetEventPriceResponse> GetEventPriceAsync(Guid eventId) =>
        await client.GetEventPriceAsync(new GetEventPriceRequest { EventId = eventId.ToString() });

    public async Task<UpdateEventPriceResponse> UpdateEventPriceAsync(
        Guid eventId,
        decimal? pot = null,
        decimal? poolYes = null,
        decimal? poolNo = null
    )
    {
        var request = new UpdateEventPriceRequest { EventId = eventId.ToString() };
        if (pot.HasValue)
            request.Pot = pot.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (poolYes.HasValue)
            request.PoolYes = poolYes.Value.ToString(
                System.Globalization.CultureInfo.InvariantCulture
            );
        if (poolNo.HasValue)
            request.PoolNo = poolNo.Value.ToString(
                System.Globalization.CultureInfo.InvariantCulture
            );
        return await client.UpdateEventPriceAsync(request);
    }
}
