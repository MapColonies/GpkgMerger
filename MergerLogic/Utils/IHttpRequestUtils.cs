namespace MergerLogic.Utils
{
    public interface IHttpRequestUtils
    {
        byte[]? GetData(string url);

        string GetDataString(string url);

        string PostDataString(string url, FormUrlEncodedContent? content);

        string PutDataString(string url, FormUrlEncodedContent? content);

        T? GetData<T>(string url);
    }
}
