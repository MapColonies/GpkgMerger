using Microsoft.Extensions.Logging;
using System.Net;

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
            HttpResponseMessage httpRes;
            using (HttpRequestMessage req = new HttpRequestMessage
            {
                Method = method,
                RequestUri = new Uri(url),
                Content = content
            })
            {
                httpRes = this._httpClient.Send(req);
            }

            if (httpRes.StatusCode == HttpStatusCode.NotFound)
            {
                if (ignoreNotFound)
                {
                    return null;
                }

                string message = $"{url} not found";
                this._logger.LogDebug(message);
                this._logger.LogDebug($"Response: {httpRes.ToString()}");
                throw new HttpRequestException(message, null, HttpStatusCode.NotFound);
            }
            else if (httpRes.StatusCode != HttpStatusCode.OK)
            {
                string message = $"Invalid response from {url}, status: {httpRes.StatusCode}";
                this._logger.LogWarning(message);
                this._logger.LogDebug($"Response: {httpRes.ToString()}");
                throw new HttpRequestException(message, null, httpRes.StatusCode);
            }

            return httpRes.Content;
        }

        public byte[]? GetData(string url, bool ignoreNotFound = false)
        {
            HttpContent? resBody = GetContent(url, HttpMethod.Get, null, ignoreNotFound);
            var bodyTask = resBody?.ReadAsByteArrayAsync()!;
            return bodyTask.Result;
        }

        public string? PostDataString(string url, HttpContent? content, bool ignoreNotFound = false)
        {
            HttpContent? resBody = GetContent(url, HttpMethod.Post, content, ignoreNotFound);
            var bodyTask = resBody?.ReadAsStringAsync()!.Result;
            return bodyTask;
        }

        public string? PutDataString(string url, HttpContent? content, bool ignoreNotFound = false)
        {
            HttpContent? resBody = GetContent(url, HttpMethod.Put, content, ignoreNotFound);
            var bodyTask = resBody?.ReadAsStringAsync()!.Result;
            return bodyTask;
        }

        public string? GetDataString(string url, bool ignoreNotFound = false)
        {
            HttpContent? resBody = GetContent(url, HttpMethod.Get, null, ignoreNotFound);
            var bodyTask = resBody?.ReadAsStringAsync()!.Result;
            return bodyTask;
        }

        public T? GetData<T>(string url, bool ignoreNotFound = false)
        {
            HttpContent? content = GetContent(url, HttpMethod.Get, null, ignoreNotFound);
            var bodyTask = content?.ReadAsAsync<T>()!;
            return bodyTask.Result;
        }
    }
}
