using Microsoft.Extensions.Configuration;

namespace GpkgMerger.Src.Utils
{
    public class Configuration
    {
        private static Configuration instance = null;

        private IConfiguration config;

        // From: https://stackoverflow.com/questions/27880433/using-iconfiguration-in-c-sharp-class-library
        private Configuration()
        {
            // Get configurations
            string basePath = System.AppContext.BaseDirectory;
            config = new ConfigurationBuilder()
                        .SetBasePath(basePath)
                        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                        .AddEnvironmentVariables()
                        .Build();
        }

        public string GetConfiguration(params string[] configPath)
        {
            var config = this.config.GetSection(configPath[0]);
            for (int i = 1; i < configPath.Length - 1; i++)
            {
                config = this.config.GetSection(configPath[i]);
            }
            return config[configPath[configPath.Length - 1]];
        }

        public static Configuration Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new Configuration();
                }
                return instance;
            }
        }
    }
}
