namespace SFMCSDK.Net;

/// <summary>
/// An <see cref="ISfmcSdkClient"/> that does nothing, successfully, on every target framework. For
/// the heads where the SFMC SDK does not exist (a MAUI app's Windows or Mac Catalyst head) and for
/// tests that want the façade out of the way.
/// </summary>
/// <remarks>
/// <para>
/// Shipped rather than left as an exercise: the alternative is every consumer hand-writing the same
/// handful of no-op members, and getting them wrong in the same way — a fake that throws from
/// <see cref="DiagnosticState"/>, or one that reports <see cref="IsSupported"/> true and then does
/// nothing.
/// </para>
/// <para>
/// It is not a spy: nothing is recorded and nothing is asserted. A test that needs to see what the
/// app asked for wants its own mock (the interface is small on purpose); this type is for the case
/// where the answer is "don't care, don't crash". Registering it is a deliberate decision to send no
/// data to Marketing Cloud on that head — <see cref="SfmcSdkClient"/> throws on neutral target
/// frameworks precisely so that decision cannot be made by accident.
/// </para>
/// <example>
/// The shape this exists for, in a MAUI app whose Windows head has no SDK:
/// <code>
/// builder.Services.AddSingleton&lt;ISfmcSdkClient&gt;(
///     new SfmcSdkClient() is { IsSupported: true } client ? client : new NullSfmcSdkClient());
/// </code>
/// </example>
/// </remarks>
public sealed class NullSfmcSdkClient : ISfmcSdkClient
{
    /// <inheritdoc />
    public ISfmcIdentity Identity { get; } = new NullSfmcIdentity();

    /// <summary>
    /// A fixed line naming this type, so a diagnostics screen or bug report says plainly that no SDK
    /// was driven rather than looking like an SDK with nothing to say.
    /// </summary>
    public string DiagnosticState => "SFMCSDK.Net no-op client: no native SDK was driven.";

    /// <summary>Always false — there is no native SDK behind this client.</summary>
    public bool IsSupported => false;

    /// <summary>
    /// True once <see cref="InitializeAsync"/> has been called. The no-op initialization succeeds,
    /// so this reports the client's own honest state rather than pretending an SDK came up.
    /// </summary>
    public bool IsInitialized { get; private set; }

    /// <summary>
    /// Validates nothing, initializes nothing, and completes successfully — so app startup that
    /// awaits initialization proceeds identically on a head with no SDK.
    /// </summary>
    /// <remarks>
    /// Not one-shot: the guard exists on the real client to protect a native SDK that cannot be
    /// configured twice, and there is nothing here to protect. Cancellation is honoured, because a
    /// caller that cancels startup should see that regardless of which client it holds.
    /// </remarks>
    public Task InitializeAsync(SfmcSdkOptions? options = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IsInitialized = true;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Discards the event, but validates it first — a blank event name is a bug on every head, and
    /// the fake is often the only implementation a unit test ever runs.
    /// </summary>
    public void TrackCustomEvent(string name, IReadOnlyDictionary<string, string>? attributes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (attributes is null)
        {
            return;
        }

        foreach (var pair in attributes)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
            {
                throw new ArgumentException(
                    $"Event '{name}' has a null or blank attribute key.", nameof(attributes));
            }

            if (pair.Value is null)
            {
                throw new ArgumentException(
                    $"Event '{name}' attribute '{pair.Key}' has a null value.", nameof(attributes));
            }
        }
    }

    private sealed class NullSfmcIdentity : ISfmcIdentity
    {
        public void SetProfileId(string profileId) => ArgumentException.ThrowIfNullOrWhiteSpace(profileId);

        public void SetAttribute(string key, string value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ArgumentNullException.ThrowIfNull(value);
        }

        public void ClearAttribute(string key) => ArgumentException.ThrowIfNullOrWhiteSpace(key);
    }
}
