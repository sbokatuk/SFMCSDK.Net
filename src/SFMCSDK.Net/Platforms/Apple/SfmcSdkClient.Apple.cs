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
    /// Appended to the timeout message, because on iOS a timeout almost always has one cause and
    /// nothing else in the process says so: the SDK keeps its keys in the keychain and, refused
    /// it, neither initializes nor reports.
    /// </summary>
    private const string KeychainHint =
        "On iOS the SDK does not initialize, and reports nothing, when it cannot use the " +
        "keychain. On a simulator that means the app was built without entitlements: give the " +
        "simulator build a keychain-access-groups entitlement (see the SFMCSDK.Net README, " +
        "Troubleshooting).";

    /// <remarks>
    /// <c>initializeSdk</c>'s completion reports one status per configured module, so with the
    /// empty configuration built here it reports none, but it does fire: once per call, a few
    /// milliseconds after the call returns, on a background thread, with an empty array. That
    /// was measured on sfmc-sdk-ios 5.0.0 and on 4.0.1 by the SFMCSDK.Net.iOS device tests. What
    /// the SDK needs before it completes is the keychain; without it the completion never comes,
    /// which is what the timeout and <see cref="KeychainHint"/> are for.
    /// </remarks>
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

        // The statuses are discarded: there is one per configured module and none is configured,
        // so the array is always empty here. Its arrival is the signal. An earlier version of
        // this method passed no completion and waited a fixed two seconds instead, on the belief
        // that the zero-module completion never fires; that was a simulator build without
        // keychain access, misread as upstream behaviour.
        await AwaitNativeCompletion(
            complete => SFMCSdk.InitializeSdk(config, _ => complete()),
            "initialization", options.InitializationTimeout, cancellationToken,
            KeychainHint).ConfigureAwait(false);
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
