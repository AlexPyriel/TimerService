using System;

namespace GDF.TimerService
{
    /// <summary>
    /// Named countdown timers with offline progression and auto-save. Suitable for daily rewards,
    /// cooldowns, stamina regeneration, timed quests. Timers survive game restarts and advance by the
    /// real-time offline delta when the service initializes.
    /// </summary>
    public interface ITimerService
    {
        /// <summary>Raised when any timer value changes (progress or creation).</summary>
        public event Action TimersChanged;

        /// <summary>Raised when a specific timer changes. The argument is the timer name.</summary>
        public event Action<string> TimerChanged;

        /// <summary>Raised when a timer reaches its total duration. The argument is the timer name.</summary>
        public event Action<string> TimerCompleted;

        /// <summary>Initializes the service: loads persisted state, applies the offline delta, starts the tick/autosave loop. Call once at app startup.</summary>
        public void Initialize();

        /// <summary>Creates a new timer or resets an existing one.</summary>
        /// <param name="name">Timer identifier.</param>
        /// <param name="totalSeconds">Total duration in seconds. Negative values are clamped to zero.</param>
        public void CreateOrReset(string name, int totalSeconds);

        /// <summary>Removes a timer from the active set.</summary>
        /// <param name="name">Timer identifier.</param>
        /// <returns><see langword="true"/> when a timer with that name existed and was removed.</returns>
        public bool Remove(string name);

        /// <summary>Returns the current snapshot of a timer, or <see langword="false"/> when no timer with that name exists.</summary>
        public bool TryGet(string name, out TimerEntry entry);

        /// <summary>Whether the named timer exists and has reached its total duration.</summary>
        public bool IsCompleted(string name);

        /// <summary>Flushes the current state to the persistence layer immediately. Normally the service autosaves on its tick cadence.</summary>
        public void SaveNow();
    }
}
