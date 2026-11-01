# GDF Timer Service

Named countdown timers with offline progression and auto-save for Unity. Dailies, cooldowns,
stamina systems and reward timers survive game restarts and advance by the real-time offline
delta on initialization.

Package name: `com.gdf.timer-service`
Assembly name: `GDF.TimerService`

## Features

- `ITimerService` facade: `CreateOrReset(name, seconds)`, `Remove(name)`, `TryGet(name, out TimerEntry)`, `IsCompleted(name)`, `SaveNow()`.
- Events: `TimersChanged`, `TimerChanged(name)`, `TimerCompleted(name)`.
- Offline progression: on `Initialize` the service applies the `DateTime.UtcNow - LastExit` delta to every active timer. A timer that would complete offline arrives capped at `TotalSeconds` on the next frame.
- Per-second tick loop via UniTask that ignores time scale (`Time.timeScale = 0` does not stop timers).
- Autosave cadence configurable (default 60 seconds). The service also saves on `CreateOrReset`, `Remove` and `Dispose`.
- Persisted through `com.gdf.local-data-storage` — crash-safe through its atomic file writes when the file backend is enabled.
- DI installers for VContainer, Zenject and Reflex.

## Requirements

- Unity `6000.0+`
- `com.gdf.local-data-storage` `2.0.0+`
- `com.cysharp.unitask`
- One of the supported DI containers (optional, required only when using the matching installer):
  - `jp.hadashikick.vcontainer`
  - `com.mathijsbakker.extenject`
  - `com.gustavopsantos.reflex`

## Installation (UPM via Git)

Add this dependency to `Packages/manifest.json`:

```json
"com.gdf.timer-service": "https://github.com/AlexPyriel/TimerService.git#v1.0.0"
```

For development tracking (not recommended for production), use `#main` instead of a tag.

You can also install it from Unity Package Manager:

- `Window -> Package Manager -> + -> Add package from git URL...`
- `https://github.com/AlexPyriel/TimerService.git#v1.0.0`

## Usage

### 1. Register the service

```csharp
protected override void Configure(IContainerBuilder builder)
{
    new LocalDataStorageVContainerInstaller()
        .WithFileBackend()
        .Install(builder);

    new TimerServiceVContainerInstaller()
        .WithAutosaveSeconds(30)
        .Install(builder);
}
```

### 2. Initialize at startup

```csharp
public sealed class BootstrapEntryPoint : IStartable
{
    [Inject] private readonly ITimerService _timers;

    public void Start() => _timers.Initialize();
}
```

### 3. Drive timers from gameplay

```csharp
_timers.CreateOrReset("daily_reward", 24 * 60 * 60);

if (_timers.IsCompleted("daily_reward"))
    GrantDailyReward();

_timers.TimerCompleted += name =>
{
    if (name == "stamina_regen")
        AwardStamina();
};
```

### 4. Query current progress for UI

```csharp
if (_timers.TryGet("daily_reward", out TimerEntry entry))
    _label.text = TimeSpan.FromSeconds(entry.RemainingSeconds).ToString(@"hh\:mm\:ss");
```

## Assembly Definition Notes

`GDF.TimerService` is a dedicated runtime assembly that depends on `GDF.LocalDataStorage` and
`UniTask`. DI installers live in their own assemblies (`GDF.TimerService.Installers.VContainer`,
`.Zenject`, `.Reflex`) so games that only use one container do not pull in the others. The internal
`TimerService` is accessed by installers through `InternalsVisibleTo`.

## License

MIT. See `LICENSE.md`.
