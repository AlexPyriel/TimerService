namespace GDF.TimerService
{
    /// <summary>
    /// Named countdown timers with offline progression and auto-save. Suitable for daily rewards,
    /// cooldowns, stamina regeneration, timed quests. Timers survive game restarts and advance by the
    /// real-time offline delta when the service initializes. State is read with queries
    /// (<see cref="TryGet"/>, <see cref="IsCompleted"/>); lifecycle facts are published through MessagePipe as
    /// <see cref="Messages.TimerStartedMessage"/>, <see cref="Messages.TimerTickedMessage"/>,
    /// <see cref="Messages.TimerCompletedMessage"/> and <see cref="Messages.TimerRemovedMessage"/>.
    /// </summary>
    public interface ITimerService
    {
        /// <summary>
        /// Initializes the service: loads persisted state, applies the offline delta, starts the tick/autosave loop.
        /// Call once at app startup. The offline delta publishes one <see cref="Messages.TimerTickedMessage"/> per
        /// running timer and a <see cref="Messages.TimerCompletedMessage"/> with <c>CompletedOffline</c> set for each
        /// timer it finishes.
        /// </summary>
        public void Initialize();

        /// <summary>Creates a new timer or resets an existing one. Publishes <see cref="Messages.TimerStartedMessage"/>.</summary>
        /// <param name="name">Timer identifier.</param>
        /// <param name="totalSeconds">Total duration in seconds. Negative values are clamped to zero.</param>
        public void CreateOrReset(string name, int totalSeconds);

        /// <summary>Removes a timer from the active set. Publishes <see cref="Messages.TimerRemovedMessage"/> when it existed.</summary>
        /// <param name="name">Timer identifier.</param>
        /// <returns><see langword="true"/> when a timer with that name existed and was removed.</returns>
        public bool Remove(string name);

        /// <summary>Returns the current snapshot of a timer, or <see langword="false"/> when no timer with that name exists.</summary>
        /// <param name="name">Timer identifier.</param>
        /// <param name="entry">Snapshot of the timer when found.</param>
        /// <returns><see langword="true"/> when the timer exists.</returns>
        public bool TryGet(string name, out TimerEntry entry);

        /// <summary>Whether the named timer exists and has reached its total duration.</summary>
        public bool IsCompleted(string name);

        /// <summary>Flushes the current state to the persistence layer immediately. Normally the service autosaves on its tick cadence.</summary>
        public void SaveNow();
    }
}
