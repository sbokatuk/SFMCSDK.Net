using SFMCSDK.Net;

namespace SFMCSDK.Net.DeviceTests;

/// <summary>One check: a name and something that throws when the façade misbehaves.</summary>
public sealed record SmokeTest(string Name, Func<Task> Execute);

/// <summary>
/// Drives the FAÇADE - not the raw bindings - over the packed SFMCSDK.Net package on a real
/// platform runtime. No credentials: the empty module configuration the façade builds points at
/// no tenant, so nothing registers anywhere. What is being proven is the packaging promise: the
/// dependency groups pulled the right platform binding in, and the façade's shared surface
/// drives it - initialization completes, identity and tracking cross into native code, and the
/// one-shot guard holds on-device exactly as the unit tests hold it on the neutral leg.
/// </summary>
/// <remarks>
/// The checks share one client and run in array order, deliberately: initialize-then-use-then-
/// reinitialize is the lifecycle under test, not four independent facts. The raw-binding smoke
/// tests live in the two platform repositories; repeating them here would test the same native
/// code twice and this package's code not at all.
/// </remarks>
public static class SmokeTests
{
    /// <summary>Where progress lines go; each platform host points this at its log sink.</summary>
    public static Action<string> Reporter { get; set; } = _ => { };

    private static readonly SfmcSdkClient Client = new();

    public static readonly SmokeTest[] All =
    [
        new("initialize_completes", async () =>
        {
            // Debug logging on, so a native failure under investigation is already captured in
            // the platform log the runner uploads. The timeout is generous for emulator cold
            // starts; the façade's own default would very likely do.
            await Client.InitializeAsync(new SfmcSdkOptions
            {
                LogLevel = SfmcLogLevel.Debug,
                InitializationTimeout = TimeSpan.FromSeconds(60),
            });

            Reporter("InitializeAsync completed");
        }),

        new("diagnostic_state_reports", () =>
        {
            var state = Client.DiagnosticState;
            Reporter($"DiagnosticState: {state}");

            if (string.IsNullOrWhiteSpace(state))
            {
                throw new InvalidOperationException("DiagnosticState returned nothing.");
            }

            return Task.CompletedTask;
        }),

        new("identity_accepts_a_profile_id", () =>
        {
            // Fire-and-forget by contract - no server round-trip is asserted, there is no
            // tenant - only that the call crosses into native code without throwing, which is
            // what a broken binding dependency breaks.
            Client.Identity.SetProfileId("sfmcsdk-net-device-test");
            Client.Identity.SetAttribute("suite", "SFMCSDK.Net.DeviceTests");
            Client.Identity.ClearAttribute("suite");

            return Task.CompletedTask;
        }),

        new("track_custom_event_does_not_throw", () =>
        {
            Client.TrackCustomEvent(
                "sfmc_net_e2e",
                new Dictionary<string, string> { ["source"] = "SFMCSDK.Net.DeviceTests" });

            return Task.CompletedTask;
        }),

        new("second_initialize_throws_the_documented_guard", async () =>
        {
            try
            {
                await Client.InitializeAsync();
            }
            catch (InvalidOperationException error)
            {
                Reporter($"guard message: {error.Message}");
                return;
            }

            throw new InvalidOperationException(
                "A second InitializeAsync completed instead of throwing the one-shot guard.");
        }),
    ];
}
