using System.Net.Sockets;

namespace Eventus.Testing;

/// <summary>
/// Points Testcontainers at a usable container runtime.
/// <para>
/// CI runners expose a real Docker socket and need no help. Developer machines here run
/// rootless Podman, whose socket speaks the Docker API but lives under $XDG_RUNTIME_DIR
/// rather than /var/run/docker.sock. Probing for it keeps the same fixtures working in both
/// places without a per-machine DOCKER_HOST export.
/// </para>
/// <para>
/// The probe connects rather than checking for the file: a machine with Docker installed but
/// the user outside the <c>docker</c> group has a /var/run/docker.sock that exists and refuses
/// every connection, which is exactly the case this needs to fall through.
/// </para>
/// </summary>
public static class ContainerEnvironment
{
    private const string DockerSocket = "/var/run/docker.sock";
    private static readonly object Gate = new();
    private static bool _configured;

    public static void Ensure()
    {
        lock (Gate)
        {
            if (_configured)
                return;
            _configured = true;

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOCKER_HOST")))
                return;

            if (CanConnect(DockerSocket))
                return;

            var podmanSocket = Path.Combine(RuntimeDirectory(), "podman", "podman.sock");
            if (!CanConnect(podmanSocket))
                return;

            Environment.SetEnvironmentVariable("DOCKER_HOST", $"unix://{podmanSocket}");

            // Ryuk needs a privileged container bind-mounting the runtime socket, which rootless
            // Podman refuses. The fixtures dispose their own containers, so it is redundant here.
            Environment.SetEnvironmentVariable("TESTCONTAINERS_RYUK_DISABLED", "true");
        }
    }

    private static bool CanConnect(string socketPath)
    {
        if (!File.Exists(socketPath))
            return false;

        try
        {
            using var socket = new Socket(
                AddressFamily.Unix,
                SocketType.Stream,
                ProtocolType.Unspecified
            );
            socket.Connect(new UnixDomainSocketEndPoint(socketPath));
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
        catch (PlatformNotSupportedException)
        {
            return false;
        }
    }

    private static string RuntimeDirectory() =>
        Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR") ?? $"/run/user/{GetEffectiveUid()}";

    [System.Runtime.InteropServices.DllImport("libc", EntryPoint = "geteuid")]
    private static extern uint GetEffectiveUid();
}
