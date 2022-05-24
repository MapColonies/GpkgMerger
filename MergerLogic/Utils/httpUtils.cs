using MergerLogic.Batching;

namespace MergerLogic.Utils
{
    internal class httpUtils : DataUtils
    {
        private PathPatternUtils _pathPatternUtils;
        private HttpClient _httpClient;
        public httpUtils(string path, PathPatternUtils pathPatternUtils) : base(path)
        {
            this._pathPatternUtils = pathPatternUtils;
            this._httpClient = new HttpClient();
        }

        public override Tile GetTile(int z, int x, int y)
        {
            string url = this._pathPatternUtils.renderUrlTemplate(x, y, z);
            var resTask = this._httpClient.GetAsync(url);
            resTask.Wait();
            var httpRes = resTask.Result;
            if (httpRes.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
            var bodyTask = httpRes.Content.ReadAsByteArrayAsync();
            byte[] data = bodyTask.Result;
            return new Tile(z, x, y, data);

        }
    }
}
