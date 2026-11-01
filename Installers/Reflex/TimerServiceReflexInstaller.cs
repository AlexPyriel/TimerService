using Reflex.Core;
using UnityEngine;

namespace TimerService.Installers.Reflex
{
    /// <summary>Registers <see cref="ITimerService"/> in a Reflex container.</summary>
    public sealed class TimerServiceReflexInstaller : MonoBehaviour, IInstaller
    {
        [SerializeField]
        [Tooltip("Autosave cadence in seconds. Must be positive.")]
        private int _autosaveSeconds = TimerServiceConfig.DEFAULT_AUTOSAVE_SECONDS;

        /// <inheritdoc />
        public void InstallBindings(ContainerBuilder builder)
        {
            builder.AddSingleton(new TimerServiceConfig(_autosaveSeconds), typeof(TimerServiceConfig));
            builder.AddSingleton(
                typeof(Internal.TimerService),
                typeof(ITimerService));
        }
    }
}
