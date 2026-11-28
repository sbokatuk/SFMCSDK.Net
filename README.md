# SFMCSDK.Net

[![NuGet](https://img.shields.io/nuget/v/SFMCSDK.Net?label=nuget)](https://www.nuget.org/packages/SFMCSDK.Net)
[![release](https://github.com/sbokatuk/SFMCSDK.Net/actions/workflows/release.yml/badge.svg)](https://github.com/sbokatuk/SFMCSDK.Net/actions/workflows/release.yml)
[![Targets: net8.0 | net9.0 | net10.0](https://img.shields.io/badge/targets-net8.0%20%7C%20net9.0%20%7C%20net10.0-512BD4)](#installing)
[![SFMCSDK 5.0.0](https://img.shields.io/badge/SFMCSDK-5.0.0-099DFD)](https://github.com/salesforce-marketingcloud/sfmc-sdk-ios/releases)
[![sfmcsdk 3.3.0](https://img.shields.io/badge/sfmcsdk-3.3.0-099DFD)](https://salesforce-marketingcloud.github.io/MarketingCloudSDK-Android/)
[![Licence: MIT](https://img.shields.io/badge/licence-MIT-orange)](#licence)

**One Salesforce Marketing Cloud SFMC SDK API for .NET.** Awaitable initialization, identity
(contact key and attributes) and custom event tracking on Android and iOS, written once in shared
code — over the [SFMCSDK.Net.Android](https://github.com/sbokatuk/SFMCSDK.Net.Android) and
[SFMCSDK.Net.iOS](https://github.com/sbokatuk/SFMCSDK.Net.iOS) bindings.

```sh
dotnet add package SFMCSDK.Net
```

```csharp
using SFMCSDK.Net;

var sdk = new SfmcSdkClient();          // one per process - register it as a DI singleton

await sdk.InitializeAsync(new SfmcSdkOptions { LogLevel = SfmcLogLevel.Debug });

sdk.Identity.SetProfileId("contact-key");
sdk.Identity.SetAttribute("plan", "pro");

sdk.TrackCustomEvent("checkout_started", new Dictionary<string, string>
{
    ["cart_size"] = "3",
});
```

That is the whole shared-code surface, and it is the same call sites on both platforms — no
`#if`, no per-platform adapter.

---

## Contents

- [Why there is a cross-platform layer](#why-there-is-a-cross-platform-layer)
- [What the façade does and does not hide](#what-the-façade-does-and-does-not-hide)
- [Packages and versions](#packages-and-versions)
- [Installing](#installing)
- [Usage notes](#usage-notes)
- [How this repository works](#how-this-repository-works)
- [Building locally](#building-locally)
- [Tests](#tests)
- [Upgrading](#upgrading)
- [Releasing](#releasing)
- [Troubleshooting](#troubleshooting)
- [Licence](#licence)

---

## Why there is a cross-platform layer

The two platform bindings are faithful projections of what Salesforce ships, and consequently
share almost no shape. The same three operations, raw:

| Operation | Android (`Com.Salesforce.Marketingcloud.Sfmcsdk`) | iOS (`SFMCSDK`) |
| --- | --- | --- |
| Initialize | `SFMCSdk.Configure(context, config, status => …)` — needs a `Context`, reports one terminal status through a Kotlin `Function1` | `SFMCSdk.InitializeSdk(config, statuses => …)` — all static, reports **per-module** statuses through a block |
| Identity | `sdk.Identity = sdk.Identity.NewBuilder().SetProfileId(x).Build()` — an immutable record on an instance that `RequestSdk` hands out asynchronously | `SFMCSdk.Identity.Edit(m => { m.ProfileId = x; return m; })` — a static object edited in place through a modifier closure |
| Track | `SFMCSdk.Track(EventManager.CustomEvent(name, attrs))` — attributes are `IDictionary<string, Java.Lang.Object>` | `SFMCSdk.Track(new SFMCSdkCustomEvent(name, attrs))` — attributes are `NSDictionary<NSString, NSObject>` |

Writing that twice per app is the tax this package removes. It also files off the sharp edges the
raw surfaces leave: a custom event whose name the native SDK rejects **throws** here (Android's
factory returns null and the event silently vanishes; iOS's initializer returns nil), and an
initialization callback that never arrives **times out** with an exception naming the operation
instead of hanging the app's startup await forever.

On iOS, `InitializeAsync` awaits `initializeSdk`'s completion block. It reports one status per
configured module, and the façade configures none, so it fires with an empty array — a few
milliseconds after the call, measured on SFMCSDK 4.0.1 and 5.0.0. What it does need is the
keychain: the SDK keeps its keys there and, refused it, neither initializes nor reports, so the
`await` times out instead. A device build signed with a provisioning profile always has keychain
access; a simulator build needs an entitlement — see [Troubleshooting](#troubleshooting).

## What the façade does and does not hide

The façade carries what an app does from *shared* code — initialize once, identify the user,
track events, read a diagnostic line — and deliberately nothing else. It adds no abstraction over
things only one platform has, and it does not wrap surfaces an app touches from platform code
anyway.

Everything else stays reachable, because the packages underneath arrive with this one:

- **Android**: namespace `Com.Salesforce.Marketingcloud.Sfmcsdk` (package
  `SFMCSDK.Net.Android`) — module configs, the event bus, behaviors, encrypted storage, the
  in-app messaging models, `RequestSdk` itself.
- **iOS**: namespace `SFMCSDK` (package `SFMCSDK.Net.iOS`) — the config builder's per-module
  setters, consent/CDP, keychain helpers, the full event model.

Mixing is fine and expected: initialize and track through the façade, and configure a module or
subscribe to the event bus through the raw namespace in the same app. The façade builds an
**empty module config** on both platforms — module packages (MobilePush and friends) are
configured through the raw builders, which is where their options live.

On the plain `net8.0`/`net9.0`/`net10.0` target frameworks the package restores and compiles —
that is what lets a shared class library, a unit test, or a MAUI app's Windows head reference it
unconditionally — and every member that would reach the native SDK throws
`PlatformNotSupportedException` naming where the real implementation lives. Throwing, not
no-oping: an identity edit that silently vanished on a Windows head would read as data loss in
Marketing Cloud.

Two members are exempt and never throw, on any target framework, because they are what shared code
*branches on*: `IsSupported` (is there a native SDK under this build?) and `IsInitialized` (did an
`InitializeAsync` on this client succeed?). For a head that has no SDK and wants the calls to be
harmless rather than fatal, the package also ships `NullSfmcSdkClient` — a no-op implementation that
still validates its arguments, so it hides platforms without hiding your bugs:

```csharp
builder.Services.AddSingleton<ISfmcSdkClient>(
    new SfmcSdkClient() is { IsSupported: true } client ? client : new NullSfmcSdkClient());
```

> **Using MobilePush too?** Then you do not need this client at all:
> [`MarketingCloudSDK.Net`](https://github.com/sbokatuk/MarketingCloudSDK.Net) composes one
> internally and re-exports `Identity` and `TrackCustomEvent`, and its initialization brings this
> core up underneath with the MobilePush module attached. Never call **this** package's
> `InitializeAsync` in an app that initializes MobilePush — that configures the core a second time
> with an empty module set, which upstream forbids.

## Packages and versions

One package. The version is `<SFMCSDK iOS version>.<binding revision>` — `5.0.0.1` is SFMCSDK
**5.0.0**, revision **1**, and the Android side of the same release is sfmcsdk **3.3.0**.

> **Why one version names one SDK.** Salesforce releases the iOS and Android SDKs on separate
> cadences and their version numbers have never matched. A façade over both has to pick one line
> to name itself after or invent a third numbering that maps to nothing. It picks iOS — the same
> convention the sibling DatadogNet façade uses — and states the Android version everywhere it
> states its own.

| SFMCSDK.Net | SFMCSDK (iOS, native) | sfmcsdk (Android, native) | SFMCSDK.Net.iOS | SFMCSDK.Net.Android |
| --- | --- | --- | --- | --- |
| 5.0.0.1 | 5.0.0 | 3.3.0 | 5.0.0.1 | 3.3.0.1 |
| 4.0.1.2 | 4.0.1 | 3.1.1 | 4.0.1.2 | 3.1.1.1 |

The two rows are two generations of the native SDKs, and each pairs with one MobilePush
generation: SFMCSDK 5 / sfmcsdk 3.3 with MobilePush 12, SFMCSDK 4.0 / sfmcsdk 3.1 with
MobilePush 11. The iOS core enforces this itself (`SFMCSdk.State` on 5.0.0 accepts the
MobilePush module only in the 12.x range). An app that also uses the MobilePush bindings
directly stays on 4.0.1.2 until it moves to their 12.x packages.

The platform packages are pinned **exactly** (`[5.0.0.1]` / `[3.3.0.1]`), not floored: the façade
calls each binding's hand-written convenience layer — the `Action` overloads of
`Configure`/`RequestSdk` on Android, the `Func`-typed `Identity.Edit` trampoline on iOS — and
those carry no compatibility promise across binding revisions. A newer binding is consumed by
this repository re-pinning and releasing, with the device suites in between, not by NuGet
floating a consumer onto it.

## Installing

```xml
<PackageReference Include="SFMCSDK.Net" Version="5.0.0.1" />
```

Nine target frameworks: `net8.0`, `net9.0`, `net10.0`, each with its `-android` and `-ios` head —
`net8.0-android34.0`, `net8.0-ios18.0`, `net9.0-android35.0`, `net9.0-ios18.0`,
`net10.0-android36.0`, `net10.0-ios26.0`. Floors: **iOS 12.2** (the SFMC framework is Swift and
relies on the OS Swift runtime, ABI-stable from 12.2), **Android API 26** (the sfmcsdk `.aar`
manifest's own floor).

The platform heads pull `SFMCSDK.Net.Android 3.3.0.1` / `SFMCSDK.Net.iOS 5.0.0.1` transitively;
apps reference only this package unless they want the raw namespaces pinned explicitly (they may
— the same versions arrive either way).

For a MAUI app with an Android head, target net10: MAUI 8 and 9 cannot build against the AndroidX
generation the SFMC Android binding's dependencies resolve to (a Java-callable-wrapper defect the
binding repository's README records), while MAUI 10 handles it. An iOS-only MAUI app is fine on
net8 or net9. The net8 and net9 assets are otherwise for plain .NET Android / .NET iOS apps, which
is also what the binding packages' own net8 support is for.

## Usage notes

- **One client per process.** The native SDK on both platforms is a process-wide singleton
  behind static entry points, so `SfmcSdkClient` is meant to live as a DI singleton. The
  initialization guard is per-instance — the instance is what promises one-shot semantics.
- **`InitializeAsync` is one-shot, even on failure.** A second call throws
  `InvalidOperationException`. The guard deliberately does not reset on failure: the native
  configure is itself not retryable, and after a timeout the first attempt may still be running —
  a retry would race it. A process that needs a fresh attempt restarts.
- **Identity is fire-and-forget**, before or after initialization — both SDKs queue early
  identity work. Nothing waits for a server acknowledgement; changes batch on the SDK's own
  schedule.
- **`DiagnosticState` is for logs, never for parsing.** iOS returns the SDK's state JSON;
  Android's equivalent detail lives on the instance `requestSdk` delivers asynchronously, so the
  synchronous property reports the static initialization state
  (`NONE`/`INITIALIZING`/`READY`/`ERROR`) instead.
- **Push is not here.** MobilePush lives in the MarketingCloudSDK binding repositories, which
  depend on these same core bindings. For a cross-platform MobilePush surface use
  [`MarketingCloudSDK.Net`](https://github.com/sbokatuk/MarketingCloudSDK.Net) — it composes this
  façade rather than sitting beside it; for module-level control, the raw config builders.
- **`IsSupported` and `IsInitialized` never throw.** They are the two members shared code branches
  on, so they answer on every target framework — including the neutral ones, where everything else
  throws. `IsInitialized` is a status signal, not a precondition: identity work is legal before
  initialization because the SDK queues it. It is also not the one-shot guard — after a failed
  initialization it stays false while the guard stays claimed.

## How this repository works

Nothing is bound here and nothing native is committed. The repository compiles one multi-targeted
assembly against the two pinned binding packages: shared sources declare the API and
`private partial …Core` seams, and exactly one of `Platforms/Android`, `Platforms/Apple` or
`Platforms/Neutral` supplies the bodies per target framework (see `src/Sfmc.Facade.props`).

Each .NET SDK's android/ios workloads ship reference packs for only two target frameworks — the
.NET 9 band covers net8/net9, the .NET 10 band covers net9/net10 — so
[build/BuildNugets.sh](build/BuildNugets.sh) packs twice and
[build/merge-packages.py](build/merge-packages.py) grafts the net10 assets *and their dependency
groups* into one package. The groups matter as much as the assemblies: an empty net10 group would
tell NuGet a net10 consumer needs no binding underneath, and the app would fail with the native
SDK missing.

The pins live in [Directory.Build.props](Directory.Build.props) as literal properties
(`SfmcAndroidPackageVersion`, `SfmcIosPackageVersion`) that the scripts and workflows sed-parse.
`NuGet.config` adds `./artifacts` as a package source, so locally packed platform bindings (from
the sibling repositories) and the locally packed façade both resolve without publishing anything.

### Layout

| Path | What |
| --- | --- |
| `src/Sfmc.Facade.props` | TFM bands, per-platform source selection, compiler settings, pack assets |
| `src/SFMCSDK.Net/` | The façade: shared half + `Platforms/{Android,Apple,Neutral}` |
| `build/` | Pack, merge, README-check and upstream-check scripts; `packages.tsv` is the roster |
| `tests/` | `UnitTests` (neutral leg, no workloads), `PackageTests` (nupkg shape), `DeviceTests` (one project, two heads, driving the façade over the packed package) |
| `samples/` | A MAUI app driving initialization, identity and events through the façade, zero `#if` |
| `.github/workflows/` | `pr`, `build`, `release`, `auto-release` |

## Building locally

```sh
cp ../SFMCSDK.Net.Android/artifacts/*.nupkg ../SFMCSDK.Net.iOS/artifacts/*.nupkg artifacts/  # or let nuget.org serve them
./build/BuildNugets.sh                # packs 5.0.0.1 into ./artifacts
dotnet test tests/SFMCSDK.Net.UnitTests -p:SfmcNeutralOnly=true
dotnet test tests/SFMCSDK.Net.PackageTests
```

## Tests

Three suites, cheapest first — each catches what the previous one cannot see:

```sh
dotnet test tests/SFMCSDK.Net.UnitTests -p:SfmcNeutralOnly=true   # validation, guard, neutral contract
dotnet test tests/SFMCSDK.Net.PackageTests                        # nine TFMs, exact pins, licence, symbols
./.github/scripts/run-simulator-tests.sh 5.0.0.1 net9.0-ios18.0   # the façade over the real SDK
./.github/scripts/run-emulator-tests.sh 5.0.0.1 net9.0-android35.0
```

`-p:SfmcNeutralOnly=true` collapses the referenced façade to its neutral target frameworks, so
the unit tests restore and run on any machine with no mobile workloads — it is the pipeline's
fastest signal, and must never be set on a pack (it would produce a package with no platform
legs).

The device checks run without credentials on purpose: the empty module config points at no
tenant, so server calls fail by design. What they prove is the packaging promise — the packed
package's dependency groups pull the right binding in on each platform, and the façade drives it:
`InitializeAsync` completes, identity and tracking cross into native code, the one-shot guard
holds, and `DiagnosticState` answers. The iOS head carries a simulator-only keychain entitlement
(see [Troubleshooting](#troubleshooting)) and checks the keychain before anything else, so a lost
entitlement is reported first as one line naming the cause, ahead of the initialization timeout it
then leads to.

## Upgrading

Re-pin, verify, release — in that order:

1. Bump `SfmcAndroidPackageVersion` / `SfmcIosPackageVersion` (and, when the native line moved,
   `SfmcNativeVersion` + `SfmcAndroidNativeVersion`) in `Directory.Build.props`.
2. Update the version map above — `./build/CheckReadmeVersions.sh` fails the build until the
   README agrees with the props, which is the point.
3. Reset `SfmcBindingRevision` to 1 on a native bump, or increment it for a façade-only change.
4. Open a PR: the pipeline packs, validates the package shape and runs both device suites
   against the new pins before anything ships.

`./build/check-upstream.sh` (shared verbatim with the binding repositories, driven by
[build/upstream.tsv](build/upstream.tsv)) reports when Salesforce publishes a native version
newer than the pinned lines — the early signal that the binding repositories will move and this
façade will need re-pinning behind them.

## Releasing

Merging a file named `docs/release-notes/<four-part-version>.md` to `main` **is** the release:
`auto-release` tags it, `release` verifies the tag is on the default branch (the guard), packs
with verification off — the tagged commit was already verified on its pull request — and
publishes to nuget.org via trusted publishing (OIDC, no stored API key). Every pull request
publishes a `-beta.<pr>.<run>` prerelease.

## Troubleshooting

**`PlatformNotSupportedException` mentioning the neutral build.** The code ran on a plain target
framework (a unit test, a Windows head). That is the documented contract — construct freely,
guard the calls, or inject a fake of `ISfmcSdkClient`; the real implementation runs in
`net*-android` / `net*-ios` heads.

**`InvalidOperationException` from a second `InitializeAsync`.** Initialization is one-shot per
client and per process — see [Usage notes](#usage-notes). Await the first call; do not retry.

**`InitializeAsync` times out on the iOS simulator.** The app has no keychain access. The SDK
keeps its keys in the keychain and, refused it, does not initialize and reports nothing, so the
completion `InitializeAsync` waits for never comes. On a simulator an app without entitlements is
refused (`SecItemAdd` returns `errSecMissingEntitlement`, -34018), and the .NET 8 and .NET 9 iOS
workloads build simulator apps exactly that way; the .NET 10 workload embeds simulated
entitlements by itself. Give simulator builds one entitlement, a `keychain-access-groups` array
holding your bundle identifier, in a file of its own such as
`Platforms/iOS/Entitlements.Simulator.plist`, and point the build at it for simulator runtime
identifiers only:

```xml
<Target Name="UseSimulatorKeychainEntitlement" BeforeTargets="BuildOnlySettings"
        Condition="$(RuntimeIdentifier.StartsWith('iossimulator'))">
  <PropertyGroup>
    <CodesignEntitlements>$(MSBuildProjectDirectory)/Platforms/iOS/Entitlements.Simulator.plist</CodesignEntitlements>
    <CodesignRequireProvisioningProfile>false</CodesignRequireProvisioningProfile>
  </PropertyGroup>
</Target>
```

A target, not a `PropertyGroup` with the same condition: the iOS SDK chooses the default runtime
identifier (a simulator for `dotnet build`, `ios-arm64` for `dotnet publish`) after the project's
properties are evaluated, so a property condition cannot tell a simulator build from a device
publish without `-r`, and would give the device build the simulator's entitlements. Device builds
keep their own: the provisioning profile carries what the keychain needs. If the app already has a
`Platforms/iOS/Entitlements.plist`, the simulator file replaces it on simulator builds, so copy its
entries into the simulator file as well.

The [sample](https://github.com/sbokatuk/SFMCSDK.Net/tree/main/samples/SFMCSDK.Net.Sample) and
the device tests do this. Releases before 5.0.0.1 did not wait for the completion at all — they
reported success after a fixed two seconds — so on such a simulator build they claimed an
initialization that never happened.

**`javac error JAVAC0000: class file has wrong version 61.0, should be 55.0` in a
`net8.0-android34.0` app.** The sfmcsdk `.aar` is Java 17 bytecode and the build compiled against
it with JDK 11, which .NET 8 for Android still accepts (the net9 and net10 workloads refuse
anything older than 17). Build with JDK 17: pass `-p:JavaSdkDirectory=<path to JDK 17>` or set
that property in the project. `JAVA_HOME` alone is not a guarantee: the build skips it when it
names a JDK newer than the workload supports and falls back to another installed one.

**Restore fails with NU1301 naming `artifacts`.** The local package source must exist:
`mkdir -p artifacts` (a fresh clone has it via the committed `.gitkeep`).

**A stale package in `artifacts/` shadows nuget.org, and `NU1107` blames a version you never
pinned.** The local feed is searched alongside nuget.org, so a nupkg packed here *before* a
dependency was re-pinned keeps being resolved under the same version number — and once restored it
is cached in the NuGet global-packages folder, where it poisons every other repository too. Clearing
both is the fix:

```bash
rm -f artifacts/*.nupkg artifacts/*.snupkg && rm -rf ~/.nuget/packages/sfmcsdk.net/<version> && dotnet restore --force
```

The tell is a `NU1107` naming two versions of the same binding, one of which matches no
`Directory.Build.props` pin in any repository.

**NU1608 warnings about AndroidX Lifecycle versions.** A property of the AndroidX graph the SFMC
Android binding pulls in — two of its packages exact-range a sibling that a third floats past.
NuGet resolves the higher version and the result is what the binding's emulator tests pass
against. This repository suppresses the warning rather than pinning AndroidX for every consumer;
pin in your app if you want it gone.

**A surface you need is missing from the façade.** Deliberate — see
[What the façade does and does not hide](#what-the-façade-does-and-does-not-hide). Use the
platform namespaces; they ship in the same restore.

## Licence

The code in this repository is [MIT](LICENSE), and the package declares plain `MIT` — it ships no
native binaries, so Salesforce's licence is not its to declare. The binding packages underneath
ship the native SFMC SDK artifacts (© Salesforce, BSD-3-Clause) and each declares
`MIT AND BSD-3-Clause` with both texts packed; the BSD text is mirrored at
[licenses/BSD-3-Clause-Salesforce.txt](licenses/BSD-3-Clause-Salesforce.txt) for reference.
