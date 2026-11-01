namespace TimerService
{
    /// <summary>
    /// Immutable configuration for <see cref="ITimerService"/>. Hosts build it through the fluent
    /// methods on the DI installers.
    /// </summary>
    public sealed class TimerServiceConfig
    {
        /// <summary>Default autosave cadence in seconds.</summary>
        public const int DEFAULT_AUTOSAVE_SECONDS = 60;

        /// <summary>Autosave cadence in seconds. The service also saves on <c>Remove</c> and <c>CreateOrReset</c>.</summary>
        public int AutosaveSeconds { get; }

        public TimerServiceConfig(int autosaveSeconds = DEFAULT_AUTOSAVE_SECONDS)
        {
            AutosaveSeconds = autosaveSeconds <= 0 ? DEFAULT_AUTOSAVE_SECONDS : autosaveSeconds;
        }

        /// <summary>Default configuration (autosave every 60 seconds).</summary>
        public static TimerServiceConfig Default() => new();
    }
}
