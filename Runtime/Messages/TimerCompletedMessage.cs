namespace GDF.TimerService.Messages
{
    /// <summary>
    /// Published once when a timer reaches its total duration, right after the final
    /// <see cref="TimerTickedMessage"/>.
    /// </summary>
    public readonly struct TimerCompletedMessage
    {
        /// <summary>Creates the message.</summary>
        /// <param name="name">Timer identifier.</param>
        /// <param name="completedOffline"><see langword="true"/> when the timer finished while the game was closed and the completion was applied by <see cref="ITimerService.Initialize"/>.</param>
        public TimerCompletedMessage(string name, bool completedOffline)
        {
            Name = name;
            CompletedOffline = completedOffline;
        }

        /// <summary>Timer identifier.</summary>
        public string Name { get; }

        /// <summary><see langword="true"/> when the timer finished while the game was closed and the completion was applied by <see cref="ITimerService.Initialize"/>.</summary>
        public bool CompletedOffline { get; }
    }
}
