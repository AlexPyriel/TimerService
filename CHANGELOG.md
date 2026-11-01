# Changelog

## [1.0.0] - 2026-11-02

Initial public release.

- `ITimerService` facade: `Initialize`, `CreateOrReset`, `Remove`, `TryGet`, `IsCompleted`, `SaveNow`.
- Events: `TimersChanged`, `TimerChanged(string)`, `TimerCompleted(string)`.
- Offline progression: on `Initialize` the service applies the `DateTime.UtcNow - LastExit` delta to every active timer and clamps at `TotalSeconds`.
- Per-second tick loop via UniTask (ignores time scale), autosave on a configurable cadence.
- Persisted through `com.gdf.local-data-storage`: `TimersData` (list of entries) + `LastEnterData` (UTC ticks of previous exit).
- DI installers for VContainer, Zenject and Reflex.
- EditMode tests cover create/reset/remove, offline-delta clamp, and save round-trip.
