using MergerLogic.Batching;

namespace MergerLogic.Utils
{
    public class HttpUtils : DataUtils, IHttpUtils
    {
        private IPathPatternUtils _pathPatternUtils;
        private HttpClient _httpClient;
        public HttpUtils(string path, IPathPatternUtils pathPatternUtils, IGeoUtils geoUtils) : base(path,geoUtils)
        {
            this._pathPatternUtils = pathPatternUtils;
            this._httpClient = new HttpClient();
        }

        ~HttpUtils()
        {
            this._httpClient.Dispose();
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
    }
}
