namespace SFMCSDK.Net;

/// <summary>
/// Initializes the Salesforce Marketing Cloud SFMC SDK core and drives what it does from shared
/// code: identity edits and custom event tracking, with the same surface on Android and iOS.
///
/// The platform bindings underneath do not resemble each other — Android's <c>SFMCSdk</c> is
/// configured with a <c>Context</c> and reports through Kotlin callbacks, hands its instance out
/// via <c>requestSdk</c>, and rebuilds identity through an immutable builder; iOS's is all static,
/// reports per-module completion blocks, and edits identity in place through a modifier closure.
/// This is the layer that hides the difference behind one awaitable initialize and two ordinary
/// method groups.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately small. The façade carries the operations an app performs from shared code on
/// every screen — initialize, identify, track — and nothing else. For everything platform-shaped
/// (module configs, consent, in-app messaging models, encryption, the event bus), reach past it
/// to the bindings it is built on: namespace <c>Com.Salesforce.Marketingcloud.Sfmcsdk</c> on
/// Android (package SFMCSDK.Net.Android) and namespace <c>SFMCSDK</c> on iOS (package
/// SFMCSDK.Net.iOS). Both arrive with this package on their platform, so the escape hatch needs
/// no extra reference.
/// </para>
/// <para>
/// On the plain (neutral) target frameworks the interface exists so shared code can compile and
/// inject against it, and every member of the packaged implementation throws
/// <see cref="PlatformNotSupportedException"/> — see <see cref="SfmcSdkClient"/>.
/// </para>
/// </remarks>
public interface ISfmcSdkClient
{
    /// <summary>
    /// Initializes the SDK core with an empty module configuration, completing when the SDK has
    /// reported — Android's terminal initialization callback, or iOS's post-initialize settle
    /// (the zero-module completion never fires there; see <see cref="SfmcSdkClient"/>).
    /// </summary>
    /// <param name="options">Timeout and log level; null takes every default.</param>
    /// <param name="cancellationToken">
    /// Cancels the wait, not the native initialization — neither SDK exposes an abort.
    /// </param>
    /// <returns>A task that completes when the SDK is up.</returns>
    /// <exception cref="InvalidOperationException">
    /// Called a second time on the same client. Initialization is one-shot; see
    /// <see cref="SfmcSdkClient.InitializeAsync"/> for why the guard does not reset.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <see cref="SfmcSdkOptions.InitializationTimeout"/> is zero or negative.
    /// </exception>
    /// <exception cref="TimeoutException">
    /// The native SDK reported nothing within <see cref="SfmcSdkOptions.InitializationTimeout"/>
    /// (Android only — see that property).
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> fired.</exception>
    /// <exception cref="PlatformNotSupportedException">Neutral target framework.</exception>
    Task InitializeAsync(SfmcSdkOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// The registration identity: profile id (contact key) and string attributes. Usable before
    /// or after <see cref="InitializeAsync"/> — both SDKs queue early identity work — and always
    /// fire-and-forget; see <see cref="ISfmcIdentity"/>.
    /// </summary>
    ISfmcIdentity Identity { get; }

    /// <summary>
    /// Tracks a custom engagement event, queued and sent on the SDK's own batching schedule.
    /// </summary>
    /// <param name="name">
    /// The event name. Must not be null, empty or whitespace — and the native SDKs impose rules
    /// of their own (length, reserved prefixes); a name the native factory rejects throws
    /// <see cref="ArgumentException"/> rather than being dropped silently.
    /// </param>
    /// <param name="attributes">
    /// String attributes for the event, or null for none. Keys must be non-blank and values
    /// non-null. Strings only, deliberately: it is the one payload shape both platforms accept
    /// without a conversion policy this façade would have to invent.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> is blank or rejected by the native SDK, an attribute key is
    /// blank, or an attribute value is null.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    /// <exception cref="PlatformNotSupportedException">Neutral target framework.</exception>
    void TrackCustomEvent(string name, IReadOnlyDictionary<string, string>? attributes = null);

    /// <summary>
    /// What the native SDK says about itself right now, for logs and bug reports — never parse
    /// it. The shape is platform-owned and asymmetric by nature: iOS returns its state JSON
    /// (<c>SFMCSdk.state</c>); Android's equivalent detail lives on the instance
    /// <c>requestSdk</c> delivers asynchronously, so a synchronous property can only report the
    /// static initialization state (<c>NONE</c>/<c>INITIALIZING</c>/<c>READY</c>/<c>ERROR</c>),
    /// and that is what it does.
    /// </summary>
    /// <exception cref="PlatformNotSupportedException">Neutral target framework.</exception>
    string DiagnosticState { get; }
}
