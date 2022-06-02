namespace MergerLogic.Utils
{
    public interface IConfigurationManager
    {
        string GetConfiguration(params string[] configPath);
    }
}