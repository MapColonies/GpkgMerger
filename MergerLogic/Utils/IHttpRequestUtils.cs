namespace MergerLogic.Utils
{
    public interface IHttpRequestUtils
    {
        byte[]? GetData(string url, bool ignoreNotFound = false);

        string? GetDataString(string url, bool ignoreNotFound = false);

        string? PostDataString(string url, HttpContent? content, bool ignoreNotFound = false);

        string? PutDataString(string url, HttpContent? content, bool ignoreNotFound = false);

        T? GetData<T>(string url, bool ignoreNotFound = false);
    }
}
