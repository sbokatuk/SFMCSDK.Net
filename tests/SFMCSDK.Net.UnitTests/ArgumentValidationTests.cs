using Xunit;

namespace SFMCSDK.Net.UnitTests;

/// <summary>
/// The shared argument validation, asserted on the neutral assembly where a validation failure
/// is distinguishable from platform behaviour by exception type alone: arguments the façade
/// refuses throw <see cref="ArgumentException"/>-family errors <em>before</em> any platform seam
/// runs, so a <see cref="PlatformNotSupportedException"/> in these tests would mean bad input
/// leaked through to a platform that would fail with something unhelpful (a JNI NPE, an ObjC
/// invalid-argument abort) in a real app.
/// </summary>
public class ArgumentValidationTests
{
    [Fact]
    public void TrackCustomEvent_refuses_a_null_name()
    {
        Assert.Throws<ArgumentNullException>(() => new SfmcSdkClient().TrackCustomEvent(null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void TrackCustomEvent_refuses_a_blank_name(string name)
    {
        Assert.Throws<ArgumentException>(() => new SfmcSdkClient().TrackCustomEvent(name));
    }

    [Fact]
    public void TrackCustomEvent_refuses_a_blank_attribute_key_naming_the_event()
    {
        var error = Assert.Throws<ArgumentException>(() => new SfmcSdkClient().TrackCustomEvent(
            "checkout_started", new Dictionary<string, string> { [" "] = "value" }));

        Assert.Contains("checkout_started", error.Message);
    }

    [Fact]
    public void TrackCustomEvent_refuses_a_null_attribute_value_naming_the_key()
    {
        var error = Assert.Throws<ArgumentException>(() => new SfmcSdkClient().TrackCustomEvent(
            "checkout_started", new Dictionary<string, string> { ["plan"] = null! }));

        // The key is the debugging handle - a bare "null value" would send the reader off to
        // log every attribute themselves.
        Assert.Contains("plan", error.Message);
    }

    [Fact]
    public void TrackCustomEvent_accepts_empty_attributes_as_equivalent_to_none()
    {
        // An empty dictionary must behave like null attributes: validation passes and the call
        // reaches the platform seam (the neutral one, here).
        Assert.Throws<PlatformNotSupportedException>(() => new SfmcSdkClient().TrackCustomEvent(
            "checkout_started", new Dictionary<string, string>()));
    }

    [Fact]
    public void Identity_refuses_blank_arguments()
    {
        var identity = new SfmcSdkClient().Identity;

        Assert.Throws<ArgumentNullException>(() => identity.SetProfileId(null!));
        Assert.Throws<ArgumentException>(() => identity.SetProfileId("  "));
        Assert.Throws<ArgumentException>(() => identity.SetAttribute("", "value"));
        Assert.Throws<ArgumentNullException>(() => identity.SetAttribute("plan", null!));
        Assert.Throws<ArgumentException>(() => identity.ClearAttribute(" "));
    }

    [Fact]
    public void Identity_accepts_an_empty_attribute_value()
    {
        // Empty is a legal attribute value on both platforms (distinct from clearing); only null
        // is refused. On this neutral head acceptance shows up as the seam being reached.
        Assert.Throws<PlatformNotSupportedException>(
            () => new SfmcSdkClient().Identity.SetAttribute("plan", ""));
    }
}
