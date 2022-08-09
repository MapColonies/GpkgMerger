namespace MergerLogic.Utils
{
    public interface IHttpRequestUtils
    {
        byte[]? GetData(string url, bool ignoreNotFound = true);

        string GetDataString(string url, bool ignoreNotFound = true);

        string PostDataString(string url, HttpContent? content, bool ignoreNotFound = true);

        string PutDataString(string url, HttpContent? content, bool ignoreNotFound = true);

        T? GetData<T>(string url, bool ignoreNotFound = true);
    }
}
