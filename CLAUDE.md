# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build / run

Single-project .NET 10 MAUI app, **Android-only target** (`net10.0-android`). The single project lives at `src/LocalWallet/LocalWallet.csproj`; the `LocalWallet.sln` at the repo root just references it.

```bash
dotnet workload install maui-android       # one-time
dotnet restore src/LocalWallet/LocalWallet.csproj
dotnet build   src/LocalWallet/LocalWallet.csproj -f net10.0-android -c Debug
dotnet publish src/LocalWallet/LocalWallet.csproj -f net10.0-android -c Release -p:AndroidPackageFormat=apk -o ./artifacts
```

CI (`.github/workflows/android-build.yml`) only runs `dotnet publish` and uploads the APK; there is no linter or formatting config.

### Tests
A standalone xUnit project lives at `tests/LocalWallet.Tests/`. It targets `net10.0` (not Android) and **does not** `ProjectReference` the main app — instead it links specific source files (currently `FamilyCryptoService`, `IFamilyCryptoService`, `InvitationCodec`) into its own compilation. This is deliberate: the main project is Android-only and cannot be `dotnet test`-loaded. When adding tests for a new file, only link it if it has no MAUI / Android / SQLite dependencies; otherwise extract the pure logic first.

```bash
dotnet test tests/LocalWallet.Tests/LocalWallet.Tests.csproj            # full suite
dotnet test tests/LocalWallet.Tests/LocalWallet.Tests.csproj --filter FullyQualifiedName~FamilyCryptoServiceTests   # one class
dotnet test tests/LocalWallet.Tests/LocalWallet.Tests.csproj --filter "FullyQualifiedName=LocalWallet.Tests.InvitationCodecTests.Decode_returns_null_for_garbage"  # single test
```

Do NOT run `dotnet test` at the solution root — that triggers a build of the Android target and fails on machines without the `maui-android` workload. Always point at the test csproj explicitly.

### Releases
Pushing a commit to `master` whose subject matches `Release vX.Y.Z` makes the workflow auto-publish a GitHub Release with the signed APK. Bumping `<ApplicationDisplayVersion>`/`<ApplicationVersion>` in the csproj must accompany the commit message — they are not derived from each other. Failed CI builds push their full log to a separate `ci-log` branch (`raw.githubusercontent.com/.../ci-log/latest.txt`).

## Architecture

### Composition root
`MauiProgram.CreateMauiApp` wires every service, ViewModel, and Page into the MAUI DI container. **Adding a new page or VM requires registering it there** — DI resolution is constructor-based and pages are pulled from the container in `App.InitializeAsync` and via Shell routes. The single split between scopes:
- Services + `IDatabaseService` etc. → `Singleton`
- ViewModels and Pages → `Transient`

### Startup flow (`App.xaml.cs`)
`CreateWindow` returns a placeholder splash immediately, then `InitializeAsync` runs off the UI thread:
1. Read `AppSettings` (id=1) from SQLite — DB tables are created lazily by `DatabaseService.InitializeAsync` on first connection (no migrations framework; schemas grow by adding columns to model classes).
2. Initialize `DeviceIdentityService` (Ed25519 keypair stored in `SecureStorage`, public key persisted to DB).
3. Hop to main thread and pick the root page: no settings → `AppShell`; not onboarded → `OnboardingPage`; biometric on → `LockPage`; else `AppShell`.
4. Best-effort background tasks: refresh exchange rates if stale (>12h), then `SyncService.StartListenerAsync` → `LanDiscoveryService.StartAsync` → `AutoSyncCoordinator.Start`. Order matters: discovery beacons advertise the listener's port.

`InstallGlobalExceptionHandlers` surfaces every unhandled exception (AppDomain, TaskScheduler, Android) via `UiAlerts` instead of crashing — explicitly chosen over silent `e.Handled = true` because v2.3.0 left the UI in dead states without feedback (see commit history).

### UI shell
`AppShell.xaml` declares 5 routes (`dashboard / transactions / multicurrency / chat / settings`) but **hides Shell's native bottom bar** (`Shell.TabBarIsVisible="False"`). The custom floating "Liquid-Glass" tab bar is rendered by `Controls/LiquidGlassTabBar.cs` and `Controls/AppTabBar.cs`, with the Android blur effect configured by `Platforms/Android/LiquidGlassBlur.cs` (`Configure()` is called from `MauiProgram`). When adding a new top-level tab, both `AppShell.xaml` and the tab bar control need updating.

### Two write paths — this is the core invariant
Every entity (Transaction / Account / Category / ChatMessage) has two ways into the database:

1. **Personal data** (`FamilyId` is null): VMs call `IDatabaseService` directly. Plain CRUD.
2. **Family-scoped data** (`FamilyId` is set): VMs MUST call `IEntityWriter`, which:
   - Loads the family AEAD key via `IFamilyService.GetFamilyKeyAsync`
   - Serializes the entity to JSON
   - Calls `IEventStore.AppendLocalAsync` → produces a signed `SyncEvent` (ChaCha20-Poly1305 sealed payload, Ed25519 signature, monotonic Lamport clock from `AppSettings.CurrentLamportClock`)
   - Writes the event, then re-applies it locally via `IProjector` (so the event log is the single source of truth even on the local device)
   - Notifies `SyncService` so `AutoSyncCoordinator` can push to LAN peers

If a VM writes a family entity directly to `IDatabaseService`, the change will exist locally but never sync, and remote events overwriting that entity will silently win on next sync. **Never bypass `IEntityWriter` for family-scoped data.**

`ChatMessage` is unconditionally family-scoped (`FamilyId` is non-nullable on the model).

### Sync protocol
- Discovery: `LanDiscoveryService` uses mDNS / UDP beacons advertising `{deviceId, familyId, port}`. `AutoSyncCoordinator` debounces sync attempts per `(FamilyId, DeviceId)` with a 3 s cooldown.
- Transport: TCP listener on preferred port `47321` (falls back to ephemeral port if taken). `SyncMessages.cs` defines the message types: `Hello → HelloAck → VectorClock → EventBatch → Done`.
- Pairing: SAS-based handshake (`PairingService`, `PairingProtocol`). The salt-order bug fixed in `cdfea54` is a known footgun — both sides MUST canonicalize salt order identically before deriving the SAS code.
- Conflict resolution (`Projector.IsOlderOrEqual`): higher `LamportClock` wins; tie broken by ordinal compare of `AuthorDeviceId`. Same rule used by every projection method, so changing it is a one-line change with global blast radius.
- Soft delete: every entity carries `IsDeleted` and is filtered out by all `Get*Async` queries. Hard deletes never happen except in `ResetAllDataAsync`. **`DeviceIdentity` is intentionally preserved on reset** — its keypair lives in `SecureStorage` and regenerating it would orphan family memberships.
- Member revocation: `EntityType.Member` events with `EventOperation.Delete` carry a `MemberRevokePayload`; only events authored by a member with `Role == "Owner"` are accepted (`EventStore.AcceptRemoteAsync`).

### Crypto
- Device identity: Ed25519 keypair, private key in `SecureStorage`, public key in DB (`DeviceIdentity` table, id=1). Used to sign every outgoing `SyncEvent`.
- Family key: 32-byte random, used for ChaCha20-Poly1305 AEAD per event. Associated data is `"{familyId:N}|{entityType}|{entityId:N}|{operation}|{lamportClock}"` — must match exactly on encode and decode sides.
- BouncyCastle is used for Ed25519 specifically; the rest uses `System.Security.Cryptography`.

### Database
SQLite via `sqlite-net-pcl`, single file `localwallet.db3` in `FileSystem.AppDataDirectory`. Tables are created via `CreateTableAsync<T>()` in `DatabaseService.InitializeAsync`. **There is no migration system** — adding a column to a model class will cause `sqlite-net` to ALTER TABLE on next startup, but renames or type changes silently break existing installs. Default categories are seeded on first run only (`SeedDefaultCategoriesAsync`).

### Exchange rates
`ExchangeRateService` calls `NbpRatesProvider` first (Polish National Bank), falls back to `FrankfurterRatesProvider`. Default base currency is `PLN`. Rates refresh on app start if older than 12h.

## Conventions worth knowing
- All UI text is in Russian.
- Localizable strings are inlined in XAML and code-behind; there is no resource bundle.
- ViewModels use `CommunityToolkit.Mvvm` source generators (`[ObservableProperty]`, `[RelayCommand]`).
- The `Microsoft.Maui.Controls.Shapes` import bug in `7be5ca6`/`f0b56e5` is a recurring class — XAML files using `RoundRectangle` need the namespace explicitly.
