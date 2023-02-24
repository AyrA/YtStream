using AyrA.AutoDI;
using System;
using System.IO;
using YtStream.Extensions;
using YtStream.Models;

namespace YtStream.Services
{
    [AutoDIRegister(AutoDIType.Singleton)]
    public class ConfigService
    {
        /// <summary>
        /// File name where config is stored
        /// </summary>
        public const string ConfigFileName = "config.json";

        private readonly string _basePath;

        public ConfigService(BasePathService basePathService)
        {
            _basePath = basePathService.BasePath;
        }

        public ConfigModel GetConfiguration()
        {
            var F = Path.Combine(_basePath, ConfigFileName);
            try
            {
                return File.ReadAllText(F).FromJson<ConfigModel>(true);
            }
            catch (FileNotFoundException)
            {
                return new ConfigModel();
            }
        }

        public void SaveConfiguration(ConfigModel model)
        {
            if (model is null)
            {
                throw new ArgumentNullException(nameof(model));
            }
            var F = Path.Combine(_basePath, ConfigFileName);
            File.WriteAllText(F, model.ToJson(true));
        }
    }
}