using System;

namespace TimerService
{
    /// <summary>
    /// Snapshot of a single named timer. Returned by read calls on <see cref="ITimerService"/>; game
    /// code treats the struct as a value, so the returned data stays consistent while the service may
    /// continue mutating its internal store on a tick.
    /// </summary>
    [Serializable]
    public struct TimerEntry
    {
        /// <summary>Unique timer identifier (e.g. "daily_reward", "stamina_regen").</summary>
        public string Name;

        /// <summary>Total duration in seconds configured by <see cref="ITimerService.CreateOrReset"/>.</summary>
        public int TotalSeconds;

        /// <summary>Elapsed seconds. Reaches but does not exceed <see cref="TotalSeconds"/>.</summary>
        public int ElapsedSeconds;

        /// <summary>Remaining seconds until completion. Reaches zero on completion.</summary>
        public int RemainingSeconds => Math.Max(0, TotalSeconds - ElapsedSeconds);

        /// <summary>Whether the timer has reached its total duration.</summary>
        public bool IsCompleted => ElapsedSeconds >= TotalSeconds;
    }
}
