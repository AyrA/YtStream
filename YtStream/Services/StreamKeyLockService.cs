using AyrA.AutoDI;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using YtStream.Services.Accounts;

namespace YtStream.Services
{
    [AutoDIRegister(AutoDIType.Singleton)]
    public class StreamKeyLockService
    {
        public const int MaxSemaphoreCount = 99;
        private readonly Dictionary<Guid, StreamKeyInfo> keyInfo = new();
        private readonly ConfigService _configService;
        private readonly UserManagerService _userManager;
        private readonly ILogger<StreamKeyLockService> _logger;

        public bool IsAdjustingSettings { get; private set; }

        public StreamKeyLockService(ConfigService configService, UserManagerService userManager,
            ILogger<StreamKeyLockService> logger)
        {
            _configService = configService;
            _userManager = userManager;
            _logger = logger;
        }

        public Task UseKeyAsync(Guid key) => UseKeyAsync(key, Timeout.InfiniteTimeSpan);

        public async Task<bool> UseKeyAsync(Guid key, TimeSpan maxWait)
        {
            var acc = _userManager.GetUser(key);
            if (acc == null || !acc.Enabled)
            {
                _logger.LogWarning("Cannot find account for key {key}", key);
                return false;
            }
            StreamKeyInfo? kInfo;
            lock (keyInfo)
            {
                if (!keyInfo.TryGetValue(key, out kInfo))
                {
                    kInfo = new StreamKeyInfo(key, _configService.GetConfiguration().MaxKeyUsageCount);
                    keyInfo[key] = kInfo;
                }
            }
            return await kInfo.Semaphore.WaitAsync(maxWait);
        }

        public bool IsKeyInUse(Guid key)
        {
            if (keyInfo.TryGetValue(key, out var kInfo))
            {
                return kInfo.InUse;
            }
            return false;
        }

        public void FreeKey(Guid key)
        {
            lock (keyInfo)
            {
                if (keyInfo.TryGetValue(key, out var info))
                {
                    info.Semaphore.Release();
                }
            }
        }

        /// <summary>
        /// Updates the maximum allowed stream count from the configuration
        /// </summary>
        public void UpdateMaxCount()
        {
            lock (this)
            {
                if (IsAdjustingSettings)
                {
                    throw new InvalidOperationException("The service is still adjusting the max count from a previous call");
                }
                IsAdjustingSettings = true;
            }
            var configuredMaxCount = _configService.GetConfiguration().MaxKeyUsageCount;
            //Create a cache of info structures to not lock the list for too long
            //It doesn't matters if keys are added during the current runtime
            //because they'll already have the new key usage limit
            StreamKeyInfo[]? info;
            lock (keyInfo)
            {
                info = keyInfo.Values.ToArray();
            }
            _logger.LogInformation("Adjusting max key usage count for {count} keys", info.Length);
            //This is done in a thread because it can potentially take a long time
            new Thread(() =>
            {
                try
                {
                    var concurrency = Math.Max(1, Environment.ProcessorCount - 1);
                    //Spawn at most "CpuCount-1" threads
                    using var limiter = new SemaphoreSlim(concurrency);
                    foreach (var key in info)
                    {
                        var t = new Thread((o) =>
                        {
                            try
                            {
                                var key = o as StreamKeyInfo ?? throw null!;
                                _logger.LogDebug("Adjusting max key usage count for {key}", key.Key);
                                key.SetMaxCount(configuredMaxCount);
                            }
                            finally
                            {
                                limiter.Release();
                            }
                        });
                        limiter.Wait();
                        t.IsBackground = true;
                        t.Priority = ThreadPriority.Lowest;
                        t.Start(key);
                    }
                    //This is an easy trick to wait for all threads to end.
                    //We are here either because the last thread got past the limiter,
                    //or because there are less threads than the limiter allows.
                    //In either way, any request for the limiter is already queued up at this point.
                    //By consuming all possible slots of the limiter we know the threads have all ended,
                    //because the previous loop could not have completed without starting them all.
                    //In other words, it's impossible that we deadlock this
                    //because everything that can be queued up at the semaphore, is already.
                    //And we only have to wait for threads to release it.
                    for (var i = 0; i < concurrency; i++)
                    {
                        limiter.Wait();
                    }
                }
                finally
                {
                    IsAdjustingSettings = false;
                }
            }).Start();
        }

        public void Cleanup()
        {
            lock (keyInfo)
            {
                var freeKeys = keyInfo.Where(m => !m.Value.InUse).Select(m => m.Key).ToList();
                foreach (var key in freeKeys)
                {
                    keyInfo.Remove(key);
                }
            }
        }

        private class StreamKeyInfo : IDisposable
        {
            public Guid Key { get; }

            public SemaphoreSlim Semaphore { get; }

            public int MaxCount { get; private set; }

            public int RemainCount => Semaphore.CurrentCount;

            public int UseCount => MaxCount - Semaphore.CurrentCount;

            public bool InUse => RemainCount != MaxCount;

            public StreamKeyInfo(Guid key, int configCount)
            {
                Key = key;
                Semaphore = new SemaphoreSlim(configCount, MaxSemaphoreCount);
                MaxCount = configCount;
            }

            public void SetMaxCount(int value)
            {
                if (value < 1 || value > MaxSemaphoreCount)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }
                lock (this)
                {
                    if (value > MaxCount)
                    {
                        Semaphore.Release(value - MaxCount);
                        MaxCount = value;
                    }
                    while (value < MaxCount)
                    {
                        Semaphore.Wait();
                        --MaxCount;
                    }
                }
            }

            public void Dispose()
            {
                Semaphore.Dispose();
                GC.SuppressFinalize(this);
            }
        }
    }
}
