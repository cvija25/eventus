using Grpc.Core;

namespace Catalog.Tests.Integration;

/// <summary>
/// The minimum ServerCallContext needed to invoke a service method directly. Grpc.Core.Testing
/// would supply one, but it drags in the deprecated native Grpc.Core runtime.
/// </summary>
internal sealed class TestServerCallContext : ServerCallContext
{
    protected override string MethodCore => "/catalog.Catalog/Test";
    protected override string HostCore => "localhost";
    protected override string PeerCore => "ipv4:127.0.0.1:0";
    protected override DateTime DeadlineCore => DateTime.UtcNow.AddMinutes(1);
    protected override Metadata RequestHeadersCore { get; } = [];
    protected override CancellationToken CancellationTokenCore => CancellationToken.None;
    protected override Metadata ResponseTrailersCore { get; } = [];
    protected override Status StatusCore { get; set; }
    protected override WriteOptions? WriteOptionsCore { get; set; }
    protected override AuthContext AuthContextCore { get; } =
        new(null, new Dictionary<string, List<AuthProperty>>());
    protected override IDictionary<object, object> UserStateCore { get; } =
        new Dictionary<object, object>();

    protected override ContextPropagationToken CreatePropagationTokenCore(
        ContextPropagationOptions? options
    ) => throw new NotSupportedException();

    protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders) =>
        Task.CompletedTask;
}
