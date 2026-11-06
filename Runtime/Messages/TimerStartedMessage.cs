namespace GDF.TimerService.Messages
{
    /// <summary>
    /// Published when <see cref="ITimerService.CreateOrReset"/> starts a timer or restarts an existing one.
    /// </summary>
    public readonly struct TimerStartedMessage
    {
        /// <summary>Creates the message.</summary>
        /// <param name="name">Timer identifier.</param>
        /// <param name="totalSeconds">Full duration of the timer in seconds.</param>
        public TimerStartedMessage(string name, int totalSeconds)
        {
            Name = name;
            TotalSeconds = totalSeconds;
        }

        /// <summary>Timer identifier.</summary>
        public string Name { get; }

        /// <summary>Full duration of the timer in seconds.</summary>
        public int TotalSeconds { get; }
    }
}
