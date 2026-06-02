using Catalog.GRPC;
using CatalogProto = Catalog.GRPC.Catalog;

namespace Game.GrpcClients;

public class CatalogGrpcClient(CatalogProto.CatalogClient client)
{
    public async Task<GetEventPriceResponse> GetEventPriceAsync(Guid eventId) =>
        await client.GetEventPriceAsync(new GetEventPriceRequest { EventId = eventId.ToString() });

    public async Task<UpdateEventPriceResponse> UpdateEventPriceAsync(
        Guid eventId,
        long? priceYes = null,
        long? priceNo = null,
        long? potSize = null
    )
    {
        var request = new UpdateEventPriceRequest { EventId = eventId.ToString() };
        if (priceYes.HasValue)
            request.PriceYes = priceYes.Value;
        if (priceNo.HasValue)
            request.PriceNo = priceNo.Value;
        if (potSize.HasValue)
            request.PotSize = potSize.Value;
        return await client.UpdateEventPriceAsync(request);
    }
}
