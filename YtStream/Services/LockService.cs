using AyrA.AutoDI;
using Microsoft.Extensions.Logging;

namespace YtStream.Services
{
    /// <summary>
    /// Provides a global lockout mechanism for the application
    /// </summary>
    [AutoDIRegister(AutoDIType.Singleton)]
    public class ApplicationLockService
    {
        private readonly ILogger<ApplicationLockService> _logger;

        /// <summary>
        /// Gets if the application is locked
        /// </summary>
        public bool Locked { get; private set; }

        public ApplicationLockService(ILogger<ApplicationLockService> logger)
        {
            _logger = logger;
            Locked = false;
        }

        /// <summary>
        /// Lock the application
        /// </summary>
        public void Lock()
        {
            lock (this)
            {
                _logger.LogWarning("Requesting application lock");
                Locked = true;
            }
        }

        /// <summary>
        /// Unlock the application
        /// </summary>
        public void Unlock()
        {
            lock (this)
            {
                _logger.LogWarning("Requesting application unlock");
                Locked = false;
            }
        }
    }
}
