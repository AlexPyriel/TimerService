# Changelog

## [2.0.0] - 2026-11-06

Breaking release. Timer lifecycle facts are published as MessagePipe messages instead of C# events.

### Changed

- `TimerService` is `public sealed`; its constructor takes the four message publishers after the storage.
- Depends on `com.gdf.local-data-storage >= 3.0.0` and `com.cysharp.messagepipe >= 1.8.2`.

### Added

- `TimerStartedMessage` (`Name`, `TotalSeconds`), published by `CreateOrReset`.
- `TimerTickedMessage` (`Name`, `RemainingSeconds`), published once per second per running timer and once per timer
  for the offline delta.
- `TimerCompletedMessage` (`Name`, `CompletedOffline`), published once when a timer finishes.
- `TimerRemovedMessage` (`Name`), published when `Remove` drops an existing timer.

### Removed

- `ITimerService.TimersChanged`, `TimerChanged` and `TimerCompleted` events.
- Zenject installer (`TimerServiceZenjectInstaller`) and Reflex installer (`TimerServiceReflexInstaller`). The README
  section "Using with Zenject / Reflex" shows the replacement bindings.

### Migration

1. Register MessagePipe once in the root scope: `builder.RegisterMessagePipe();`.
2. Replace `TimerCompleted += name => ...` with a subscription to `TimerCompletedMessage`.
3. Replace `TimerChanged` with `TimerTickedMessage` (progress) or `TimerStartedMessage` (restart); replace
   `TimersChanged` with the specific message the consumer needs.

## [1.0.1] - 2026-11-02

### Changed

- **Breaking:** root namespace renamed from `TimerService` to `GDF.TimerService`. Consumers must
  update `using TimerService;` → `using GDF.TimerService;` (and installer sub-namespaces similarly).
  Alignment with the GDF.* namespace discipline across the library family.

### Fixed

- VContainer installer compilation: added missing `using VContainer.Unity;` so `IInstaller`
  (which lives in `VContainer.Unity`, not `VContainer`) resolves. Without this fix the
  installer assembly failed with `CS0246`.

## [1.0.0] - 2026-11-02

Initial public release.

- `ITimerService` facade: `Initialize`, `CreateOrReset`, `Remove`, `TryGet`, `IsCompleted`, `SaveNow`.
- Events: `TimersChanged`, `TimerChanged(string)`, `TimerCompleted(string)`.
- Offline progression: on `Initialize` the service applies the `DateTime.UtcNow - LastExit` delta to every active timer and clamps at `TotalSeconds`.
- Per-second tick loop via UniTask (ignores time scale), autosave on a configurable cadence.
- Persisted through `com.gdf.local-data-storage`: `TimersData` (list of entries) + `LastEnterData` (UTC ticks of previous exit).
- DI installers for VContainer, Zenject and Reflex.
- EditMode tests cover create/reset/remove, offline-delta clamp, and save round-trip.
