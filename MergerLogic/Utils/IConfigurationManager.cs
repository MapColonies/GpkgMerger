namespace MergerLogic.Utils
{
    public interface IConfigurationManager
    {
        string GetConfiguration(params string[] configPath);

        T GetConfiguration<T>(params string[] configPath);
    }
}
