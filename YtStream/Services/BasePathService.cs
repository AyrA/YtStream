using AyrA.AutoDI;
using Microsoft.Extensions.Configuration;
using System;

namespace YtStream.Services
{
    [AutoDIRegister(AutoDIType.Singleton)]
    public class BasePathService
    {
        public string BasePath { get; }

        public BasePathService(IConfiguration config)
        {
            var configuredValue = config.GetValue<string>("Config:BasePath");
            BasePath = string.IsNullOrEmpty(configuredValue) ? AppContext.BaseDirectory : configuredValue;
            /*
            //Set current directory because for services it's wrong.
            using var P = Process.GetCurrentProcess();
            var BaseDir = Path.GetDirectoryName(P.MainModule.FileName);
            while (!Directory.Exists(Path.Combine(BaseDir, "wwwroot")))
            {
                var NewDir = Path.GetFullPath(Path.Combine(BaseDir, ".."));
                if (NewDir == BaseDir)
                {
                    throw new Exception("Unable to find path to 'wwwroot' folder");
                }
                BaseDir = NewDir;
            }
            BasePath = BaseDir;
            //*/
        }
    }
}
