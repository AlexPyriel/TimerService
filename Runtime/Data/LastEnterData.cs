using System;
using LocalDataStorage;

namespace TimerService.Data
{
    /// <summary>
    /// Timestamp (<see cref="DateTime.Ticks"/>) of the last time the service was running. Used on
    /// startup to compute the offline delta that is applied to every active timer.
    /// </summary>
    [Serializable]
    public sealed class LastEnterData : LocalData
    {
        /// <summary>UTC tick count captured at the previous autosave.</summary>
        public long LastExitTicksUtc;
    }
}
