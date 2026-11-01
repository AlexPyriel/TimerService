using VContainer;

namespace TimerService.Installers.VContainer
{
    /// <summary>
    /// Registers <see cref="ITimerService"/> in a VContainer scope. Call <c>service.Initialize()</c>
    /// from a bootstrap entry point once the host container is built.
    /// </summary>
    public sealed class TimerServiceVContainerInstaller : IInstaller
    {
        private TimerServiceConfig _config = TimerServiceConfig.Default();

        /// <summary>Sets the autosave cadence in seconds.</summary>
        public TimerServiceVContainerInstaller WithAutosaveSeconds(int autosaveSeconds)
        {
            _config = new TimerServiceConfig(autosaveSeconds);
            return this;
        }

        /// <inheritdoc />
        public void Install(IContainerBuilder builder)
        {
            builder.RegisterInstance(_config);
            builder
                .Register<Internal.TimerService>(Lifetime.Singleton)
                .As<ITimerService>();
        }
    }
}
