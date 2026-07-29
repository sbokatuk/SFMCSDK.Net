namespace SFMCSDK.Net;

/// <summary>
/// Edits the registration identity the SDK sends to Marketing Cloud: the profile id (contact
/// key) and its string attributes.
/// </summary>
/// <remarks>
/// <para>
/// Every member is fire-and-forget by design, because that is the only contract both platforms
/// can honour: Android's identity lives on the <c>SFMCSdk</c> instance that
/// <c>requestSdk</c> hands out asynchronously once the SDK is operational, while iOS edits a
/// static <c>Identity</c> through a modifier block that runs at once. The calls are safe in any
/// order relative to <see cref="ISfmcSdkClient.InitializeAsync"/> — both SDKs queue identity
/// work started early — but nothing here waits for the server to acknowledge anything: identity
/// changes are batched and sent on the SDK's own schedule.
/// </para>
/// <para>
/// Only string attributes are surfaced, because that is the intersection of the two native
/// surfaces (both platforms' identity attribute stores are string-to-string). Anything richer —
/// party identification fields on iOS, attribute maps on Android — is reachable through the
/// platform namespaces named in <see cref="ISfmcSdkClient"/>.
/// </para>
/// </remarks>
public interface ISfmcIdentity
{
    /// <summary>
    /// Sets the profile id — Marketing Cloud's contact key — that identifies this app install's
    /// user. Setting it again replaces the previous value.
    /// </summary>
    /// <param name="profileId">The contact key. Must not be null, empty or whitespace.</param>
    /// <exception cref="ArgumentException"><paramref name="profileId"/> is empty or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="profileId"/> is null.</exception>
    void SetProfileId(string profileId);

    /// <summary>
    /// Sets one string attribute on the registration, replacing any previous value for the key.
    /// </summary>
    /// <param name="key">The attribute name. Must not be null, empty or whitespace.</param>
    /// <param name="value">The attribute value. Must not be null; empty is allowed.</param>
    /// <exception cref="ArgumentException"><paramref name="key"/> is empty or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> or <paramref name="value"/> is null.</exception>
    void SetAttribute(string key, string value);

    /// <summary>
    /// Removes an attribute from the registration. Clearing a key that was never set is a no-op
    /// on both platforms, so this needs no existence check first.
    /// </summary>
    /// <param name="key">The attribute name. Must not be null, empty or whitespace.</param>
    /// <exception cref="ArgumentException"><paramref name="key"/> is empty or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is null.</exception>
    void ClearAttribute(string key);
}
