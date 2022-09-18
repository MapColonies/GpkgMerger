using MergerLogic.Batching;
using MergerLogic.Utils;

namespace MergerLogic.Clients
{
    public class HttpSourceClient : DataUtils, IHttpSourceClient
    {
        private IHttpRequestUtils _httpClient;
        private IPathPatternUtils _pathPatternUtils;

        public HttpSourceClient(string path, IHttpRequestUtils httpClient, IPathPatternUtils pathPatternUtils,
            IGeoUtils geoUtils) : base(path, geoUtils, null)
        {
            this._httpClient = httpClient;
            this._pathPatternUtils = pathPatternUtils;
        }

        public override Tile? GetTile(int z, int x, int y)
        {
            string url = this._pathPatternUtils.RenderUrlTemplate(x, y, z);
            byte[]? data = this._httpClient.GetData(url, true);
            if (data is null)
            {
                return null;
            }

            return new Tile(z, x, y, data);
        }

        public override bool TileExists(int z, int x, int y)
        {
            return this.GetTile(z, x, y) is not null;
        }
    }
}
