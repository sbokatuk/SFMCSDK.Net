using Xunit;

namespace SFMCSDK.Net.UnitTests;

/// <summary>
/// Pins the option defaults. They are part of the public contract - an app that passes no
/// options gets exactly this behaviour, and a changed default is a behaviour change every
/// consumer inherits silently, so a failure here is the prompt to write it into the release
/// notes rather than ship it as a surprise.
/// </summary>
public class SfmcSdkOptionsTests
{
    [Fact]
    public void Initialization_timeout_defaults_to_thirty_seconds()
    {
        Assert.Equal(TimeSpan.FromSeconds(30), new SfmcSdkOptions().InitializationTimeout);
    }

    [Fact]
    public void Log_level_defaults_to_null_leaving_the_platform_default_in_place()
    {
        Assert.Null(new SfmcSdkOptions().LogLevel);
    }

    [Fact]
    public void Options_are_a_record_so_with_expressions_work_for_partial_overrides()
    {
        var options = new SfmcSdkOptions { LogLevel = SfmcLogLevel.Debug };
        var adjusted = options with { InitializationTimeout = TimeSpan.FromSeconds(5) };

        // The point being pinned: overriding one property must not disturb the other - the
        // record-ness of the type is what makes partial overrides safe to recommend in docs.
        Assert.Equal(SfmcLogLevel.Debug, adjusted.LogLevel);
        Assert.Equal(TimeSpan.FromSeconds(5), adjusted.InitializationTimeout);
        Assert.Equal(TimeSpan.FromSeconds(30), options.InitializationTimeout);
    }

    [Fact]
    public async Task A_non_positive_timeout_is_refused_before_the_initialization_is_consumed()
    {
        var client = new SfmcSdkClient();
        var options = new SfmcSdkOptions { InitializationTimeout = TimeSpan.Zero };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.InitializeAsync(options));

        // The refused call must not have claimed the one-shot slot: the next attempt with sane
        // options reaches the platform seam (which on this neutral head reports itself).
        await Assert.ThrowsAsync<PlatformNotSupportedException>(() => client.InitializeAsync());
    }
}
