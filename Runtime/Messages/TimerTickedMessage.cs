namespace GDF.TimerService.Messages
{
    /// <summary>
    /// Published when a running timer advances: once per second while the game runs, and once per
    /// timer for the offline delta applied by <see cref="ITimerService.Initialize"/>. Drives countdown labels.
    /// </summary>
    public readonly struct TimerTickedMessage
    {
        /// <summary>Creates the message.</summary>
        /// <param name="name">Timer identifier.</param>
        /// <param name="remainingSeconds">Seconds left until completion; zero when the tick completed the timer.</param>
        public TimerTickedMessage(string name, int remainingSeconds)
        {
            Name = name;
            RemainingSeconds = remainingSeconds;
        }

        /// <summary>Timer identifier.</summary>
        public string Name { get; }

        /// <summary>Seconds left until completion; zero when the tick completed the timer.</summary>
        public int RemainingSeconds { get; }
    }
}
