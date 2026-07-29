namespace SFMCSDK.Net;

/// <summary>
/// What <see cref="SfmcSdkClient.InitializeAsync"/> accepts. Construct one with the properties
/// you want changed, or pass nothing and take the defaults — every member has one.
/// </summary>
/// <remarks>
/// A record with <c>init</c> setters rather than a mutable options bag: initialization is
/// one-shot (see <see cref="SfmcSdkClient"/>), so options that could be edited after the call
/// would only ever be a trap.
/// </remarks>
public sealed record SfmcSdkOptions
{
    /// <summary>
    /// How long <see cref="SfmcSdkClient.InitializeAsync"/> waits for the native SDK to report
    /// before failing with <see cref="TimeoutException"/>. Default: 30 seconds.
    /// </summary>
    /// <remarks>
    /// Only the Android path can actually spend this: <c>SFMCSdk.configure</c> reports through a
    /// callback, and a callback that never comes — a broken binding, a deadlocked initializer —
    /// would otherwise hang the app's startup await forever. On iOS the zero-module completion
    /// never fires by (verified) design and initialization completes after a short settle, so the
    /// timeout has nothing to bound there. Must be positive.
    /// </remarks>
    public TimeSpan InitializationTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Native SDK log verbosity, applied immediately before initialization so the initialization
    /// itself is captured. Null — the default — leaves each platform's own default in place.
    /// </summary>
    public SfmcLogLevel? LogLevel { get; init; }
}
