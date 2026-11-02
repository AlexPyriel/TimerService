using Zenject;

namespace GDF.TimerService.Installers.Zenject
{
    /// <summary>Registers <see cref="ITimerService"/> in a Zenject container. Config injected as an argument.</summary>
    public sealed class TimerServiceZenjectInstaller : Installer<TimerServiceConfig, TimerServiceZenjectInstaller>
    {
        [Inject]
        private TimerServiceConfig _config;

        /// <summary>Returns the default config when the host passes none.</summary>
        public static TimerServiceConfig DefaultConfig() => TimerServiceConfig.Default();

        /// <inheritdoc />
        public override void InstallBindings()
        {
            Container.BindInstance(_config);
            Container
                .Bind<ITimerService>()
                .To<Internal.TimerService>()
                .AsSingle();
        }
    }
}
