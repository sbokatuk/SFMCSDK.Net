using Foundation;
using Security;

namespace SFMCSDK.Net.DeviceTests;

/// <summary>
/// The iOS head's precondition: the keychain accepts a write. Not a property of the façade, so it
/// is not one of the shared checks, but every iOS result depends on it. The SDK stores its keys in
/// the keychain and, refused it, does not initialize and says nothing, so the first shared check
/// would fail as a 60-second initialization timeout. Failing here with the status code names the
/// cause in one line; see the project file for how this app gets its entitlement.
/// </summary>
public static class KeychainCheck
{
    public static readonly SmokeTest Test = new("keychain_is_usable", () =>
    {
        var probe = new SecRecord(SecKind.GenericPassword)
        {
            Service = "com.sbokatuk.sfmcnet.devicetests.probe",
            Account = "probe",
        };

        SecKeyChain.Remove(probe);
        var status = SecKeyChain.Add(new SecRecord(SecKind.GenericPassword)
        {
            Service = probe.Service,
            Account = probe.Account,
            ValueData = NSData.FromString("probe"),
        });
        SecKeyChain.Remove(probe);

        SmokeTests.Reporter($"keychain: SecItemAdd -> {status} ({(int)status})");
        if (status != SecStatusCode.Success)
        {
            throw new InvalidOperationException(
                $"The keychain is not usable (SecItemAdd returned {status}, {(int)status}), so the SDK " +
                "cannot initialize. On a simulator this means the app was built without entitlements.");
        }

        return Task.CompletedTask;
    });
}
