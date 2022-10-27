using Amazon.S3;
using Amazon.S3.Model;
using MergerLogic.Batching;
using MergerLogic.DataTypes;
using MergerLogic.ImageProcessing;
using MergerLogic.Utils;

namespace MergerLogic.Clients
{
    public class S3Client : DataUtils, IS3Client
    {
        private readonly string _bucket;

        private readonly IAmazonS3 _client;
        private readonly IPathUtils _pathUtils;

        public S3Client(IAmazonS3 client, IPathUtils pathUtils, IGeoUtils geoUtils, IImageFormatter formatter,
            string bucket, string path) : base(path, geoUtils, formatter)
        {
            this._client = client;
            this._bucket = bucket;
            this._pathUtils = pathUtils;
        }

        private byte[]? GetImageBytes(string key)
        {
            try
            {
                var request = new GetObjectRequest() { BucketName = this._bucket, Key = key };
                var getObjectTask = this._client.GetObjectAsync(request);
                GetObjectResponse res = getObjectTask.Result;

                byte[] image;
                using (MemoryStream ms = new MemoryStream())
                {
                    using (var responseStream = res.ResponseStream)
                    {
                        responseStream.CopyTo(ms);
                    }

                    image = ms.ToArray();
                }

                return image;
            }
            catch (AggregateException)
            {
                return null;
            }
        }

        public override Tile? GetTile(int z, int x, int y)
        {
            var key = this.GetTileKey(z, x, y);
            if (key == null)
            {
                return null;
            }

            byte[]? imageBytes = this.GetImageBytes(key);
            return this.createTile(z, x, y, imageBytes);
        }

        public Tile? GetTile(string key)
        {
            Coord coords = this._pathUtils.FromPath(key, out TileFormat format, true);
            byte[]? imageBytes = this.GetImageBytes(key);
            if (imageBytes == null)
            {
                return null;
            }

            return new Tile(coords, imageBytes, format);
        }

        public Task<Tile?>[] GetImages(List<S3Object> s3Objects)
        {
            var tasks = s3Objects.Select(o => {
                string key = o.Key;
                Coord coords = this._pathUtils.FromPath(key, out TileFormat format, true);

                var request = new GetObjectRequest() { BucketName = this._bucket, Key = o.Key };
                var getObjectTask = this._client.GetObjectAsync(request);
                var toImageTask = getObjectTask.ContinueWith(t => {
                    byte[] image;
                    try {
                        using (MemoryStream ms = new MemoryStream())
                        {
                            t.Result.ResponseStream.CopyTo(ms);
                            image = ms.ToArray();
                        }
                        return new Tile(coords, image, format);
                    }
                    catch (AggregateException)
                    {
                        return null;
                    }
                });

                return toImageTask;
            }).ToArray();

            return tasks;

            // Task.WaitAll(tasks);

            // return tasks.Select(t => t.Result).ToList();
        }

        // public Tile? GetTile(List<S3Object> s3Objects)
        // {
        //     s3Objects.Select(o => {
        //         var request = new GetObjectRequest() { BucketName = this._bucket, Key = o.Key };
        //         // Task<GetObjectResponse> response = Task.Factory.FromAsync<GetObjectRequest, GetObjectResponse>(
        //         //     this._client.GetObjectAsync, this._client.WriteGetObjectResponseAsync, request, null
        //         // );

        //         Task<GetObjectResponse> response = new Task<GetObjectResponse>(() => this._client.GetObjectAsync(request));
        //         response.ContinueWith(t => {

        //         });
        //     });

        //     var request = new GetObjectRequest() { BucketName = this._bucket, Key = key };
        //     Task<GetObjectResponse> response = Task.Factory.FromAsync<GetObjectRequest, GetObjectResponse>(
        //         this._client.GetObjectAsync, this._client.WriteGetObjectResponseAsync,  
        //     );

        //     Coord coords = this._pathUtils.FromPath(key, out TileFormat format, true);
        //     byte[]? imageBytes = this.GetImageBytes(key);
        //     if (imageBytes == null)
        //     {
        //         return null;
        //     }

        //     return new Tile(coords, imageBytes, format);
        // }

        public override bool TileExists(int z, int x, int y)
        {
            return this.GetTileKey(z, x, y) != null;
        }

        public void UpdateTile(Tile tile)
        {
            string key = this._pathUtils.GetTilePath(this.path, tile, true);

            var request = new PutObjectRequest()
            {
                BucketName = this._bucket, CannedACL = S3CannedACL.PublicRead, Key = String.Format(key)
            };

            byte[] buffer = tile.GetImageBytes();
            using (var ms = new MemoryStream(buffer))
            {
                request.InputStream = ms;
                var task = this._client.PutObjectAsync(request);
                var res = task.Result;
            }
        }

        public void UpdateTiles(IEnumerable<Tile> tiles)
        {
            var tasks = tiles.Select(t => {
                string key = this._pathUtils.GetTilePath(this.path, t, true);

                var request = new PutObjectRequest()
                {
                    BucketName = this._bucket, CannedACL = S3CannedACL.PublicRead, Key = String.Format(key)
                };

                byte[] buffer = t.GetImageBytes();
                // using (var ms = new MemoryStream(buffer))
                // {
                    var ms = new MemoryStream(buffer);
                    request.InputStream = ms;
                    ms = null;
                    return this._client.PutObjectAsync(request);
                    // var res = task.Result;
                // }
            }).ToArray();
            Task.WaitAll(tasks);
        }

        // public void UpdateTiles(IEnumerable<Tile> tiles)
        // {
        //     tiles.ToList().ForEach(t => {
        //         string key = this._pathUtils.GetTilePath(this.path, t, true);

        //         var request = new PutObjectRequest()
        //         {
        //             BucketName = this._bucket, CannedACL = S3CannedACL.PublicRead, Key = String.Format(key)
        //         };

        //         byte[] buffer = t.GetImageBytes();
        //         using (var ms = new MemoryStream(buffer))
        //         {
        //             // var ms = new MemoryStream(buffer);
        //             request.InputStream = ms;
        //             // ms = null;
        //             var task = this._client.PutObjectAsync(request);
        //             var res = task.Result;
        //         }
        //     });
        //     // Task.WaitAll(tasks);
        // }

        private string? GetTileKey(int z, int x, int y)
        {
            string keyPrefix = this._pathUtils.GetTilePathWithoutExtension(this.path, z, x, y, true);
            var listRequests = new ListObjectsV2Request { BucketName = this._bucket, Prefix = keyPrefix, MaxKeys = 1 };

            var listObjectsTask = this._client.ListObjectsV2Async(listRequests);
            return listObjectsTask.Result.S3Objects.FirstOrDefault()?.Key;
        }
    }
}
