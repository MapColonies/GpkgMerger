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

        private HttpContent? GetContent(HttpRequestMessage req)
        {
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

        public byte[]? GetData(string url)
        {
            HttpRequestMessage req = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri(url),
            };
            HttpContent? content = GetContent(req);
            var bodyTask = content?.ReadAsByteArrayAsync()!;
            return bodyTask.Result;
        }

        public string PostDataString(string url)
        {
            HttpRequestMessage req = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(url)
            };
            HttpContent? content = GetContent(req);
            var bodyTask = content?.ReadAsStringAsync()!.Result;
            return bodyTask;
        }

        public string PutDataString(string url, StringContent body)
        {
            HttpRequestMessage req = new HttpRequestMessage
            {
                Method = HttpMethod.Put,
                RequestUri = new Uri(url),
                Content = body
            };
            HttpContent? content = GetContent(req);
            var bodyTask = content?.ReadAsStringAsync()!.Result;
            return bodyTask;
        }

        public string GetDataString(string url)
        {
            HttpRequestMessage req = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri(url)
            };
            HttpContent? content = GetContent(req);
            var bodyTask = content?.ReadAsStringAsync()!.Result;
            return bodyTask;
        }

        public T? GetData<T>(string url)
        {
            HttpRequestMessage req = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri(url)
            };
            HttpContent? content = GetContent(req);
            var bodyTask = content?.ReadAsAsync<T>()!;
            return bodyTask.Result;
        }
    }
}
