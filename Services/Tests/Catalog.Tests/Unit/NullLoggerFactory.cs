using Microsoft.Extensions.Logging.Abstractions;

namespace Catalog.Tests.Unit;

/// <summary>AutoMapper 16 requires an ILoggerFactory; the tests have no use for its output.</summary>
internal static class NullLoggerFactory
{
    public static Microsoft.Extensions.Logging.ILoggerFactory Instance { get; } =
        new Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory();
}
