using Microsoft.Extensions.Configuration;

namespace Common.Web;

public static class ConfigurationGuard
{
    public static string Require(this IConfiguration configuration, string key)
    {
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException(
                $"Configuration key '{key}' is missing. Containers read it from "
                    + $"compose.override.yaml as '{key.Replace(":", "__")}'; IDE runs read it "
                    + "from appsettings.Development.json."
            );

        return value;
    }
}
