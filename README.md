# GDF Timer Service

Named countdown timers with offline progression and auto-save for Unity. Dailies, cooldowns,
stamina systems and reward timers survive game restarts and advance by the real-time offline
delta on initialization.

Package name: `com.gdf.timer-service`
Assembly name: `GDF.TimerService`

## Features

- `ITimerService` facade: `CreateOrReset(name, seconds)`, `Remove(name)`, `TryGet(name, out TimerEntry)`, `IsCompleted(name)`, `SaveNow()`.
- MessagePipe messages: `TimerStartedMessage`, `TimerTickedMessage`, `TimerCompletedMessage` (with `CompletedOffline`), `TimerRemovedMessage`.
- Offline progression: on `Initialize` the service applies the `DateTime.UtcNow - LastExit` delta to every active timer. A timer that would complete offline arrives capped at `TotalSeconds` on the next frame.
- Per-second tick loop via UniTask that ignores time scale (`Time.timeScale = 0` does not stop timers).
- Autosave cadence configurable (default 60 seconds). The service also saves on `CreateOrReset`, `Remove` and `Dispose`.
- Persisted through `com.gdf.local-data-storage` — crash-safe through its atomic file writes when the file backend is enabled.
- VContainer installer; public `sealed` `TimerService` for any other DI container.

## Requirements

- Unity `6000.0+`
- `com.gdf.local-data-storage` `3.0.0+`
- `com.cysharp.unitask`
- `com.cysharp.messagepipe` `1.8.2+` (required). The host registers MessagePipe once at its composition root;
  the package installer does not.
- `jp.hadashikick.vcontainer` and `com.cysharp.messagepipe.vcontainer` when using the bundled installer.

## Installation (UPM via Git)

Add this dependency to `Packages/manifest.json`:

```json
"com.gdf.timer-service": "https://github.com/AlexPyriel/TimerService.git#v2.0.0"
```

For development tracking (not recommended for production), use `#main` instead of a tag.

You can also install it from Unity Package Manager:

- `Window -> Package Manager -> + -> Add package from git URL...`
- `https://github.com/AlexPyriel/TimerService.git#v2.0.0`

## Usage

### 1. Register the service

```csharp
using GDF.TimerService.Installers.VContainer;
using LocalDataStorage.Installers.VContainer;
using MessagePipe;
using VContainer;

protected override void Configure(IContainerBuilder builder)
{
    builder.RegisterMessagePipe();      // once per application, in the root scope

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
using GDF.TimerService;
using VContainer.Unity;

public sealed class BootstrapEntryPoint : IStartable
{
    [Inject] private readonly ITimerService _timers;

    public void Start() => _timers.Initialize();
}
```

### 3. Drive timers from gameplay

```csharp
using GDF.TimerService;

_timers.CreateOrReset("daily_reward", 24 * 60 * 60);

if (_timers.IsCompleted("daily_reward"))
    GrantDailyReward();
```

### 4. Query current progress for UI

```csharp
using System;
using GDF.TimerService;

if (_timers.TryGet("daily_reward", out TimerEntry entry))
    _label.text = TimeSpan.FromSeconds(entry.RemainingSeconds).ToString(@"hh\:mm\:ss");
```

### 5. React to timer facts

State is read with queries (`TryGet`, `IsCompleted`); facts arrive as messages. Subscribe through
`ISubscriber<T>` and dispose the subscription with its owner:

```csharp
using GDF.TimerService.Messages;
using MessagePipe;

_completed.Subscribe(message =>
{
    if (message.Name == "stamina_regen")
        AwardStamina(showToast: !message.CompletedOffline);
});

_ticked.Subscribe(message =>
{
    if (message.Name == "daily_reward")
        _label.text = TimeSpan.FromSeconds(message.RemainingSeconds).ToString(@"hh\:mm\:ss");
});
```

| Message | Published |
|---------|-----------|
| `TimerStartedMessage` | on every `CreateOrReset` |
| `TimerTickedMessage` | once per second per running timer, and once per timer for the offline delta in `Initialize` |
| `TimerCompletedMessage` | once, after the tick that finishes the timer; `CompletedOffline` is `true` when `Initialize` finished it |
| `TimerRemovedMessage` | when `Remove` drops an existing timer |

Subscribe before calling `Initialize` to receive offline completions.

## Assembly Definition Notes

`GDF.TimerService` is a dedicated runtime assembly that depends on `GDF.LocalDataStorage`, `UniTask`
and `MessagePipe`. The VContainer installer lives in `GDF.TimerService.Installers.VContainer` and compiles
only when `jp.hadashikick.vcontainer` is installed. `GDF.TimerService.Internal.TimerService` is
`public sealed` so any container can construct it.

## Using with Zenject / Reflex

The package ships only a VContainer installer. Other containers bind the public service type and provide
the four message brokers.

Zenject (`MessagePipe.Zenject`, in the `ProjectContext` installer):

```csharp
using GDF.TimerService;
using GDF.TimerService.Messages;
using MessagePipe;

MessagePipeOptions options = Container.BindMessagePipe();
Container.BindMessageBroker<TimerStartedMessage>(options);
Container.BindMessageBroker<TimerTickedMessage>(options);
Container.BindMessageBroker<TimerCompletedMessage>(options);
Container.BindMessageBroker<TimerRemovedMessage>(options);

Container.BindInstance(TimerServiceConfig.Default());
Container.BindInterfacesTo<GDF.TimerService.Internal.TimerService>().AsSingle();
```

Reflex (MessagePipe's built-in container provides the brokers):

```csharp
using System;
using GDF.TimerService;
using GDF.TimerService.Messages;
using MessagePipe;

BuiltinContainerBuilder pipe = new BuiltinContainerBuilder();
pipe.AddMessagePipe();
pipe.AddMessageBroker<TimerStartedMessage>();
pipe.AddMessageBroker<TimerTickedMessage>();
pipe.AddMessageBroker<TimerCompletedMessage>();
pipe.AddMessageBroker<TimerRemovedMessage>();
IServiceProvider provider = pipe.BuildServiceProvider();

// Repeat for each message type: the publisher for the service, the subscriber for the project.
builder.AddSingleton(provider.GetRequiredService<IPublisher<TimerCompletedMessage>>(), typeof(IPublisher<TimerCompletedMessage>));
builder.AddSingleton(provider.GetRequiredService<ISubscriber<TimerCompletedMessage>>(), typeof(ISubscriber<TimerCompletedMessage>));

builder.AddSingleton(TimerServiceConfig.Default(), typeof(TimerServiceConfig));
builder.AddSingleton(typeof(GDF.TimerService.Internal.TimerService), typeof(ITimerService), typeof(IDisposable));
```

Both snippets assume `ILocalDataStorage` is bound as shown in the `com.gdf.local-data-storage` README.

## License

MIT. See `LICENSE.md`.
