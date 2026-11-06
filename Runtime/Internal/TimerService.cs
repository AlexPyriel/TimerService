using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using LocalDataStorage;
using GDF.TimerService.Data;
using GDF.TimerService.Messages;
using MessagePipe;
using UnityEngine;

namespace GDF.TimerService.Internal
{
    /// <summary>
    /// Default <see cref="ITimerService"/> implementation. Keeps an in-memory dictionary of named
    /// timers, drives per-second progression through a UniTask loop, autosaves on a configurable
    /// cadence, applies the offline delta on <see cref="Initialize"/> and publishes lifecycle messages
    /// through MessagePipe.
    /// </summary>
    public sealed class TimerService : ITimerService, IDisposable
    {
        private readonly ILocalDataStorage _storage;
        private readonly TimerServiceConfig _config;
        private readonly IPublisher<TimerStartedMessage> _startedPublisher;
        private readonly IPublisher<TimerTickedMessage> _tickedPublisher;
        private readonly IPublisher<TimerCompletedMessage> _completedPublisher;
        private readonly IPublisher<TimerRemovedMessage> _removedPublisher;
        private readonly Dictionary<string, TimerEntry> _timers = new();

        private CancellationTokenSource _loopCts;
        private int _secondsSinceSave;
        private bool _initialized;

        /// <summary>Creates the timer service.</summary>
        /// <param name="storage">Storage that persists <see cref="TimersData"/> and <see cref="LastEnterData"/>.</param>
        /// <param name="startedPublisher">Channel for <see cref="TimerStartedMessage"/>.</param>
        /// <param name="tickedPublisher">Channel for <see cref="TimerTickedMessage"/>.</param>
        /// <param name="completedPublisher">Channel for <see cref="TimerCompletedMessage"/>.</param>
        /// <param name="removedPublisher">Channel for <see cref="TimerRemovedMessage"/>.</param>
        /// <param name="config">Autosave cadence; <see langword="null"/> uses <see cref="TimerServiceConfig.Default"/>.</param>
        public TimerService(
            ILocalDataStorage storage,
            IPublisher<TimerStartedMessage> startedPublisher,
            IPublisher<TimerTickedMessage> tickedPublisher,
            IPublisher<TimerCompletedMessage> completedPublisher,
            IPublisher<TimerRemovedMessage> removedPublisher,
            TimerServiceConfig config = null)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _startedPublisher = startedPublisher ?? throw new ArgumentNullException(nameof(startedPublisher));
            _tickedPublisher = tickedPublisher ?? throw new ArgumentNullException(nameof(tickedPublisher));
            _completedPublisher = completedPublisher ?? throw new ArgumentNullException(nameof(completedPublisher));
            _removedPublisher = removedPublisher ?? throw new ArgumentNullException(nameof(removedPublisher));
            _config = config ?? TimerServiceConfig.Default();
        }

        /// <inheritdoc />
        public void Initialize()
        {
            if (_initialized)
                return;

            if (_storage.TryLoad(out TimersData timers) && timers.Timers != null)
            {
                foreach (TimerEntry entry in timers.Timers)
                    _timers[entry.Name] = entry;
            }

            long lastExit = _storage.TryLoad(out LastEnterData last) ? last.LastExitTicksUtc : DateTime.UtcNow.Ticks;
            TimeSpan delta = DateTime.UtcNow - new DateTime(lastExit, DateTimeKind.Utc);
            int offlineSeconds = (int)Math.Max(0, delta.TotalSeconds);
            if (offlineSeconds > 0)
                Progress(offlineSeconds, offline: true);

            PersistLastEnter();
            PersistTimers();

            _initialized = true;
            _loopCts = new CancellationTokenSource();
            TickLoopAsync(_loopCts.Token).Forget();
        }

        /// <inheritdoc />
        public void CreateOrReset(string name, int totalSeconds)
        {
            if (string.IsNullOrEmpty(name))
            {
                Debug.LogWarning("[TimerService] CreateOrReset called with an empty name; dropped.");
                return;
            }

            TimerEntry entry = new TimerEntry
            {
                Name = name,
                TotalSeconds = Math.Max(0, totalSeconds),
                ElapsedSeconds = 0,
            };

            _timers[name] = entry;
            PersistTimers();
            _startedPublisher.Publish(new TimerStartedMessage(name, entry.TotalSeconds));
        }

        /// <inheritdoc />
        public bool Remove(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            if (!_timers.Remove(name))
                return false;

            PersistTimers();
            _removedPublisher.Publish(new TimerRemovedMessage(name));
            return true;
        }

        /// <inheritdoc />
        public bool TryGet(string name, out TimerEntry entry)
        {
            return _timers.TryGetValue(name, out entry);
        }

        /// <inheritdoc />
        public bool IsCompleted(string name)
        {
            return _timers.TryGetValue(name, out TimerEntry entry) && entry.IsCompleted;
        }

        /// <inheritdoc />
        public void SaveNow()
        {
            PersistTimers();
            PersistLastEnter();
            _secondsSinceSave = 0;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            CancellationTokenSource cts = _loopCts;
            if (cts != null)
            {
                _loopCts = null;
                cts.Cancel();
                cts.Dispose();
            }

            SaveNow();
        }

        private async UniTaskVoid TickLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(1), ignoreTimeScale: true, cancellationToken: token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                if (_timers.Count == 0)
                {
                    _secondsSinceSave++;
                    continue;
                }

                Progress(1, offline: false);
                _secondsSinceSave++;

                if (_secondsSinceSave >= _config.AutosaveSeconds)
                    SaveNow();
            }
        }

        private void Progress(int seconds, bool offline)
        {
            if (seconds <= 0 || _timers.Count == 0)
                return;

            // Snapshot keys: subscribers may create or remove timers while messages are published.
            List<string> names = new List<string>(_timers.Keys);

            foreach (string name in names)
            {
                if (!_timers.TryGetValue(name, out TimerEntry entry) || entry.IsCompleted)
                    continue;

                entry.ElapsedSeconds = Math.Min(entry.TotalSeconds, entry.ElapsedSeconds + seconds);
                _timers[name] = entry;

                _tickedPublisher.Publish(new TimerTickedMessage(name, entry.RemainingSeconds));
                if (entry.IsCompleted)
                    _completedPublisher.Publish(new TimerCompletedMessage(name, offline));
            }
        }

        private void PersistTimers()
        {
            TimersData data = new TimersData { Timers = new List<TimerEntry>(_timers.Values) };
            _storage.Save(data);
        }

        private void PersistLastEnter()
        {
            _storage.Save(new LastEnterData { LastExitTicksUtc = DateTime.UtcNow.Ticks });
        }
    }
}
