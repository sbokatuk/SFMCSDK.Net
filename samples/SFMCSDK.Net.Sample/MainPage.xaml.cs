namespace SFMCSDK.Net.Sample;

/// <summary>
/// Everything on this page goes through <see cref="ISfmcSdkClient"/> - no platform namespace is
/// imported and no <c>#if</c> appears anywhere in this app, which is the point being
/// demonstrated. Compare with the two binding repositories' samples, which drive the same three
/// operations through the raw per-platform surfaces.
/// </summary>
public partial class MainPage : ContentPage
{
    private readonly ISfmcSdkClient _sdk;

    public MainPage(ISfmcSdkClient sdk)
    {
        InitializeComponent();
        _sdk = sdk;
    }

    private async void OnInitializeClicked(object? sender, EventArgs e)
    {
        InitializeButton.IsEnabled = false;
        AppendLog("Initializing…");

        try
        {
            // Debug logging routes the native SDK's own lines to logcat / os_log, so what the
            // SDK does with these calls is observable in the platform log while you poke at the
            // buttons.
            await _sdk.InitializeAsync(new SfmcSdkOptions { LogLevel = SfmcLogLevel.Debug });

            StatusLabel.Text = $"Initialized. {_sdk.DiagnosticState}";
            AppendLog("InitializeAsync completed.");
            ProfileIdEntry.IsEnabled = true;
            ProfileIdButton.IsEnabled = true;
            TrackButton.IsEnabled = true;
        }
        catch (Exception exception)
        {
            // InvalidOperationException here means the one-shot guard: initialization already
            // ran (or is running) on this client. The button stays disabled either way - a
            // second attempt is exactly what the guard exists to refuse.
            StatusLabel.Text = "Initialization failed.";
            AppendLog($"{exception.GetType().Name}: {exception.Message}");
        }
    }

    private void OnSetProfileIdClicked(object? sender, EventArgs e)
    {
        var profileId = ProfileIdEntry.Text?.Trim();
        if (string.IsNullOrEmpty(profileId))
        {
            AppendLog("Enter a profile id first.");
            return;
        }

        // Fire-and-forget by contract: the SDK batches identity changes on its own schedule,
        // and this call returns as soon as the edit is queued on the platform underneath.
        _sdk.Identity.SetProfileId(profileId);
        AppendLog($"Profile id set to '{profileId}'.");
    }

    private void OnTrackClicked(object? sender, EventArgs e)
    {
        try
        {
            _sdk.TrackCustomEvent("sample_button_tapped", new Dictionary<string, string>
            {
                ["source"] = "SFMCSDK.Net.Sample",
            });

            AppendLog("Tracked 'sample_button_tapped'.");
        }
        catch (ArgumentException exception)
        {
            // The façade turns a native rejection of the event name into an ArgumentException
            // on both platforms - the raw Android surface would have dropped the event silently.
            AppendLog($"Event rejected: {exception.Message}");
        }
    }

    private void AppendLog(string line)
        => LogLabel.Text = $"{DateTime.Now:HH:mm:ss}  {line}\n{LogLabel.Text}";
}
