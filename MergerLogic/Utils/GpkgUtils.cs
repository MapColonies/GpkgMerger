using MergerLogic.Batching;
using MergerLogic.DataTypes;
using System.Data.SQLite;
using System.Diagnostics;
using System.Text;

namespace MergerLogic.Utils
{
    public class GpkgUtils : DataUtils, IGpkgUtils
    {
        private string _tileCache;

        private ITimeUtils _timeUtils;

        public GpkgUtils(string path, ITimeUtils timeUtils) : base(path)
        {
            this._tileCache = this.GetTileCache();
            this._timeUtils = timeUtils;
        }

        public string GetTileCache()
        {
            string tileCache = "";

            using (var connection = new SQLiteConnection($"Data Source={this.path}"))
            {
                connection.Open();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT table_name FROM gpkg_contents";

                    using (var reader = command.ExecuteReader())
                    {
                        reader.Read();
                        tileCache = reader.GetString(0);
                    }
                }
            }

            return tileCache;
        }

        public Extent GetExtent()
        {
            Extent extent = new Extent();

            using (var connection = new SQLiteConnection($"Data Source={this.path}"))
            {
                connection.Open();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT min_x, min_y, max_x, max_y FROM gpkg_contents";

                    using (var reader = command.ExecuteReader(System.Data.CommandBehavior.SingleRow))
                    {
                        reader.Read();
                        extent.minX = reader.GetDouble(0);
                        extent.minY = reader.GetDouble(1);
                        extent.maxX = reader.GetDouble(2);
                        extent.maxY = reader.GetDouble(3);
                    }
                }
            }

            return extent;
        }

        public int GetTileCount(string tileCache)
        {
            int tileCount = 0;

            using (var connection = new SQLiteConnection($"Data Source={this.path}"))
            {
                connection.Open();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = $"SELECT count(*) FROM {tileCache}";

                    using (var reader = command.ExecuteReader(System.Data.CommandBehavior.SingleRow))
                    {
                        reader.Read();
                        tileCount = reader.GetInt32(0);
                    }
                }
            }

            return tileCount;
        }

        public void UpdateExtent(Extent extent)
        {
            using (var connection = new SQLiteConnection($"Data Source={this.path}"))
            {
                connection.Open();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "UPDATE gpkg_contents SET min_x=$minX, max_x=$maxX, min_y=$minY, max_y=$maxY";

                    command.Parameters.AddWithValue("$minX", extent.minX);
                    command.Parameters.AddWithValue("$minY", extent.minY);
                    command.Parameters.AddWithValue("$maxX", extent.maxX);
                    command.Parameters.AddWithValue("$maxY", extent.maxY);

                    command.ExecuteNonQuery();
                }
            }
        }

        public static void CopyTileMatrix(string target, string source, string tileCache)
        {
            using (var connection = new SQLiteConnection($"Data Source={source}"))
            {
                connection.Open();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM gpkg_tile_matrix";

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            TileMatrix tileMatrix = new TileMatrix();
                            tileMatrix.tableName = tileCache;
                            tileMatrix.zoomLevel = reader.GetInt32(1);
                            tileMatrix.matrixWidth = reader.GetInt32(2);
                            tileMatrix.matrixHeight = reader.GetInt32(3);
                            tileMatrix.tileWidth = reader.GetInt32(4);
                            tileMatrix.tileHeight = reader.GetInt32(5);
                            tileMatrix.pixleXSize = reader.GetDouble(6);
                            tileMatrix.pixleYSize = reader.GetDouble(7);

                            InsertTileMatrixRow(target, tileMatrix);
                        }
                    }
                }
            }
        }

        private static void InsertTileMatrixRow(string path, TileMatrix tileMatrix)
        {
            using (var connection = new SQLiteConnection($"Data Source={path}"))
            {
                connection.Open();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "REPLACE INTO gpkg_tile_matrix (table_name, zoom_level, matrix_width, matrix_height, tile_width, tile_height, pixel_x_size, pixel_y_size) VALUES ($tableName, $zoomLevel, $matrixWidth, $matrixHeight, $tileWidth, $tileHeight, $pixleXSize, $pixleYSize)";

                    command.Parameters.AddWithValue("$tableName", tileMatrix.tableName);
                    command.Parameters.AddWithValue("$zoomLevel", tileMatrix.zoomLevel);
                    command.Parameters.AddWithValue("$matrixWidth", tileMatrix.matrixWidth);
                    command.Parameters.AddWithValue("$matrixHeight", tileMatrix.matrixHeight);
                    command.Parameters.AddWithValue("$tileWidth", tileMatrix.tileWidth);
                    command.Parameters.AddWithValue("$tileHeight", tileMatrix.tileHeight);
                    command.Parameters.AddWithValue("$pixleXSize", tileMatrix.pixleXSize);
                    command.Parameters.AddWithValue("$pixleYSize", tileMatrix.pixleYSize);

                    command.ExecuteNonQuery();
                }
            }
        }

        public override Tile GetTile(int z, int x, int y)
        {
            Tile tile = null;

            using (var connection = new SQLiteConnection($"Data Source={this.path}"))
            {
                connection.Open();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = $"SELECT hex(tile_data) FROM {this._tileCache} where zoom_level=$z and tile_column=$x and tile_row=$y";
                    command.Parameters.AddWithValue("$z", z);
                    command.Parameters.AddWithValue("$x", x);
                    command.Parameters.AddWithValue("$y", y);

                    using (var reader = command.ExecuteReader(System.Data.CommandBehavior.SingleRow))
                    {
                        while (reader.Read())
                        {
                            var blob = reader.GetString(0);
                            tile = new BlobTile(z, x, y, blob, blob.Length);
                        }
                    }
                }
            }

            return tile;
        }

        public override bool TileExists(int z, int x, int y)
        {
            using (var connection = new SQLiteConnection($"Data Source={this.path}"))
            {
                connection.Open();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = $"SELECT tile_row FROM {this._tileCache} where zoom_level=$z and tile_column=$x and tile_row=$y";
                    command.Parameters.AddWithValue("$z", z);
                    command.Parameters.AddWithValue("$x", x);
                    command.Parameters.AddWithValue("$y", y);

                    using (var reader = command.ExecuteReader(System.Data.CommandBehavior.SingleRow))
                    {
                        // Check if a row was returned
                        if (reader.HasRows)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public void InsertTiles(string tileCache, IEnumerable<Tile> tiles)
        {
            using (var connection = new SQLiteConnection($"Data Source={this.path}"))
            {
                connection.Open();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = $"REPLACE INTO {tileCache} (zoom_level, tile_column, tile_row, tile_data) VALUES ($z, $x, $y, $blob)";

                    using (var transaction = connection.BeginTransaction())
                    {
                        foreach (Tile tile in tiles)
                        {
                            byte[] tileBytes = tile.GetImageBytes();
                            SQLiteParameter blobParameter = new SQLiteParameter("$blob", System.Data.DbType.Binary, tileBytes.Length);
                            blobParameter.Value = tileBytes;

                            command.Parameters.AddWithValue("$z", tile.Z);
                            command.Parameters.AddWithValue("$x", tile.X);
                            command.Parameters.AddWithValue("$y", tile.Y);
                            command.Parameters.Add(blobParameter);
                            command.ExecuteNonQuery();
                        }
                        transaction.Commit();
                    }
                }
            }
        }

        public List<Tile> GetBatch(int batchSize, int offset, string tileCache)
        {
            List<Tile> tiles = new List<Tile>();

            using (var connection = new SQLiteConnection($"Data Source={this.path}"))
            {
                connection.Open();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = $"SELECT zoom_level, tile_column, tile_row, hex(tile_data), length(hex(tile_data)) as blob_size FROM {tileCache} limit $limit offset $offset";
                    command.Parameters.AddWithValue("$limit", batchSize);
                    command.Parameters.AddWithValue("$offset", offset);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var z = reader.GetInt32(0);
                            var x = reader.GetInt32(1);
                            var y = reader.GetInt32(2);
                            var blob = reader.GetString(3);
                            var blobSize = reader.GetInt32(4);

                            Tile tile = new BlobTile(z, x, y, blob, blobSize);
                            tiles.Add(tile);
                        }
                    }
                }
            }
            return tiles;
        }

        public Tile GetLastTile(string tileCache, int[] coords, Coord baseCoords)
        {
            Tile lastTile = null;
            using (var connection = new SQLiteConnection($"Data Source={this.path}"))
            {
                connection.Open();

                using (var command = connection.CreateCommand())
                {

                    // Build command
                    StringBuilder commandBuilder = new StringBuilder($"SELECT zoom_level, tile_column, tile_row, hex(tile_data), length(hex(tile_data)) as blob_size FROM {tileCache} where ");

                    int zoomLevel = baseCoords.z;
                    int maxZoomLevel = zoomLevel - 1;
                    int arrayIdx = 0;
                    for (int currentZoomLevel = maxZoomLevel; currentZoomLevel >= 0; currentZoomLevel--)
                    {
                        arrayIdx = currentZoomLevel << 1;
                        commandBuilder.AppendFormat("(zoom_level = {0} and tile_column = {1} and tile_row = {2})", currentZoomLevel, coords[arrayIdx], coords[arrayIdx + 1]);
                        if (currentZoomLevel > 0)
                        {
                            commandBuilder.Append(" OR ");
                        }
                    }
                    commandBuilder.Append("order by zoom_level desc limit 1");

                    command.CommandText = commandBuilder.ToString();

                    using (var reader = command.ExecuteReader(System.Data.CommandBehavior.SingleRow))
                    {
                        bool exists = reader.Read();

                        if (!exists)
                        {
                            return null;
                        }

                        var z = reader.GetInt32(0);
                        var x = reader.GetInt32(1);
                        var y = reader.GetInt32(2);
                        var blob = reader.GetString(3);
                        var blobSize = reader.GetInt32(4);
                        lastTile = new BlobTile(z, x, y, blob, blobSize);
                    }
                }
            }
            return lastTile;
        }

        public void CreateTileIndex(string tileCache)
        {
            using (var connection = new SQLiteConnection($"Data Source={this.path}"))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = $"CREATE UNIQUE INDEX IF NOT EXISTS index_tiles on {tileCache} (zoom_level, tile_row, tile_column)";
                    command.ExecuteNonQuery();
                }
            }
        }

        public void Vacuum()
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            Console.WriteLine($"Vacuuming GPKG {this.path}");
            using (var connection = new SQLiteConnection($"Data Source={this.path}"))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "vacuum";
                    command.ExecuteNonQuery();
                }
            }
            Console.WriteLine("Done vacuuming GPKG");

            // Get the elapsed time as a TimeSpan value.
            TimeSpan ts = stopWatch.Elapsed;

            this._timeUtils.PrintElapsedTime("Vacuum runtime", ts);
        }
    }
}
