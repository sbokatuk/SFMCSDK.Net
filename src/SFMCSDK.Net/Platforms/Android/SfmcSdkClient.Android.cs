using Com.Salesforce.Marketingcloud.Sfmcsdk;
using Com.Salesforce.Marketingcloud.Sfmcsdk.Components.Events;
using Com.Salesforce.Marketingcloud.Sfmcsdk.Components.Logging;

// The binding assembly is SFMCSDK.Net.Android, and inside namespace SFMCSDK.Net an unqualified
// `Android.*` would resolve against that sibling namespace first if the binding ever grew a type
// there - so the platform namespaces below are reached through global:: throughout, the same wart
// (and the same fix) Net.Agora's Android legs carry.
using AndroidLogLevel = Com.Salesforce.Marketingcloud.Sfmcsdk.Components.Logging.LogLevel;

namespace SFMCSDK.Net;

/// <summary>
/// The Android half of <see cref="SfmcSdkClient"/>, over the SFMCSDK.Net.Android binding
/// (namespace <c>Com.Salesforce.Marketingcloud.Sfmcsdk</c>).
/// </summary>
public sealed partial class SfmcSdkClient
{
    /// <remarks>
    /// <c>SFMCSdk.configure</c> takes the application <c>Context</c> and reports the terminal
    /// status through a Kotlin <c>Function1</c>; the binding's Additions supply the
    /// <see cref="Action{T}"/> overload used here. <c>Application.Context</c> rather than a
    /// caller-supplied one, deliberately: the SDK outlives any Activity, holding a shorter-lived
    /// Context would leak it, and it is what keeps the cross-platform surface free of a
    /// parameter only one platform could use.
    /// </remarks>
    private async partial Task InitializeCore(SfmcSdkOptions options, CancellationToken cancellationToken)
    {
        if (options.LogLevel is { } level)
        {
            // Before configure, so the initialization itself is captured. AndroidLogger is the
            // SDK's own logcat outputter - the platform-natural sink, matching os_log on iOS.
            SFMCSdk.SetLogging(ToNative(level), new ILogListener.AndroidLogger());
        }

        // An empty module config: this façade drives the SFMC SDK *core*. Module packages
        // (MobilePush and friends) configure themselves through the raw binding's
        // SFMCSdkModuleConfig.Builder - the escape hatch named on ISfmcSdkClient.
        var config = new SFMCSdkModuleConfig.Builder().Build();

        // The callback fires exactly once with the terminal InitializationStatus. Its parameter
        // is deliberately discarded unnamed-and-untyped: the binding marks the status *class*
        // obsolete in favour of the IInitializationStatus interface, and naming either type here
        // would be CS0618 under TreatWarningsAsErrors for information nothing acts on - with the
        // empty config above there are no modules whose failure it could report.
        await AwaitNativeCompletion(
            complete => SFMCSdk.Configure(
                global::Android.App.Application.Context, config, _ => complete()),
            "initialization", options.InitializationTimeout, cancellationToken).ConfigureAwait(false);
    }

    /// <remarks>
    /// v3 identity is an immutable record on the SDK instance: rebuild it and assign it back.
    /// The instance itself arrives through <c>requestSdk</c>, which queues the callback until the
    /// SDK is operational - which is what makes these edits legal before InitializeAsync, and
    /// also what makes them fire-and-forget (see <see cref="ISfmcIdentity"/>).
    /// </remarks>
    private partial void SetProfileIdCore(string profileId) =>
        SFMCSdk.RequestSdk(sdk =>
            sdk.Identity = sdk.Identity.NewBuilder().SetProfileId(profileId).Build());

    private partial void SetAttributeCore(string key, string value) =>
        SFMCSdk.RequestSdk(sdk =>
            sdk.Identity = sdk.Identity.NewBuilder().PutAttribute(key, value).Build());

    private partial void ClearAttributeCore(string key) =>
        SFMCSdk.RequestSdk(sdk =>
            sdk.Identity = sdk.Identity.NewBuilder().ClearAttribute(key).Build());

    private partial void TrackCustomEventCore(string name, IReadOnlyDictionary<string, string>? attributes)
    {
        Event? custom;

        if (attributes is { Count: > 0 })
        {
            // Values cross JNI as java.lang.String. The shared half already refused null values,
            // so every conversion here is total.
            var javaAttributes = new Dictionary<string, Java.Lang.Object>(attributes.Count);
            foreach (var pair in attributes)
            {
                javaAttributes[pair.Key] = new Java.Lang.String(pair.Value);
            }

            custom = EventManager.CustomEvent(name, javaAttributes);
        }
        else
        {
            custom = EventManager.CustomEvent(name);
        }

        // customEvent is Kotlin-nullable: a name the SDK's own validation rejects (too long, a
        // reserved prefix) comes back null rather than throwing. Surfaced as the argument error
        // it is - dropping the event silently is what the raw binding does, and is exactly the
        // sharp edge this façade exists to file off.
        if (custom is null)
        {
            throw new ArgumentException(
                $"The native SFMC SDK rejected the custom event name '{name}'.", nameof(name));
        }

        SFMCSdk.Track(custom);
    }

    /// <remarks>
    /// The static initialization state - NONE, INITIALIZING, READY or ERROR. Android's richer
    /// per-module detail lives on the instance <c>requestSdk</c> delivers asynchronously, which
    /// a synchronous property cannot await; see <see cref="ISfmcSdkClient.DiagnosticState"/>.
    /// </remarks>
    private partial string DiagnosticStateCore() =>
        SFMCSdk.InitializationState?.ToString() ?? "unavailable";

    private static AndroidLogLevel ToNative(SfmcLogLevel level) => level switch
    {
        SfmcLogLevel.Debug => AndroidLogLevel.Debug!,
        SfmcLogLevel.Warning => AndroidLogLevel.Warn!,
        SfmcLogLevel.Error => AndroidLogLevel.Error!,
        SfmcLogLevel.None => AndroidLogLevel.None!,
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Unknown SfmcLogLevel."),
    };
}
