# Local Wallet

A simple personal-finance app for Android, built with .NET 10 MAUI.

## Features

- Income / expense tracking by category
- Multiple accounts in different currencies
- Automatic FX conversion via NBP (Polish National Bank) with frankfurter.app as fallback
- Biometric unlock (fingerprint / face)
- Charts and statistics
- CSV export
- Bulk-delete transactions for a date range
- Local-only storage (SQLite on device — no cloud)
- Default base currency: **PLN**

## Family sync (LAN-only, peer-to-peer)

- Shared budgets across family devices on the same Wi-Fi
- mDNS / UDP beacons discover peers, TCP transports the events
- Every event is sealed with ChaCha20-Poly1305 (per-family AEAD key) and signed with Ed25519 (per-device keypair stored in the device keychain)
- SAS-based pairing handshake for joining a family
- Last-write-wins conflict resolution via Lamport clocks + ordinal device-ID tiebreak
- No servers — direct device-to-device only
- Family chat tied to the same encrypted event log

## Getting the APK

1. Push to `master` triggers the GitHub Actions workflow.
2. Commits whose subject matches `Release vX.Y.Z` are auto-published as a GitHub Release with the signed APK attached.
3. For other commits, grab the APK from the workflow run's **Artifacts** section (`LocalWallet-apk.zip`).

## Local development

Requirements: .NET 10 SDK + MAUI workload + Android SDK.

```bash
dotnet workload install maui-android
dotnet restore src/LocalWallet/LocalWallet.csproj
dotnet build   src/LocalWallet/LocalWallet.csproj -f net10.0-android -c Debug
dotnet publish src/LocalWallet/LocalWallet.csproj -f net10.0-android -c Release \
               -p:AndroidPackageFormat=apk -o ./artifacts
```

On Windows, `run.bat` boots the `LocalWallet_Pixel6` AVD (if not already running), waits for it to finish booting, then builds and deploys the app via `dotnet build -t:Run`.

## Tests

A standalone xUnit project lives at `tests/LocalWallet.Tests/`. It targets `net10.0` (not Android) and links individual source files instead of taking a `ProjectReference` on the main project — the main project is Android-only and can't be loaded by `dotnet test`.

```bash
dotnet test tests/LocalWallet.Tests/LocalWallet.Tests.csproj
```

Do NOT run `dotnet test` at the solution root — it would try to build the Android target and fail on machines without the `maui-android` workload.

## Project layout

```
src/LocalWallet/
├── Models/                   # SQLite entities (Account, Transaction, Family, SyncEvent, ...)
├── Services/
│   ├── Crypto/               # Ed25519 device identity, ChaCha20-Poly1305 family AEAD, X25519 pairing
│   ├── Database/             # SQLite via sqlite-net-pcl
│   ├── ExchangeRates/        # NBP + Frankfurter providers
│   ├── Family/               # Family service, invitation codec, pairing protocol
│   └── Sync/                 # Event store, projector, LAN discovery, TCP sync, auto-sync coordinator
├── ViewModels/               # MVVM via CommunityToolkit.Mvvm source generators
├── Views/                    # XAML pages
├── Controls/                 # Custom Liquid-Glass tab bar
├── Converters/               # XAML value converters
├── Resources/                # Styles, fonts, icons
└── Platforms/Android/        # MainActivity, AndroidManifest, native blur effect
tests/LocalWallet.Tests/      # xUnit suite for pure-logic helpers
```
