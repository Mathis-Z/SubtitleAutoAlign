using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.SubtitleAutoAlign.Configuration;

/// <summary>
/// Pure validation for <see cref="PluginConfiguration"/>, kept separate from
/// the configuration class so it is trivially unit testable.
/// </summary>
public static class PluginConfigurationValidator
{
    /// <summary>
    /// Validates the given configuration.
    /// </summary>
    /// <param name="configuration">Configuration to validate.</param>
    /// <returns>A list of human-readable validation problems; empty if valid.</returns>
    public static IReadOnlyList<string> Validate(PluginConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var problems = new List<string>();

        if (configuration.TimeoutSeconds <= 0)
        {
            problems.Add("TimeoutSeconds must be greater than zero.");
        }

        return problems;
    }
}
