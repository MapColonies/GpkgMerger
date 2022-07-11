namespace MergerLogic.Utils
{
    public interface IHttpRequestUtils
    {
        byte[]? GetData(string url);

        string GetDataString(string url);

        T? GetData<T>(string url);
    }
}
