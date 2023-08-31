using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.ComponentModel;

namespace MergerLogic.Utils
{
    public class ConfigurationManager : IConfigurationManager
    {
        private IConfiguration config;
        private ILogger? _logger;

        // From: https://stackoverflow.com/questions/27880433/using-iconfiguration-in-c-sharp-class-library
        public ConfigurationManager(ILogger<ConfigurationManager>? logger)
        {
            this._logger = logger;
            // Get configurations
            string basePath = System.AppContext.BaseDirectory;
            this.config = new ConfigurationBuilder()
                        .SetBasePath(basePath)
                        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                        .AddEnvironmentVariables()
                        .Build();
        }

        public IEnumerable<IConfigurationSection> GetChildren(params string[] configPath)
        {
            var config = this.config.GetSection(configPath[0]);
            for (int i = 1; i < configPath.Length - 1; i++)
            {
                config = this.config.GetSection(configPath[i]);
            }
            return config.GetSection(configPath[configPath.Length - 1]).GetChildren();
        }

        public string GetConfiguration(params string[] configPath)
        {
            // Configuration options: https://stackoverflow.com/a/41330941/11915280
            string key = string.Join(":", configPath);
            return this.config.GetValue<string>(key);
        }

        public T GetConfiguration<T>(params string[] configPath)
        {
            string value = this.GetConfiguration(configPath);
            if (value is null)
            {
                return default(T);
            }

            try
            {
                if (typeof(T).IsArray)
                {
                    return JsonConvert.DeserializeObject<T>(value);
                }
                return (T)TypeDescriptor.GetConverter(typeof(T)).ConvertFromString(value);
            }
            catch (Exception e)
            {
                string message = $"failed to parse configuration {string.Join('.', configPath)}.";
                this._logger?.LogError(message);
                throw new Exception(message, e);
            }
        }
    }
}
