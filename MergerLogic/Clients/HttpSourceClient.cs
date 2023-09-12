using MergerLogic.Batching;
using MergerLogic.ImageProcessing;
using MergerLogic.Utils;

namespace MergerLogic.Clients
{
    public class HttpSourceClient : DataUtils, IHttpSourceClient
    {
        private IHttpRequestUtils _httpClient;
        private IPathPatternUtils _pathPatternUtils;

        public HttpSourceClient(string path, IHttpRequestUtils httpClient, IPathPatternUtils pathPatternUtils,
            IGeoUtils geoUtils, IImageFormatter formatter) : base(path, geoUtils, formatter)
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

            var format = base.Formatter.GetTileFormat(data);
            return new Tile(z, x, y, data, format);
        }

        public override bool TileExists(int z, int x, int y)
        {
            return this.GetTile(z, x, y) is not null;
        }
    }
}
