using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using GDF.TimerService.Data;
using GDF.TimerService.Messages;
using LocalDataStorage;
using MessagePipe;
using NUnit.Framework;

namespace GDF.TimerService.Tests.EditMode
{
    /// <summary>
    /// Pure-logic tests for <see cref="Internal.TimerService"/> on a real MessagePipe broker. The tick
    /// loop is not exercised here; offline progression is driven by seeding <see cref="LastEnterData"/>
    /// in the past before <see cref="Internal.TimerService.Initialize"/>.
    /// </summary>
    public sealed class TimerServiceTests
    {
        private IPublisher<TimerStartedMessage> _startedPublisher;
        private IPublisher<TimerTickedMessage> _tickedPublisher;
        private IPublisher<TimerCompletedMessage> _completedPublisher;
        private IPublisher<TimerRemovedMessage> _removedPublisher;
        private List<TimerStartedMessage> _started;
        private List<TimerTickedMessage> _ticked;
        private List<TimerCompletedMessage> _completed;
        private List<TimerRemovedMessage> _removed;
        private IDisposable _subscriptions;
        private InMemoryStorage _storage;
        private Internal.TimerService _service;

        [SetUp]
        public void SetUp()
        {
            BuiltinContainerBuilder pipe = new BuiltinContainerBuilder();
            pipe.AddMessagePipe();
            pipe.AddMessageBroker<TimerStartedMessage>();
            pipe.AddMessageBroker<TimerTickedMessage>();
            pipe.AddMessageBroker<TimerCompletedMessage>();
            pipe.AddMessageBroker<TimerRemovedMessage>();
            IServiceProvider provider = pipe.BuildServiceProvider();
            _startedPublisher = provider.GetRequiredService<IPublisher<TimerStartedMessage>>();
            _tickedPublisher = provider.GetRequiredService<IPublisher<TimerTickedMessage>>();
            _completedPublisher = provider.GetRequiredService<IPublisher<TimerCompletedMessage>>();
            _removedPublisher = provider.GetRequiredService<IPublisher<TimerRemovedMessage>>();

            _started = new List<TimerStartedMessage>();
            _ticked = new List<TimerTickedMessage>();
            _completed = new List<TimerCompletedMessage>();
            _removed = new List<TimerRemovedMessage>();
            _subscriptions = DisposableBag.Create(
                provider.GetRequiredService<ISubscriber<TimerStartedMessage>>().Subscribe(_started.Add),
                provider.GetRequiredService<ISubscriber<TimerTickedMessage>>().Subscribe(_ticked.Add),
                provider.GetRequiredService<ISubscriber<TimerCompletedMessage>>().Subscribe(_completed.Add),
                provider.GetRequiredService<ISubscriber<TimerRemovedMessage>>().Subscribe(_removed.Add));

            _storage = new InMemoryStorage();
        }

        [TearDown]
        public void TearDown()
        {
            _service?.Dispose();
            _service = null;
            _subscriptions.Dispose();
        }

        [Test]
        public void Ctor_NullPublisher_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new Internal.TimerService(_storage, null, _tickedPublisher, _completedPublisher, _removedPublisher));
            Assert.Throws<ArgumentNullException>(() => new Internal.TimerService(_storage, _startedPublisher, null, _completedPublisher, _removedPublisher));
            Assert.Throws<ArgumentNullException>(() => new Internal.TimerService(_storage, _startedPublisher, _tickedPublisher, null, _removedPublisher));
            Assert.Throws<ArgumentNullException>(() => new Internal.TimerService(_storage, _startedPublisher, _tickedPublisher, _completedPublisher, null));
        }

        [Test]
        public void CreateOrReset_StoresTimer_AndPublishesStarted()
        {
            _service = CreateService();
            _service.Initialize();

            _service.CreateOrReset("daily_reward", 86400);

            Assert.IsTrue(_service.TryGet("daily_reward", out TimerEntry entry));
            Assert.AreEqual(86400, entry.TotalSeconds);
            Assert.AreEqual(0, entry.ElapsedSeconds);
            Assert.AreEqual(1, _started.Count);
            Assert.AreEqual("daily_reward", _started[0].Name);
            Assert.AreEqual(86400, _started[0].TotalSeconds);
            Assert.IsEmpty(_ticked);
        }

        [Test]
        public void Remove_DropsTimer_AndPublishesRemovedOnce()
        {
            _service = CreateService();
            _service.Initialize();
            _service.CreateOrReset("t", 100);

            Assert.IsTrue(_service.Remove("t"));
            Assert.IsFalse(_service.TryGet("t", out _));
            Assert.IsFalse(_service.Remove("t"), "Second Remove reports false.");
            Assert.IsFalse(_service.Remove("missing"));

            Assert.AreEqual(1, _removed.Count, "Removing an unknown timer publishes nothing.");
            Assert.AreEqual("t", _removed[0].Name);
        }

        [Test]
        public void OfflineDelta_AdvancesTimers_ClampedAtTotal_AndPublishesOfflineCompletion()
        {
            // Seed persisted state as if the previous session had running timers and exited 50s ago.
            _storage.Save(new TimersData
            {
                Timers = new List<TimerEntry>
                {
                    new TimerEntry { Name = "quick", TotalSeconds = 30, ElapsedSeconds = 0 },
                    new TimerEntry { Name = "slow", TotalSeconds = 1000, ElapsedSeconds = 10 },
                    new TimerEntry { Name = "done", TotalSeconds = 5, ElapsedSeconds = 5 },
                },
            });
            _storage.Save(new LastEnterData { LastExitTicksUtc = DateTime.UtcNow.AddSeconds(-50).Ticks });

            _service = CreateService();
            _service.Initialize();

            _service.TryGet("quick", out TimerEntry quick);
            _service.TryGet("slow", out TimerEntry slow);
            Assert.AreEqual(30, quick.ElapsedSeconds, "Quick timer must clamp at TotalSeconds.");
            Assert.IsTrue(quick.IsCompleted);
            Assert.AreEqual(60, slow.ElapsedSeconds, "Slow timer must receive the full 50s delta on top of its baseline 10s.");
            Assert.IsFalse(slow.IsCompleted);

            Assert.AreEqual(2, _ticked.Count, "One tick per running timer; already completed timers stay silent.");
            Assert.AreEqual(0, FindTicked("quick").RemainingSeconds);
            Assert.AreEqual(940, FindTicked("slow").RemainingSeconds);

            Assert.AreEqual(1, _completed.Count);
            Assert.AreEqual("quick", _completed[0].Name);
            Assert.IsTrue(_completed[0].CompletedOffline);
            Assert.IsEmpty(_started, "Restoring timers is not starting them.");
        }

        [Test]
        public void SaveNow_PersistsTimersAndLastEnter()
        {
            _service = CreateService();
            _service.Initialize();
            _service.CreateOrReset("x", 500);

            long tickBeforeSave = _storage.TryLoad(out LastEnterData last) ? last.LastExitTicksUtc : 0;
            _service.SaveNow();
            Assert.IsTrue(_storage.TryLoad(out LastEnterData lastAfter));
            Assert.GreaterOrEqual(lastAfter.LastExitTicksUtc, tickBeforeSave);

            Assert.IsTrue(_storage.TryLoad(out TimersData timers));
            Assert.AreEqual(1, timers.Timers.Count);
            Assert.AreEqual("x", timers.Timers[0].Name);
        }

        private Internal.TimerService CreateService()
        {
            return new Internal.TimerService(
                _storage, _startedPublisher, _tickedPublisher, _completedPublisher, _removedPublisher, TimerServiceConfig.Default());
        }

        private TimerTickedMessage FindTicked(string name)
        {
            foreach (TimerTickedMessage message in _ticked)
                if (message.Name == name)
                    return message;

            Assert.Fail($"No tick published for '{name}'.");
            return default;
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
