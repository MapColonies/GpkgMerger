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
            var bodyTask = content?.ReadAsByteArrayAsync()!;
            return bodyTask.Result;
        }

        public string GetDataString(string url)
        {
            HttpContent? content = GetContent(url);
            var bodyTask = content?.ReadAsStringAsync()!.Result;
            return bodyTask;
        }

        public T? GetData<T>(string url)
        {
            HttpContent? content = GetContent(url);
            var bodyTask = content?.ReadAsAsync<T>()!;
            return bodyTask.Result;
        }
    }
}
