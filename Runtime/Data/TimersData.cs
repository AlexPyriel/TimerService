using System;
using System.Collections.Generic;
using LocalDataStorage;

namespace GDF.TimerService.Data
{
    /// <summary>
    /// Persisted collection of active timers. Serialized through <see cref="LocalDataStorage.ILocalDataStorage"/>.
    /// </summary>
    [Serializable]
    public sealed class TimersData : LocalData
    {
        /// <summary>Active timer entries.</summary>
        public List<TimerEntry> Timers = new List<TimerEntry>();
    }
}
