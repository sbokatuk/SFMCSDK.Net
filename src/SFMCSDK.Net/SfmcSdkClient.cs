namespace SFMCSDK.Net;

/// <summary>
/// Cross-platform <see cref="ISfmcSdkClient"/>. The platform halves live in Platforms/Android and
/// Platforms/Apple (Platforms/Neutral on the plain target frameworks); everything shared —
/// argument validation, the one-shot initialization guard, and turning the Android SDK's callback
/// into an awaitable call — lives here.
/// </summary>
/// <remarks>
/// <para>
/// <b>Create one and share it.</b> The native SDK on both platforms is a process-wide singleton
/// behind static entry points, so a second client would drive exactly the same native state as
/// the first. The initialization guard is per-instance — it is the instance that promises
/// one-shot semantics — which is why the client is meant to live as a singleton (register it as
/// one in DI). Two instances would let two <see cref="InitializeAsync"/> calls through to one
/// native SDK, and upstream forbids configuring it twice.
/// </para>
/// <para>
/// <b>Initialization is one-shot, even on failure.</b> The guard is deliberately not released
/// when initialization fails or times out: <c>SFMCSdk.configure</c> is itself not retryable, and
/// on a timeout the first attempt's native initialization is likely still running — a retry would
/// race it. A process that needs a fresh attempt restarts; that is the native SDKs' own model.
/// </para>
/// </remarks>
public sealed partial class SfmcSdkClient : ISfmcSdkClient
{
    /// <summary>0 until <see cref="InitializeAsync"/> claims it; the claim is never returned.</summary>
    private int _initializationClaimed;

    /// <summary>0 until an <see cref="InitializeAsync"/> call has completed successfully.</summary>
    private int _initialized;

    /// <inheritdoc />
    public ISfmcIdentity Identity { get; }

    /// <inheritdoc />
    public string DiagnosticState => DiagnosticStateCore();

    /// <inheritdoc />
    public bool IsSupported => SupportedCore();

    /// <inheritdoc />
    public bool IsInitialized => Volatile.Read(ref _initialized) == 1;

    /// <summary>
    /// Creates the client. Nothing native happens here — construction is valid on every target
    /// framework, including the neutral ones, so shared code and tests can build the object graph
    /// anywhere and only the members that reach the native SDK are platform-bound.
    /// </summary>
    public SfmcSdkClient()
    {
        Identity = new IdentityFacade(this);
    }

    /// <inheritdoc />
    public Task InitializeAsync(SfmcSdkOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new SfmcSdkOptions();

        if (options.InitializationTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                options.InitializationTimeout,
                "SfmcSdkOptions.InitializationTimeout must be positive.");
        }

        // Validation above, claim below: a call refused for a bad argument has not consumed the
        // one initialization this client performs.
        if (Interlocked.CompareExchange(ref _initializationClaimed, 1, 0) != 0)
        {
            throw new InvalidOperationException(
                "InitializeAsync has already been called on this SfmcSdkClient. Initialization " +
                "is one-shot: the native SFMC SDK is a process-wide singleton whose configure " +
                "cannot be repeated, so the guard stays claimed even if the first call failed. " +
                "Await the first call instead of issuing another.");
        }

        // The platform call starts here, synchronously - only the flag-setting is deferred, so a
        // neutral head still throws PlatformNotSupportedException out of this method rather than
        // from an awaited task.
        return MarkInitialized(InitializeCore(options, cancellationToken));
    }

    /// <summary>
    /// Awaits the platform initialization and, only on success, publishes
    /// <see cref="IsInitialized"/>. A failed or timed-out initialization leaves the flag false
    /// while the one-shot claim stays taken - the two answer different questions ("may I try?"
    /// versus "is it up?"), and conflating them would let a retry through after a timeout whose
    /// native initialization is likely still running.
    /// </summary>
    private async Task MarkInitialized(Task initialization)
    {
        await initialization.ConfigureAwait(false);
        Volatile.Write(ref _initialized, 1);
    }

    /// <inheritdoc />
    public void TrackCustomEvent(string name, IReadOnlyDictionary<string, string>? attributes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (attributes is not null)
        {
            foreach (var pair in attributes)
            {
                if (string.IsNullOrWhiteSpace(pair.Key))
                {
                    throw new ArgumentException(
                        $"Event '{name}' has a null or blank attribute key.", nameof(attributes));
                }

                if (pair.Value is null)
                {
                    // Checked here rather than left to the platforms, where it would surface as a
                    // JNI NullPointerException on Android and an NSInvalidArgumentException on
                    // iOS - neither of which names the key.
                    throw new ArgumentException(
                        $"Event '{name}' attribute '{pair.Key}' has a null value. Use " +
                        "ISfmcIdentity.ClearAttribute to remove identity attributes; event " +
                        "attributes have no null representation on either platform.",
                        nameof(attributes));
                }
            }
        }

        TrackCustomEventCore(name, attributes);
    }

    // The platform seams. One implementation directory supplies the bodies per target framework -
    // Platforms/Android, Platforms/Apple, or Platforms/Neutral (which throws from every seam) -
    // selected in the csproj. A target framework matching none of them fails to compile, which is
    // the intended outcome: an assembly whose API silently did nothing would be worse.

    private partial Task InitializeCore(SfmcSdkOptions options, CancellationToken cancellationToken);

    /// <summary>
    /// Whether this build has a native SFMC SDK underneath it - true from the Android and iOS legs,
    /// false from the neutral one. A plain answer rather than a throw, because it is what code
    /// guards <em>on</em>; see <see cref="ISfmcSdkClient.IsSupported"/>.
    /// </summary>
    private static partial bool SupportedCore();

    private partial void SetProfileIdCore(string profileId);

    private partial void SetAttributeCore(string key, string value);

    private partial void ClearAttributeCore(string key);

    private partial void TrackCustomEventCore(string name, IReadOnlyDictionary<string, string>? attributes);

    private partial string DiagnosticStateCore();

    /// <summary>
    /// Runs one native start call and awaits the completion it reports, bounded by the configured
    /// timeout and the caller's token. Shared so the mechanics are written (and tested) once; the
    /// Android initialization is the caller today, and the iOS one deliberately is not — its
    /// zero-module completion never fires, so there is nothing to await (see Platforms/Apple).
    /// </summary>
    /// <param name="start">Starts the native call; the argument is invoked once on completion.</param>
    /// <param name="operation">Human-readable name for the timeout message.</param>
    /// <param name="timeout">How long to wait before failing with <see cref="TimeoutException"/>.</param>
    /// <param name="cancellationToken">The caller's token — cancellation wins over the timeout.</param>
    private static async Task AwaitNativeCompletion(
        Action<Action> start, string operation, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var expiry = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, expiry.Token);

        await using var registration = linked.Token.Register(() =>
        {
            // Distinguishes the two reasons the linked token can fire: the caller's cancellation
            // surfaces as OperationCanceledException carrying their token, an expired timeout as
            // TimeoutException naming the operation - so a hung callback is tellable from an
            // impatient caller in a crash report.
            if (cancellationToken.IsCancellationRequested)
            {
                pending.TrySetCanceled(cancellationToken);
            }
            else
            {
                pending.TrySetException(new TimeoutException(
                    $"The native SFMC SDK did not report {operation} within " +
                    $"{timeout.TotalSeconds:0}s. The wait was abandoned, not the SDK - its own " +
                    "initialization may still complete in the background."));
            }
        }).ConfigureAwait(false);

        start(() => pending.TrySetResult(true));

        await pending.Task.ConfigureAwait(false);
    }

    /// <summary>
    /// The <see cref="ISfmcIdentity"/> handed out by <see cref="Identity"/>: shared validation,
    /// then the owning client's platform seams. A nested type rather than the client implementing
    /// the interface itself, so <c>client.SetProfileId(...)</c> is not an API — the identity
    /// operations read as what they are, edits of one sub-object.
    /// </summary>
    private sealed class IdentityFacade(SfmcSdkClient owner) : ISfmcIdentity
    {
        public void SetProfileId(string profileId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
            owner.SetProfileIdCore(profileId);
        }

        public void SetAttribute(string key, string value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ArgumentNullException.ThrowIfNull(value);
            owner.SetAttributeCore(key, value);
        }

        public void ClearAttribute(string key)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            owner.ClearAttributeCore(key);
        }
    }
}
