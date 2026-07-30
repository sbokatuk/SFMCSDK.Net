using Xunit;

namespace SFMCSDK.Net.UnitTests;

/// <summary>
/// Pins down <see cref="NullSfmcSdkClient"/> — the no-op client shipped for heads with no SFMC SDK.
/// What matters about it is the pair of promises consumers register it on: nothing throws, and
/// nothing silently swallows a caller's own bug.
/// </summary>
public class NullSfmcSdkClientTests
{
    [Fact]
    public async Task Every_member_answers_without_throwing()
    {
        // Deliberately one test over the whole surface: the promise is "no member of this type
        // throws", and asserting it member-by-member would let a newly added member slip through
        // unasserted while still reading as covered.
        ISfmcSdkClient client = new NullSfmcSdkClient();

        Assert.False(client.IsSupported);
        Assert.False(client.IsInitialized);

        await client.InitializeAsync();
        Assert.True(client.IsInitialized);

        client.Identity.SetProfileId("contact-key");
        client.Identity.SetAttribute("plan", "pro");
        client.Identity.ClearAttribute("plan");
        client.TrackCustomEvent("checkout_started", new Dictionary<string, string> { ["step"] = "1" });
        client.TrackCustomEvent("checkout_started");

        Assert.NotEmpty(client.DiagnosticState);
    }

    [Fact]
    public void DiagnosticState_says_plainly_that_no_sdk_ran()
    {
        // A diagnostics screen or bug report must not read as "an SDK with nothing to say".
        Assert.Contains("no-op", new NullSfmcSdkClient().DiagnosticState, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Initialization_is_not_one_shot()
    {
        // The real client's guard protects a native SDK that cannot be configured twice. There is
        // nothing here to protect, and a fake that threw on a second call would fail tests that
        // exercise a restart path.
        var client = new NullSfmcSdkClient();

        await client.InitializeAsync();
        await client.InitializeAsync();
    }

    [Fact]
    public async Task Cancellation_is_honoured()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new NullSfmcSdkClient().InitializeAsync(cancellationToken: cancelled.Token));
    }

    [Fact]
    public void Caller_bugs_still_surface()
    {
        // The line between "does nothing" and "hides mistakes" - and the validation mirrors the real
        // façade's member for member, so a test passing against this fake means the same call
        // passes against the real client.
        var client = new NullSfmcSdkClient();

        Assert.Throws<ArgumentException>(() => client.TrackCustomEvent("  "));
        Assert.Throws<ArgumentException>(
            () => client.TrackCustomEvent("evt", new Dictionary<string, string> { ["  "] = "v" }));
        Assert.Throws<ArgumentException>(
            () => client.TrackCustomEvent("evt", new Dictionary<string, string> { ["k"] = null! }));
        Assert.Throws<ArgumentException>(() => client.Identity.SetProfileId(""));
        Assert.Throws<ArgumentException>(() => client.Identity.SetAttribute("", "pro"));
        Assert.Throws<ArgumentNullException>(() => client.Identity.SetAttribute("plan", null!));
        Assert.Throws<ArgumentException>(() => client.Identity.ClearAttribute(" "));
    }
}
