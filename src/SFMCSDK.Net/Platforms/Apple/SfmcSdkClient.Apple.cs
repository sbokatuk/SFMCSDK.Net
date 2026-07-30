using Foundation;

namespace SFMCSDK.Net;

/// <summary>
/// The iOS half of <see cref="SfmcSdkClient"/>, over the SFMCSDK.Net.iOS binding. The binding's
/// types live in namespace <c>SFMCSDK</c> — the parent of this assembly's <c>SFMCSDK.Net</c> —
/// so they resolve here without a using directive, through ordinary enclosing-namespace lookup.
/// </summary>
public sealed partial class SfmcSdkClient
{
    /// <summary>
    /// How long initialization waits after <c>initializeSdk</c> before reporting complete — long
    /// enough for the state machine to come up (the sibling binding repository's device tests
    /// settled on the same figure before reading state), short enough to stay well inside any
    /// sane <see cref="SfmcSdkOptions.InitializationTimeout"/>.
    /// </summary>
    private static readonly TimeSpan SettleDelay = TimeSpan.FromSeconds(2);

    private async partial Task InitializeCore(SfmcSdkOptions options, CancellationToken cancellationToken)
    {
        if (options.LogLevel is { } level)
        {
            // Before initializeSdk, so the initialization itself is captured. A fresh default
            // outputter writes to os_log - the platform-natural sink, matching logcat on Android.
            SFMCSdk.SetLogger(ToNative(level), new SFMCSdkLogOutputter());
        }

        // An empty module config: this façade drives the SFMC SDK *core*. Module packages
        // (MobilePush and friends) configure themselves through the raw binding's
        // SFMCSdkConfigBuilder - the escape hatch named on ISfmcSdkClient.
        var config = new SFMCSdkConfigBuilder().Build();

        // VERIFIED against sfmc-sdk-ios 4.0.1 on a simulator (and encoded in the sibling
        // SFMCSDK.Net.iOS device tests): initializeSdk's completion block reports PER-MODULE init
        // statuses, so with the empty config above it NEVER fires - upstream behaviour, not a
        // bridge failure. Passing a completion and awaiting it here would therefore always time
        // out. The call brings the SDK's state machine up regardless; "initialized" on this
        // platform means the call crossed the bridge and the SDK was given a moment to settle,
        // after which DiagnosticState answers.
        SFMCSdk.InitializeSdk(config, null);

        await Task.Delay(SettleDelay, cancellationToken).ConfigureAwait(false);
    }

    /// <remarks>
    /// Identity on iOS is a static object edited in place through a modifier block - the block
    /// runs synchronously, but the write it performs is still batched to the server on the SDK's
    /// own schedule, which is why the cross-platform contract stays fire-and-forget (see
    /// <see cref="ISfmcIdentity"/>).
    /// </remarks>
    private partial void SetProfileIdCore(string profileId) =>
        SFMCSdk.Identity.Edit(modifier =>
        {
            modifier.ProfileId = profileId;
            return modifier;
        });

    private partial void SetAttributeCore(string key, string value) =>
        SFMCSdk.Identity.Edit(modifier =>
        {
            modifier.AddAttribute(key, value);
            return modifier;
        });

    private partial void ClearAttributeCore(string key) =>
        SFMCSdk.Identity.Edit(modifier =>
        {
            modifier.ClearAttribute(key);
            return modifier;
        });

    private partial void TrackCustomEventCore(string name, IReadOnlyDictionary<string, string>? attributes)
    {
        NSDictionary<NSString, NSObject>? native = null;

        if (attributes is { Count: > 0 })
        {
            // Values cross the bridge as NSString. The shared half already refused null values,
            // so every conversion here is total.
            var keys = new NSString[attributes.Count];
            var values = new NSObject[attributes.Count];
            var index = 0;

            foreach (var pair in attributes)
            {
                keys[index] = new NSString(pair.Key);
                values[index] = new NSString(pair.Value);
                index++;
            }

            native = NSDictionary<NSString, NSObject>.FromObjectsAndKeys(values, keys, keys.Length);
        }

        SFMCSdkCustomEvent custom;
        try
        {
            custom = new SFMCSdkCustomEvent(name, native);
        }
        catch (Exception exception) when (exception is not ArgumentException)
        {
            // initWithName:attributes: is nullable: a name the SDK's own validation rejects
            // returns nil, which the binding surfaces as a bare Exception from InitializeHandle.
            // Rethrown as the argument error it is, matching the Android leg's null-return path -
            // the two platforms reject through different mechanics and must not fail differently.
            throw new ArgumentException(
                $"The native SFMC SDK rejected the custom event name '{name}'.", nameof(name), exception);
        }

        SFMCSdk.Track(custom);
    }


    private static partial bool SupportedCore() => true;

    /// <remarks>The SDK's own state JSON - <c>SFMCSdk.state</c>.</remarks>
    private partial string DiagnosticStateCore() => SFMCSdk.State;

    private static SFMCSdkLogLevel ToNative(SfmcLogLevel level) => level switch
    {
        SfmcLogLevel.Debug => SFMCSdkLogLevel.Debug,
        SfmcLogLevel.Warning => SFMCSdkLogLevel.Warn,
        SfmcLogLevel.Error => SFMCSdkLogLevel.Error,
        SfmcLogLevel.None => SFMCSdkLogLevel.None,
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Unknown SfmcLogLevel."),
    };
}
