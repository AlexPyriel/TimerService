using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using LocalDataStorage;
using GDF.TimerService.Data;
using UnityEngine;

namespace GDF.TimerService.Internal
{
    /// <summary>
    /// Default <see cref="ITimerService"/> implementation. Keeps an in-memory dictionary of named
    /// timers, drives per-second progression through a UniTask loop, autosaves on a configurable
    /// cadence and applies the offline delta on <see cref="Initialize"/>.
    /// </summary>
    internal sealed class TimerService : ITimerService, IDisposable
    {
        private readonly ILocalDataStorage _storage;
        private readonly TimerServiceConfig _config;
        private readonly Dictionary<string, TimerEntry> _timers = new();

        private CancellationTokenSource _loopCts;
        private int _secondsSinceSave;
        private bool _initialized;

        public event Action TimersChanged;
        public event Action<string> TimerChanged;
        public event Action<string> TimerCompleted;

        public TimerService(ILocalDataStorage storage, TimerServiceConfig config = null)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _config = config ?? TimerServiceConfig.Default();
        }

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
                Progress(offlineSeconds);

            PersistLastEnter();
            PersistTimers();

            _initialized = true;
            _loopCts = new CancellationTokenSource();
            TickLoopAsync(_loopCts.Token).Forget();
        }

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
            TimerChanged?.Invoke(name);
            TimersChanged?.Invoke();
        }

        public bool Remove(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            if (!_timers.Remove(name))
                return false;

            PersistTimers();
            TimersChanged?.Invoke();
            return true;
        }

        public bool TryGet(string name, out TimerEntry entry)
        {
            return _timers.TryGetValue(name, out entry);
        }

        public bool IsCompleted(string name)
        {
            return _timers.TryGetValue(name, out TimerEntry entry) && entry.IsCompleted;
        }

        public void SaveNow()
        {
            PersistTimers();
            PersistLastEnter();
            _secondsSinceSave = 0;
        }

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

                Progress(1);
                _secondsSinceSave++;

                if (_secondsSinceSave >= _config.AutosaveSeconds)
                    SaveNow();
            }
        }

        private void Progress(int seconds)
        {
            if (seconds <= 0 || _timers.Count == 0)
                return;

            // Snapshot keys so we can mutate the dict in-flight.
            List<string> names = new List<string>(_timers.Keys);
            bool any = false;

            foreach (string name in names)
            {
                TimerEntry entry = _timers[name];
                if (entry.IsCompleted)
                    continue;

                bool wasCompleted = entry.IsCompleted;
                entry.ElapsedSeconds = Math.Min(entry.TotalSeconds, entry.ElapsedSeconds + seconds);
                _timers[name] = entry;

                TimerChanged?.Invoke(name);
                if (!wasCompleted && entry.IsCompleted)
                    TimerCompleted?.Invoke(name);

                any = true;
            }

            if (any)
                TimersChanged?.Invoke();
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
