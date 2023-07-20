using Microsoft.Extensions.Logging;
using System.Net;
using System.Reflection;

namespace MergerLogic.Clients
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
                       Method = method, RequestUri = new Uri(url), Content = content,
                   })
            {
                httpRes = this._httpClient.SendAsync(req).Result;
            }

            if (httpRes.StatusCode == HttpStatusCode.NotFound)
            {
                if (ignoreNotFound)
                {
                    return null;
                }

                string message = $"{url} not found";
                this._logger.LogDebug($"[{MethodBase.GetCurrentMethod().Name}] message: {message}, Response: {httpRes.ToString()}");
                throw new HttpRequestException(message, null, HttpStatusCode.NotFound);
            }
            else if (httpRes.StatusCode != HttpStatusCode.OK)
            {
                string message = $"Invalid response from {url}, status: {httpRes.StatusCode}";
                this._logger.LogWarning($"[{MethodBase.GetCurrentMethod().Name}] message: {message}");
                this._logger.LogDebug($"[{MethodBase.GetCurrentMethod().Name}] Response: {httpRes.ToString()}");
                throw new HttpRequestException(message, null, httpRes.StatusCode);
            }

            return httpRes.Content;
        }

        public byte[]? GetData(string url, bool ignoreNotFound = false)
        {
            HttpContent? resBody = this.GetContent(url, HttpMethod.Get, null, ignoreNotFound);
            var bodyTask = resBody?.ReadAsByteArrayAsync()!;
            return bodyTask?.Result;
        }

        public string? PostData(string url, HttpContent? content, bool ignoreNotFound = false)
        {
            HttpContent? resBody = this.GetContent(url, HttpMethod.Post, content, ignoreNotFound);
            var bodyTask = resBody?.ReadAsStringAsync()!.Result;
            return bodyTask;
        }

        public string? PutData(string url, HttpContent? content, bool ignoreNotFound = false)
        {
            HttpContent? resBody = this.GetContent(url, HttpMethod.Put, content, ignoreNotFound);
            var bodyTask = resBody?.ReadAsStringAsync()!.Result;
            return bodyTask;
        }

        public string? GetDataString(string url, bool ignoreNotFound = false)
        {
            HttpContent? resBody = this.GetContent(url, HttpMethod.Get, null, ignoreNotFound);
            var bodyTask = resBody?.ReadAsStringAsync()!.Result;
            return bodyTask;
        }

        public T? GetData<T>(string url, bool ignoreNotFound = false)
        {
            HttpContent? content = this.GetContent(url, HttpMethod.Get, null, ignoreNotFound);
            var bodyTask = content?.ReadAsAsync<T>()!;
            return bodyTask == null ? default : bodyTask.Result;
        }
    }
}
