using Jellyfin.Plugin.SubtitleAutoAlign.Configuration;
using Xunit;

namespace Jellyfin.Plugin.SubtitleAutoAlign.Tests;

public class PluginConfigurationValidationTests
{
    [Fact]
    public void Validate_DefaultConfiguration_HasNoProblems()
    {
        var config = new PluginConfiguration();

        var problems = PluginConfigurationValidator.Validate(config);

        Assert.Empty(problems);
    }

    [Fact]
    public void Validate_EmptyFfSubSyncPath_IsValid_MeansAutoDetect()
    {
        var config = new PluginConfiguration { FfSubSyncPath = string.Empty };

        var problems = PluginConfigurationValidator.Validate(config);

        Assert.Empty(problems);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_NonPositiveTimeout_ReportsProblem(int timeout)
    {
        var config = new PluginConfiguration { TimeoutSeconds = timeout };

        var problems = PluginConfigurationValidator.Validate(config);

        Assert.Contains(problems, p => p.Contains("TimeoutSeconds"));
    }
}
