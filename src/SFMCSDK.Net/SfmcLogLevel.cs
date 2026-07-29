namespace SFMCSDK.Net;

/// <summary>
/// How much the native SDK logs, expressed once for both platforms. Set it through
/// <see cref="SfmcSdkOptions.LogLevel"/> and it is applied before initialization —
/// <c>SFMCSdk.setLogger</c> on iOS, <c>SFMCSdk.setLogging</c> on Android.
/// </summary>
/// <remarks>
/// The native enums do not match member-for-member, so this is the intersection: iOS's
/// <c>SFMCSdkLogLevel</c> carries an extra <c>Fault</c> severity between <c>Error</c> and
/// <c>None</c> that Android has no counterpart for, and it is deliberately not surfaced here —
/// a level that silently degraded to <c>Error</c> on one platform would make the two logs
/// disagree about what was captured. Reach for the platform namespaces
/// (<c>SFMCSDK</c> / <c>Com.Salesforce.Marketingcloud.Sfmcsdk.Components.Logging</c>) to use it.
/// Output goes to each platform's natural sink — os_log on iOS, logcat on Android.
/// </remarks>
public enum SfmcLogLevel
{
    /// <summary>Everything, including per-request detail. For development only.</summary>
    Debug,

    /// <summary>Recoverable problems worth seeing — maps to each platform's <c>Warn</c>.</summary>
    Warning,

    /// <summary>Failures only.</summary>
    Error,

    /// <summary>Nothing at all.</summary>
    None,
}
