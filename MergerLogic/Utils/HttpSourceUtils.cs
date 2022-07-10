using MergerLogic.Batching;

namespace MergerLogic.Utils
{
    public class HttpSourceUtils : DataUtils, IHttpSourceUtils
    {
        private IHttpRequestUtils _httpClient;
        private IPathPatternUtils _pathPatternUtils;
        
        public HttpSourceUtils(IHttpRequestUtils httpClient, string path, IPathPatternUtils pathPatternUtils, IGeoUtils geoUtils) : base(path, geoUtils)
        {
            this._httpClient = httpClient;
            this._pathPatternUtils = pathPatternUtils;
        }

        public override Tile GetTile(int z, int x, int y)
        {
            string url = this._pathPatternUtils.RenderUrlTemplate(x, y, z);  
            var resTask = this._httpClient.GetAsync(url);
            resTask.Wait();
            var httpRes = resTask.Result;
            if (httpRes.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
            else if (httpRes.StatusCode != System.Net.HttpStatusCode.OK)
            {
                throw new Exception($"invalid response from http tile source. status: {httpRes.StatusCode}");
            }
            var bodyTask = httpRes.Content.ReadAsByteArrayAsync();
            bodyTask.Wait();
            byte[] data = bodyTask.Result;
            return new Tile(z, x, y, data);
        }

        public override bool TileExists(int z, int x, int y)
        {
            return this.GetTile(z, x, y) is not null;
        }

        ~HttpSourceUtils()
        {
            HttpRequestUtils httpRequestUtils = this._httpClient as HttpRequestUtils;
            if (httpRequestUtils != null && httpRequestUtils != null)
            {
                httpRequestUtils.Dispose();
            }
        }
    }
}
