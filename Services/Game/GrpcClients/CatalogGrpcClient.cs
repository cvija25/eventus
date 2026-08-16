using Catalog.GRPC;
using CatalogProto = Catalog.GRPC.Catalog;

namespace Game.GrpcClients;

public class CatalogGrpcClient(CatalogProto.CatalogClient client)
{
    public async Task<GetEventPriceResponse> GetEventPriceAsync(Guid eventId) =>
        await client.GetEventPriceAsync(new GetEventPriceRequest { EventId = eventId.ToString() });

    public async Task<UpdateEventPriceResponse> UpdateEventPriceAsync(
        Guid eventId,
        decimal? potSizeYes = null,
        decimal? potSizeNo = null
    )
    {
        var request = new UpdateEventPriceRequest { EventId = eventId.ToString() };
        if (potSizeYes.HasValue)
            request.PotSizeYes = potSizeYes.Value.ToString(
                System.Globalization.CultureInfo.InvariantCulture
            );
        if (potSizeNo.HasValue)
            request.PotSizeNo = potSizeNo.Value.ToString(
                System.Globalization.CultureInfo.InvariantCulture
            );
        return await client.UpdateEventPriceAsync(request);
    }
}
