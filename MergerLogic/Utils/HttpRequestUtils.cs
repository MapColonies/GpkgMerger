namespace MergerLogic.Utils
{
    public class HttpRequestUtils : IHttpRequestUtils
    {
        private HttpClient _httpClient;

        public HttpRequestUtils(HttpClient httpClient)
        {
            this._httpClient = httpClient;
        }

        ~HttpRequestUtils()
        {
            if (this._httpClient != null)
            {
                this._httpClient.Dispose();
            }
        }

        private HttpContent? GetContent(string url, HttpMethod method)
        {
            HttpRequestMessage req = new HttpRequestMessage
            {
                Method = method,
                RequestUri = new Uri(url)
            };

            var resTask = this._httpClient.SendAsync(req);
            resTask.Wait();

            var httpRes = resTask.Result;
            if (httpRes.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                Console.WriteLine($"Error, res: {httpRes}");
                return null;
            }
            else if (httpRes.StatusCode != System.Net.HttpStatusCode.OK)
            {
                throw new Exception($"Invalid response from {req.RequestUri}, status: {httpRes.StatusCode}.");
            }

            return httpRes.Content;
        }

        // private HttpContent? GetContent(string url)
        // {

        // }

        // private HttpContent? PostContent(string url)
        // {

        // }

        // private HttpContent? RetrieveContent(string url, Func<string, Task<HttpResponseMessage>> method)
        // {
        //     var resTask = method(url);
        //     resTask.Wait();

        //     var httpRes = resTask.Result;
        //     if (httpRes.StatusCode == System.Net.HttpStatusCode.NotFound)
        //     {
        //         Console.WriteLine($"Error, res: {httpRes}");
        //         return null;
        //     }
        //     else if (httpRes.StatusCode != System.Net.HttpStatusCode.OK)
        //     {
        //         throw new Exception($"Invalid response from {url}, status: {httpRes.StatusCode}.");
        //     }

        //     return httpRes.Content;
        // }

        public byte[]? GetData(string url)
        {
            HttpContent? content = GetContent(url, HttpMethod.Get);
            var bodyTask = content?.ReadAsByteArrayAsync()!;
            return bodyTask.Result;
        }

        public string PostDataString(string url)
        {
            HttpContent? content = GetContent(url, HttpMethod.Post);
            var bodyTask = content?.ReadAsStringAsync()!.Result;
            return bodyTask;
        }

        public string GetDataString(string url)
        {
            HttpContent? content = GetContent(url, HttpMethod.Get);
            var bodyTask = content?.ReadAsStringAsync()!.Result;
            return bodyTask;
        }

        public T? GetData<T>(string url)
        {
            HttpContent? content = GetContent(url, HttpMethod.Get);
            var bodyTask = content?.ReadAsAsync<T>()!;
            return bodyTask.Result;
        }
    }
}
