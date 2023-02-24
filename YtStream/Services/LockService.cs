using AyrA.AutoDI;
using Microsoft.Extensions.Logging;

namespace YtStream.Services
{
    [AutoDIRegister(AutoDIType.Singleton)]
    public class LockService
    {
        private readonly ILogger<LockService> _logger;

        public bool Locked { get; private set; }

        public LockService(ILogger<LockService> logger)
        {
            _logger = logger;
        }

        public void Lock()
        {
            _logger.LogWarning("Requesting application lock");
            Locked = true;
        }
        public void Unlock()
        {
            _logger.LogInformation("Requesting application unlock");
            Locked = false;
        }
    }
}
