using Catalog.GRPC;

namespace Game.GrpcClients;

public interface ICatalogGrpcClient
{
    public Task<GetEventPriceResponse> GetEventPriceAsync(Guid eventId);

    public Task<UpdateEventPriceResponse> UpdateEventPriceAsync(
        Guid eventId,
        decimal? pot = null,
        decimal? poolYes = null,
        decimal? poolNo = null
    );
}
