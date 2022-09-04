namespace MergerLogic.Clients
{
    public interface IHttpRequestUtils
    {
        byte[]? GetData(string url, bool ignoreNotFound = false);

        string? GetDataString(string url, bool ignoreNotFound = false);

        string? PostData(string url, HttpContent? content, bool ignoreNotFound = false);

        string? PutData(string url, HttpContent? content, bool ignoreNotFound = false);

        T? GetData<T>(string url, bool ignoreNotFound = false);
    }
}
