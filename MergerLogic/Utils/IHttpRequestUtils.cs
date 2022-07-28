namespace MergerLogic.Utils
{
    public interface IHttpRequestUtils
    {
        byte[]? GetData(string url);

        string GetDataString(string url);

        string PostDataString(string url);

        string PutDataString(string url, StringContent body);

        T? GetData<T>(string url);
    }
}
