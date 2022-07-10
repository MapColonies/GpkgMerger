namespace MergerLogic.Utils
{
    public class HttpRequestUtils : IHttpRequestUtils, IDisposable
    {
        private HttpClient _httpClient;

        public HttpRequestUtils()
        {
            this._httpClient = new HttpClient();
        }

        ~HttpRequestUtils()
        {
            this._httpClient.Dispose();
        }

        private HttpContent? GetContent(string url)
        {
            var resTask = this._httpClient.GetAsync(url);
            resTask.Wait();

            var httpRes = resTask.Result;
            if (httpRes.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                Console.WriteLine($"Error, res: {httpRes}");
                return null;
            }
            else if (httpRes.StatusCode != System.Net.HttpStatusCode.OK)
            {
                throw new Exception($"Invalid response from {url}, status: {httpRes.StatusCode}.");
            }

            return httpRes.Content;
        }

        public byte[]? GetData(string url)
        {
            HttpContent? content = GetContent(url);
            Console.WriteLine($"content: {content}");
            var bodyTask = content?.ReadAsByteArrayAsync()!;
            Console.WriteLine($"result: {bodyTask.Result}");
            return bodyTask.Result;
        }

        public string GetDataString(string url)
        {
            HttpContent? content = GetContent(url);
            Console.WriteLine($"content: {content}");
            var bodyTask = content?.ReadAsStringAsync()!.Result;
            Console.WriteLine($"result: {bodyTask}");
            return bodyTask;
        }

        public T? GetData<T>(string url)
        {
            HttpContent? content = GetContent(url);
            Console.WriteLine($"content: {content}");
            var bodyTask = content?.ReadAsAsync<T>()!;
            Console.WriteLine($"result: {bodyTask.Result}");
            return bodyTask.Result;
        }

        public Task<HttpResponseMessage> GetAsync(string? requestUri)
        {
            return this._httpClient.GetAsync(requestUri);
        }

        public void Dispose()
        {
            if (this._httpClient != null)
            {
                this._httpClient.Dispose();
            }
        }
    }
}
