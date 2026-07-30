namespace SFMCSDK.Net;

/// <summary>
/// The neutral-target-framework half of <see cref="SfmcSdkClient"/> — the counterpart of
/// Platforms/Android and Platforms/Apple, compiled into the plain net8.0/net9.0/net10.0 build.
/// There is no native SFMC SDK on these target frameworks; construction succeeds so object
/// graphs and tests can be built anywhere, and every member that would reach the native SDK
/// throws instead.
/// </summary>
/// <remarks>
/// The leg exists so shared code — ViewModels, unit tests, a MAUI app's Windows head — can
/// reference the package and program against <see cref="ISfmcSdkClient"/> instead of failing
/// restore with NU1202. Run the same code in a net*-android or net*-ios application head and the
/// package resolves the platform bindings and these seams become the real implementations.
/// </remarks>
public sealed partial class SfmcSdkClient
{
    private static PlatformNotSupportedException NotSupported() => new(
        "SFMCSDK.Net has no native SFMC SDK on this target framework - the neutral build exists " +
        "so shared code can reference the package and program against ISfmcSdkClient. Run in a " +
        "net*-android or net*-ios application head, where the same package resolves the " +
        "SFMCSDK.Net.Android / SFMCSDK.Net.iOS bindings and this member drives the real SDK.");

    // Everything below satisfies the shared half's partial declarations. Throwing rather than
    // no-oping is deliberate: an identity edit or a tracked event that silently vanished on the
    // Windows head of a MAUI app would read as data loss in Marketing Cloud, with nothing logged
    // anywhere. Code that must run on neutral heads guards the calls or injects a fake.

    private partial Task InitializeCore(SfmcSdkOptions options, CancellationToken cancellationToken) =>
        throw NotSupported();

    private partial void SetProfileIdCore(string profileId) => throw NotSupported();

    private partial void SetAttributeCore(string key, string value) => throw NotSupported();

    private partial void ClearAttributeCore(string key) => throw NotSupported();

    private partial void TrackCustomEventCore(string name, IReadOnlyDictionary<string, string>? attributes) =>
        throw NotSupported();

    private partial string DiagnosticStateCore() => throw NotSupported();

    // The exception to the throwing rule, and the reason the rule is tolerable: this is the member
    // shared code branches on before calling any of the others.
    private static partial bool SupportedCore() => false;
}
