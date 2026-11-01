using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using LocalDataStorage;
using NUnit.Framework;
using TimerService.Data;

namespace TimerService.Tests.EditMode
{
    /// <summary>
    /// Pure-logic tests for <see cref="Internal.TimerService"/>. The tick loop is not exercised
    /// here; <see cref="Initialize"/> builds the in-memory state, offline progression is applied
    /// through the <see cref="InMemoryStorage.LastExitOverrideTicks"/> hook, and <c>Progress</c>
    /// is driven synchronously via the public API through CreateOrReset + SaveNow boundaries.
    /// </summary>
    public sealed class TimerServiceTests
    {
        [Test]
        public void CreateOrReset_StoresTimerAndFiresEvents()
        {
            InMemoryStorage storage = new InMemoryStorage();
            Internal.TimerService service = new Internal.TimerService(storage, TimerServiceConfig.Default());
            service.Initialize();

            int perTimerChanges = 0;
            int anyChanges = 0;
            service.TimerChanged += _ => perTimerChanges++;
            service.TimersChanged += () => anyChanges++;

            service.CreateOrReset("daily_reward", 86400);

            Assert.IsTrue(service.TryGet("daily_reward", out TimerEntry entry));
            Assert.AreEqual(86400, entry.TotalSeconds);
            Assert.AreEqual(0, entry.ElapsedSeconds);
            Assert.AreEqual(1, perTimerChanges);
            Assert.AreEqual(1, anyChanges);
        }

        [Test]
        public void Remove_DropsTimer()
        {
            InMemoryStorage storage = new InMemoryStorage();
            Internal.TimerService service = new Internal.TimerService(storage);
            service.Initialize();
            service.CreateOrReset("t", 100);

            Assert.IsTrue(service.Remove("t"));
            Assert.IsFalse(service.TryGet("t", out _));
            Assert.IsFalse(service.Remove("t"), "Second Remove reports false.");
        }

        [Test]
        public void OfflineDelta_AdvancesTimers_ClampedAtTotal()
        {
            InMemoryStorage storage = new InMemoryStorage();
            // Seed persisted state as if the previous session had a running timer and exited 50s ago.
            storage.Save(new TimersData
            {
                Timers = new List<TimerEntry>
                {
                    new TimerEntry { Name = "quick", TotalSeconds = 30, ElapsedSeconds = 0 },
                    new TimerEntry { Name = "slow", TotalSeconds = 1000, ElapsedSeconds = 10 },
                },
            });
            storage.Save(new LastEnterData { LastExitTicksUtc = DateTime.UtcNow.AddSeconds(-50).Ticks });

            Internal.TimerService service = new Internal.TimerService(storage);
            service.Initialize();

            service.TryGet("quick", out TimerEntry quick);
            service.TryGet("slow", out TimerEntry slow);

            Assert.AreEqual(30, quick.ElapsedSeconds, "Quick timer must clamp at TotalSeconds.");
            Assert.IsTrue(quick.IsCompleted);
            Assert.AreEqual(60, slow.ElapsedSeconds, "Slow timer must receive the full 50s delta on top of its baseline 10s.");
            Assert.IsFalse(slow.IsCompleted);
        }

        [Test]
        public void SaveNow_PersistsTimersAndLastEnter()
        {
            InMemoryStorage storage = new InMemoryStorage();
            Internal.TimerService service = new Internal.TimerService(storage);
            service.Initialize();
            service.CreateOrReset("x", 500);

            long tickBeforeSave = storage.TryLoad(out LastEnterData last) ? last.LastExitTicksUtc : 0;
            service.SaveNow();
            Assert.IsTrue(storage.TryLoad(out LastEnterData lastAfter));
            Assert.GreaterOrEqual(lastAfter.LastExitTicksUtc, tickBeforeSave);

            Assert.IsTrue(storage.TryLoad(out TimersData timers));
            Assert.AreEqual(1, timers.Timers.Count);
            Assert.AreEqual("x", timers.Timers[0].Name);
        }

        /// <summary>In-memory <see cref="ILocalDataStorage"/> fake sufficient for the pure-logic tests above.</summary>
        private sealed class InMemoryStorage : ILocalDataStorage
        {
            private readonly Dictionary<(Type, string), LocalData> _store = new();

            public event Action<Type> DataSaved;
            public event Action<Type> DataDeleted;
            public event Action AllDeleted;

            public void Save<T>(T data) where T : LocalData => Save("", data);
            public void Save<T>(string slot, T data) where T : LocalData
            {
                _store[(typeof(T), slot ?? string.Empty)] = data;
                DataSaved?.Invoke(typeof(T));
            }

            public bool TryLoad<T>(out T data) where T : LocalData => TryLoad("", out data);
            public bool TryLoad<T>(string slot, out T data) where T : LocalData
            {
                if (_store.TryGetValue((typeof(T), slot ?? string.Empty), out LocalData stored) && stored is T typed)
                {
                    data = typed;
                    return true;
                }
                data = null;
                return false;
            }

            public void Delete<T>() where T : LocalData => Delete<T>("");
            public void Delete<T>(string slot) where T : LocalData
            {
                _store.Remove((typeof(T), slot ?? string.Empty));
                DataDeleted?.Invoke(typeof(T));
            }

            public IEnumerable<string> ListSlots<T>() where T : LocalData
            {
                foreach (var pair in _store)
                    if (pair.Key.Item1 == typeof(T))
                        yield return pair.Key.Item2;
            }

            public void DeleteAll()
            {
                _store.Clear();
                AllDeleted?.Invoke();
            }

            public UniTask SaveAsync<T>(T data, CancellationToken cancellationToken = default) where T : LocalData
            {
                Save(data); return UniTask.CompletedTask;
            }
            public UniTask SaveAsync<T>(string slot, T data, CancellationToken cancellationToken = default) where T : LocalData
            {
                Save(slot, data); return UniTask.CompletedTask;
            }
            public UniTask<(bool Found, T Data)> TryLoadAsync<T>(CancellationToken cancellationToken = default) where T : LocalData
                => UniTask.FromResult<(bool, T)>((TryLoad(out T d), d));
            public UniTask<(bool Found, T Data)> TryLoadAsync<T>(string slot, CancellationToken cancellationToken = default) where T : LocalData
                => UniTask.FromResult<(bool, T)>((TryLoad(slot, out T d), d));
            public UniTask DeleteAsync<T>(string slot = "", CancellationToken cancellationToken = default) where T : LocalData
            {
                Delete<T>(slot); return UniTask.CompletedTask;
            }
            public UniTask DeleteAllAsync(CancellationToken cancellationToken = default)
            {
                DeleteAll(); return UniTask.CompletedTask;
            }
        }
    }
}
