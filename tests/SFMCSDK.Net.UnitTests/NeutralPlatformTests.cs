using Xunit;

namespace SFMCSDK.Net.UnitTests;

/// <summary>
/// Pins down the behaviour of the neutral (plain net8.0/net9.0/net10.0) leg: construction
/// succeeds, and every member that would reach the native SDK throws
/// <see cref="PlatformNotSupportedException"/> with a message that says where the real
/// implementation lives. This test project resolves the façade's real neutral assembly (see the
/// csproj), so what is asserted here is exactly what a consumer's shared class library hits.
/// </summary>
/// <remarks>
/// The message assertions name the platform heads rather than pin exact wording, so the message
/// can be reworded freely - but one that stops telling the reader where the real implementation
/// lives fails. Throwing (rather than no-oping) is itself the contract under test: an identity
/// edit that silently vanished on a Windows head would read as data loss in Marketing Cloud.
/// </remarks>
public class NeutralPlatformTests
{
    private static void AssertNamesThePlatformHeads(PlatformNotSupportedException error)
    {
        Assert.Contains("net*-android", error.Message);
        Assert.Contains("net*-ios", error.Message);
    }

    [Fact]
    public void Construction_succeeds_so_shared_object_graphs_can_be_built_anywhere()
    {
        var client = new SfmcSdkClient();

        Assert.NotNull(client.Identity);
    }

    [Fact]
    public async Task Initialize_throws_and_names_the_platform_heads()
    {
        var error = await Assert.ThrowsAsync<PlatformNotSupportedException>(
            () => new SfmcSdkClient().InitializeAsync());

        AssertNamesThePlatformHeads(error);
    }

    [Fact]
    public void Identity_members_throw_and_name_the_platform_heads()
    {
        var identity = new SfmcSdkClient().Identity;

        AssertNamesThePlatformHeads(
            Assert.Throws<PlatformNotSupportedException>(() => identity.SetProfileId("contact-key")));
        AssertNamesThePlatformHeads(
            Assert.Throws<PlatformNotSupportedException>(() => identity.SetAttribute("plan", "pro")));
        AssertNamesThePlatformHeads(
            Assert.Throws<PlatformNotSupportedException>(() => identity.ClearAttribute("plan")));
    }

    [Fact]
    public void TrackCustomEvent_throws_and_names_the_platform_heads()
    {
        var error = Assert.Throws<PlatformNotSupportedException>(
            () => new SfmcSdkClient().TrackCustomEvent("checkout_started"));

        AssertNamesThePlatformHeads(error);
    }

    [Fact]
    public void DiagnosticState_throws_and_names_the_platform_heads()
    {
        var error = Assert.Throws<PlatformNotSupportedException>(
            () => _ = new SfmcSdkClient().DiagnosticState);

        AssertNamesThePlatformHeads(error);
    }
}
