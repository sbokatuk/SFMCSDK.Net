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
    /// Both platforms report initialization through a callback — <c>SFMCSdk.configure</c>'s on
    /// Android, <c>initializeSdk</c>'s completion block on iOS — and a callback that never comes
    /// would otherwise hang the app's startup await forever. Normally they arrive within
    /// milliseconds. On iOS the one known way to miss it is an app the keychain refuses: the SDK
    /// keeps its keys there and, without it, neither initializes nor reports. A simulator build
    /// with no entitlements is refused (<c>errSecMissingEntitlement</c>, -34018); a device build
    /// signed with a provisioning profile is not. Must be positive and at most
    /// <see cref="int.MaxValue"/> milliseconds (about 24.8 days).
    /// </remarks>
    public TimeSpan InitializationTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Native SDK log verbosity, applied immediately before initialization so the initialization
    /// itself is captured. Null — the default — leaves each platform's own default in place.
    /// </summary>
    public SfmcLogLevel? LogLevel { get; init; }
}
