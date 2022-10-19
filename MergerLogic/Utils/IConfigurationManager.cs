using Amazon.Extensions.NETCore.Setup;
using Microsoft.Extensions.Configuration;

namespace MergerLogic.Utils
{
    public interface IConfigurationManager
    {
        IEnumerable<IConfigurationSection> GetChildren(params string[] configPath);

        string GetConfiguration(params string[] configPath);

        T GetConfiguration<T>(params string[] configPath);

        AWSOptions GetAWSOptions();
    }
}
