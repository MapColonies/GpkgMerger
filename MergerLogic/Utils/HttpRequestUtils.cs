using Microsoft.Extensions.Logging;

namespace MergerLogic.Utils
{
    public class HttpRequestUtils : IHttpRequestUtils
    {
        private HttpClient _httpClient;
        private ILogger<IHttpRequestUtils> _logger;

        public HttpRequestUtils(HttpClient httpClient, ILogger<IHttpRequestUtils> logger)
        {
            this._httpClient = httpClient;
            this._logger = logger;
        }

        ~HttpRequestUtils()
        {
            if (this._httpClient != null)
            {
                this._httpClient.Dispose();
            }
        }

        private HttpContent? GetContent(string url, HttpMethod method, HttpContent? content, bool ignoreNotFound)
        {
            Task<HttpResponseMessage> resTask;
            using (HttpRequestMessage req = new HttpRequestMessage
            {
                Method = method,
                RequestUri = new Uri(url),
                Content = content
            })
            {
                resTask = this._httpClient.SendAsync(req);
            }

            var httpRes = resTask.Result;
            if (httpRes.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                if (ignoreNotFound)
                {
                    return null;
                }

                string message = $"{url} not found";
                this._logger.LogWarning(message);
                this._logger.LogDebug($"Response: {httpRes.ToString()}");
                throw new Exception(message);
            }
            else if (httpRes.StatusCode != System.Net.HttpStatusCode.OK)
            {
                string message = $"Invalid response from {url}, status: {httpRes.StatusCode}";
                this._logger.LogWarning(message);
                this._logger.LogDebug($"Response: {httpRes.ToString()}");
                throw new Exception(message);
            }

            return httpRes.Content;
        }

        public byte[]? GetData(string url, bool ignoreNotFound = true)
        {
            HttpContent? resBody = GetContent(url, HttpMethod.Get, null, ignoreNotFound);
            var bodyTask = resBody?.ReadAsByteArrayAsync()!;
            return bodyTask.Result;
        }

        public string? PostDataString(string url, HttpContent? content, bool ignoreNotFound = true)
        {
            HttpContent? resBody = GetContent(url, HttpMethod.Post, content, ignoreNotFound);
            var bodyTask = resBody?.ReadAsStringAsync()!.Result;
            return bodyTask;
        }

        public string? PutDataString(string url, HttpContent? content, bool ignoreNotFound = true)
        {
            HttpContent? resBody = GetContent(url, HttpMethod.Put, content, ignoreNotFound);
            var bodyTask = resBody?.ReadAsStringAsync()!.Result;
            return bodyTask;
        }

        public string? GetDataString(string url, bool ignoreNotFound = true)
        {
            HttpContent? resBody = GetContent(url, HttpMethod.Get, null, ignoreNotFound);
            var bodyTask = resBody?.ReadAsStringAsync()!.Result;
            return bodyTask;
        }

        public T? GetData<T>(string url, bool ignoreNotFound = true)
        {
            HttpContent? content = GetContent(url, HttpMethod.Get, null, ignoreNotFound);
            var bodyTask = content?.ReadAsAsync<T>()!;
            return bodyTask.Result;
        }
    }
}
