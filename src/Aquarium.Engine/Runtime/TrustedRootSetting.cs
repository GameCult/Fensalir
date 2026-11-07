namespace Aquarium.Engine.Runtime;

/// <summary>
/// The client runtime trusted root as given on the command line or in AQUARIUM_CLIENT_TRUSTED_ROOT. The environment
/// value is taken (read and cleared) the first time anything is resolved, on every path, and cached: children do not
/// inherit it and a second resolution does not silently fall back to the default root. A blank flag value, a flag with
/// no value or a blank environment value is refused.
/// </summary>
internal sealed class TrustedRootSetting(Func<string?> takeEnvironment)
{
    private const string Flag = "--client-trusted-root";
    private const string Variable = "AQUARIUM_CLIENT_TRUSTED_ROOT";
    private readonly Lazy<string?> fromEnvironment = new(takeEnvironment);

    public static TrustedRootSetting FromProcessEnvironment() => new(() =>
    {
        var value = Environment.GetEnvironmentVariable(Variable);
        Environment.SetEnvironmentVariable(Variable, null);
        return value;
    });

    public string? Resolve(IReadOnlyCollection<string> args)
    {
        var environment = fromEnvironment.Value;
        if (environment is not null && string.IsNullOrWhiteSpace(environment))
        {
            throw new ArgumentException($"{Variable} is set but empty; an empty value would fall back to the default root.");
        }

        var values = args.ToArray();
        for (var index = 0; index < values.Length; index++)
        {
            if (!string.Equals(values[index], Flag, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (index + 1 >= values.Length || string.IsNullOrWhiteSpace(values[index + 1]))
            {
                throw new ArgumentException($"{Flag} needs a directory; an empty value would fall back to the default root.");
            }

            return values[index + 1];
        }

        return environment;
    }
}
