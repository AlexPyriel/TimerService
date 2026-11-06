namespace GDF.TimerService.Messages
{
    /// <summary>
    /// Published when <see cref="ITimerService.Remove"/> drops an existing timer.
    /// </summary>
    public readonly struct TimerRemovedMessage
    {
        /// <summary>Creates the message.</summary>
        /// <param name="name">Timer identifier.</param>
        public TimerRemovedMessage(string name)
        {
            Name = name;
        }

        /// <summary>Timer identifier.</summary>
        public string Name { get; }
    }
}
