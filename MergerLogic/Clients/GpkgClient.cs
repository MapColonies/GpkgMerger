using MergerLogic.Batching;
using MergerLogic.Extensions;
using MergerLogic.Utils;
using Microsoft.Extensions.Logging;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO.Abstractions;
using System.Reflection;
using System.Text;

namespace MergerLogic.Clients
{
    public class GpkgClient : DataUtils, IGpkgClient
    {
        private readonly string _tileCache;

        private readonly ITimeUtils _timeUtils;
        private readonly ILogger _logger;
        private readonly IFileSystem _fileSystem;

        public GpkgClient(string path, ITimeUtils timeUtils, ILogger<GpkgClient> logger, IFileSystem fileSystem,
            IGeoUtils geoUtils) : base(path, geoUtils)
        {
            this._timeUtils = timeUtils;
            this._logger = logger;
            this._fileSystem = fileSystem;
            this._tileCache = this.InternalGetTileCache();
        }

        private string InternalGetTileCache()
        {
            if (!this.Exist())
            {
                return this._fileSystem.Path.GetFileNameWithoutExtension(this.path);
            }

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
                        extent.MinX = reader.GetDouble(0);
                        extent.MinY = reader.GetDouble(1);
                        extent.MaxX = reader.GetDouble(2);
                        extent.MaxY = reader.GetDouble(3);
                    }
                }
            }

            return extent;
        }

        public long GetTileCount()
        {
            long tileCount = 0;

            using (var connection = new SQLiteConnection($"Data Source={this.path}"))
            {
                connection.Open();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = $"SELECT count(*) FROM \"{this._tileCache}\"";
                    //TODO: can optimized by using command.ExecuteScalar
                    using (var reader = command.ExecuteReader(System.Data.CommandBehavior.SingleRow))
                    {
                        reader.Read();
                        tileCount = reader.GetInt64(0);
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

                    command.Parameters.AddWithValue("$minX", extent.MinX);
                    command.Parameters.AddWithValue("$minY", extent.MinY);
                    command.Parameters.AddWithValue("$maxX", extent.MaxX);
                    command.Parameters.AddWithValue("$maxY", extent.MaxY);

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
                    command.CommandText =
                        $"SELECT tile_data FROM \"{this._tileCache}\" WHERE zoom_level=$z AND tile_column=$x AND tile_row=$y LIMIT 1";
                    command.Parameters.AddWithValue("$z", z);
                    command.Parameters.AddWithValue("$x", x);
                    command.Parameters.AddWithValue("$y", y);

                    var blob = (byte[])command.ExecuteScalar();
                    if (blob == null)
                    {
                        return null;
                    }

                    tile = this.CreateTile(z, x, y, blob)!;
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
                    command.CommandText =
                        $"SELECT tile_row FROM \"{this._tileCache}\" where zoom_level=$z and tile_column=$x and tile_row=$y";
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

        public void InsertTiles(IEnumerable<Tile> tiles)
        {
            using (var connection = new SQLiteConnection($"Data Source={this.path}"))
            {
                connection.Open();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText =
                        $"REPLACE INTO \"{this._tileCache}\" (zoom_level, tile_column, tile_row, tile_data) VALUES ($z, $x, $y, $blob)";

                    using (var transaction = connection.BeginTransaction())
                    {
                        foreach (Tile tile in tiles)
                        {
                            byte[] tileBytes = tile.GetImageBytes();
                            SQLiteParameter blobParameter =
                                new SQLiteParameter("$blob", System.Data.DbType.Binary, tileBytes.Length);
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

        public List<Tile> GetBatch(int batchSize, long offset)
        {
            List<Tile> tiles = new List<Tile>();

            using (var connection = new SQLiteConnection($"Data Source={this.path}"))
            {
                connection.Open();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText =
                        $"SELECT zoom_level, tile_column, tile_row, tile_data FROM \"{this._tileCache}\" ORDER BY zoom_level ASC limit $limit offset $offset";
                    command.Parameters.AddWithValue("$limit", batchSize);
                    command.Parameters.AddWithValue("$offset", offset);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var z = reader.GetInt32(0);
                            var x = reader.GetInt32(1);
                            var y = reader.GetInt32(2);
                            var blob = (byte[])reader["tile_data"];

                            Tile tile = this.CreateTile(z, x, y, blob)!;
                            tiles.Add(tile);
                        }
                    }
                }
            }

            return tiles;
        }

        public Tile? GetLastTile(int[] coords, int currentTileZoom)
        {
            if (coords.Length < 2)
            {
                return null;
            }

            Tile? lastTile = null;
            using (var connection = new SQLiteConnection($"Data Source={this.path}"))
            {
                connection.Open();

                using (var command = connection.CreateCommand())
                {
                    // Build command
                    StringBuilder commandBuilder = new StringBuilder(
                        $"SELECT zoom_level, tile_column, tile_row, tile_data FROM \"{this._tileCache}\" where ");

                    int maxZoomLevel = currentTileZoom - 1;
                    int arrayIdx = 0;
                    for (int currentZoomLevel = maxZoomLevel; currentZoomLevel >= 0; currentZoomLevel--)
                    {
                        arrayIdx = currentZoomLevel << 1;
                        commandBuilder.AppendFormat("(zoom_level = {0} and tile_column = {1} and tile_row = {2})",
                            currentZoomLevel, coords[arrayIdx], coords[arrayIdx + 1]);
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
                        var blob = (byte[])reader["tile_data"];

                        lastTile = this.CreateTile(z, x, y, blob)!;
                    }
                }
            }

            return lastTile;
        }

        public void Vacuum()
        {
            Stopwatch stopWatch = Stopwatch.StartNew();

            this._logger.LogInformation($"[{MethodBase.GetCurrentMethod().Name}] Vacuuming GPKG {this.path}");
            using (var connection = new SQLiteConnection($"Data Source={this.path}"))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "vacuum";
                    command.ExecuteNonQuery();
                }
            }

            this._logger.LogInformation($"[{MethodBase.GetCurrentMethod().Name}] Done vacuuming GPKG");

            // Get the elapsed time as a TimeSpan value.
            TimeSpan ts = stopWatch.Elapsed;

            string elapsedMessage = this._timeUtils.FormatElapsedTime($"[{MethodBase.GetCurrentMethod().Name}] Vacuum runtime", ts);
            this._logger.LogInformation(elapsedMessage);
        }

        public void Create(Extent extent, bool isOneXOne = false)
        {
            this._logger.LogInformation($"[{MethodBase.GetCurrentMethod().Name}] creating new gpkg: {this.path}");

            // Create hierarchy if needed
            string dir = this._fileSystem.Path.GetDirectoryName(this.path);
            if (dir.Length > 0 && !this._fileSystem.Directory.Exists(dir))
            {
                this._fileSystem.Directory.CreateDirectory(dir);
            }

            SQLiteConnection.CreateFile(this.path);
            using (var connection = new SQLiteConnection($"Data Source={this.path}"))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    this.SetPragma(connection);
                    this.CreateSpatialRefTable(connection);
                    this.CreateContentsTable(connection);
                    this.CreateGeometryColumnsTable(connection);
                    this.CreateTileMatrixSetTable(connection);
                    this.CreateTileMatrixTable(connection);
                    this.CreateExtentionTable(connection);
                    this.CreateTileTable(connection);
                    if (isOneXOne)
                    {
                        this.Add1X1MatrixSet(connection);
                    }
                    else
                    {
                        this.Add2X1MatrixSet(connection);
                    }

                    this.CreateGpkgContentsTable(connection, extent);
                    this.CreateTileMatrixValidationTriggers(connection);
                    transaction.Commit();
                }
            }
            // Vacuum is required if page size pragma is changed
            //Vacuum();
        }

        private void CreateSpatialRefTable(SQLiteConnection connection)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "CREATE TABLE \"gpkg_spatial_ref_sys\" (" +
                                      "\"srs_name\" TEXT NOT NULL," +
                                      "\"srs_id\" INTEGER NOT NULL," +
                                      "\"organization\" TEXT NOT NULL," +
                                      "\"organization_coordsys_id\" INTEGER NOT NULL," +
                                      "\"definition\" TEXT NOT NULL," +
                                      "\"description\" TEXT," +
                                      "PRIMARY KEY(\"srs_id\"));";
                command.ExecuteNonQuery();
            }

            using (var command = connection.CreateCommand())
            {
                command.CommandText = "INSERT INTO \"gpkg_spatial_ref_sys\" VALUES " +
                                      "('Undefined cartesian SRS',-1,'NONE',-1,'undefined','undefined cartesian coordinate reference system')," +
                                      "('Undefined geographic SRS',0,'NONE',0,'undefined','undefined geographic coordinate reference system')," +
                                      "('WGS 84 geodetic',4326,'EPSG',4326,'GEOGCS[\"WGS 84\",DATUM[\"WGS_1984\",SPHEROID[\"WGS 84\",6378137,298.257223563,AUTHORITY[\"EPSG\",\"7030\"]]," +
                                      "AUTHORITY[\"EPSG\",\"6326\"]],PRIMEM[\"Greenwich\",0,AUTHORITY[\"EPSG\",\"8901\"]],UNIT[\"degree\",0.0174532925199433,AUTHORITY[\"EPSG\",\"9122\"]]," +
                                      "AUTHORITY[\"EPSG\",\"4326\"]]','longitude/latitude coordinates in decimal degrees on the WGS 84 spheroid');";
                command.ExecuteNonQuery();
            }
        }

        private void CreateContentsTable(SQLiteConnection connection)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "CREATE TABLE \"gpkg_contents\" (" +
                                      "\"table_name\" TEXT NOT NULL," +
                                      "\"data_type\" TEXT NOT NULL," +
                                      "\"identifier\" TEXT UNIQUE," +
                                      "\"description\" TEXT DEFAULT ''," +
                                      "\"last_change\" DATETIME NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))," +
                                      "\"min_x\" DOUBLE," +
                                      "\"min_y\" DOUBLE," +
                                      "\"max_x\" DOUBLE," +
                                      "\"max_y\" DOUBLE," +
                                      "\"srs_id\"	INTEGER," +
                                      "CONSTRAINT \"fk_gc_r_srs_id\" FOREIGN KEY(\"srs_id\") REFERENCES \"gpkg_spatial_ref_sys\"(\"srs_id\")," +
                                      "PRIMARY KEY(\"table_name\"));";
                command.ExecuteNonQuery();
            }
        }

        private void CreateGeometryColumnsTable(SQLiteConnection connection)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "CREATE TABLE \"gpkg_geometry_columns\" (" +
                                      "\"table_name\" TEXT NOT NULL," +
                                      "\"column_name\" TEXT NOT NULL," +
                                      "\"geometry_type_name\" TEXT NOT NULL," +
                                      "\"srs_id\" INTEGER NOT NULL," +
                                      "\"z\" TINYINT NOT NULL," +
                                      "\"m\" TINYINT NOT NULL," +
                                      "CONSTRAINT \"pk_geom_cols\" PRIMARY KEY(\"table_name\",\"column_name\")," +
                                      "CONSTRAINT \"fk_gc_srs\" FOREIGN KEY(\"srs_id\") REFERENCES \"gpkg_spatial_ref_sys\"(\"srs_id\")," +
                                      "CONSTRAINT \"fk_gc_tn\" FOREIGN KEY(\"table_name\") REFERENCES \"gpkg_contents\"(\"table_name\"));";
                command.ExecuteNonQuery();
            }
        }

        private void CreateTileMatrixSetTable(SQLiteConnection connection)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "CREATE TABLE \"gpkg_tile_matrix_set\" (" +
                                      "\"table_name\" TEXT NOT NULL," +
                                      "\"srs_id\" INTEGER NOT NULL," +
                                      "\"min_x\" DOUBLE NOT NULL," +
                                      "\"min_y\" DOUBLE NOT NULL," +
                                      "\"max_x\" DOUBLE NOT NULL," +
                                      "\"max_y\" DOUBLE NOT NULL," +
                                      "PRIMARY KEY(\"table_name\")," +
                                      "CONSTRAINT \"fk_gtms_srs\" FOREIGN KEY(\"srs_id\") REFERENCES \"gpkg_spatial_ref_sys\"(\"srs_id\")," +
                                      "CONSTRAINT \"fk_gtms_table_name\" FOREIGN KEY(\"table_name\") REFERENCES \"gpkg_contents\"(\"table_name\"));";
                command.ExecuteNonQuery();
            }
        }

        private void CreateTileMatrixTable(SQLiteConnection connection)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "CREATE TABLE \"gpkg_tile_matrix\" (" +
                                      "\"table_name\" TEXT NOT NULL," +
                                      "\"zoom_level\" INTEGER NOT NULL," +
                                      "\"matrix_width\" INTEGER NOT NULL," +
                                      "\"matrix_height\" INTEGER NOT NULL," +
                                      "\"tile_width\" INTEGER NOT NULL," +
                                      "\"tile_height\" INTEGER NOT NULL," +
                                      "\"pixel_x_size\" DOUBLE NOT NULL," +
                                      "\"pixel_y_size\" DOUBLE NOT NULL," +
                                      "CONSTRAINT \"pk_ttm\" PRIMARY KEY(\"table_name\",\"zoom_level\")," +
                                      "CONSTRAINT \"fk_tmm_table_name\" FOREIGN KEY(\"table_name\") REFERENCES \"gpkg_contents\"(\"table_name\"));";
                command.ExecuteNonQuery();
            }
        }

        private void CreateExtentionTable(SQLiteConnection connection)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "CREATE TABLE \"gpkg_extensions\" (" +
                                      "\"table_name\" TEXT," +
                                      "\"column_name\" TEXT," +
                                      "\"extension_name\" TEXT NOT NULL," +
                                      "\"definition\"TEXT NOT NULL," +
                                      "\"scope\" TEXT NOT NULL," +
                                      "CONSTRAINT \"ge_tce\" UNIQUE(\"table_name\",\"column_name\",\"extension_name\"));";
                command.ExecuteNonQuery();
            }
        }

        private void SetPragma(SQLiteConnection connection)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA application_id = 1196444487; " // gpkg v1.2 +
                                                                             //command.CommandText = "PRAGMA application_id = 1196437808; " // gpkg v1.0 or 1.1
                                      + "PRAGMA user_version = 10201; "; // gpkg version number in the form MMmmPP (MM = major version, mm = minor version, PP = patch). aka 10000 is 1.0.0
                // + "PRAGMA page_size = 1024; "; //set sqlite page size, must be power of 2. current default is 4096 - changing the default requires vacuum
                command.ExecuteNonQuery();
            }
        }

        private void CreateSqureGrid(SQLiteConnection connection, int minZoom, int maxZoom, int baseWidth,
            int baseHeight, int yAxisSizeDeg, int zoomMultipiler, int tileSize)
        {
            StringBuilder gridBuilder = new StringBuilder("INSERT OR REPLACE INTO \"gpkg_tile_matrix\" VALUES ");

            int startZoomMultiplier = (int)Math.Pow(zoomMultipiler, minZoom);
            int width = baseWidth * startZoomMultiplier;
            int height = baseHeight * startZoomMultiplier;
            double res = (double)yAxisSizeDeg / height / 256;
            for (int z = minZoom; z <= maxZoom; z++)
            {
                gridBuilder.Append($"('{this._tileCache}',{z},{width},{height},{tileSize},{tileSize},{res},{res}),");
                width *= zoomMultipiler;
                height *= zoomMultipiler;
                res /= zoomMultipiler;
            }

            gridBuilder.Remove(gridBuilder.Length - 1, 1);
            gridBuilder.Append(";");
            using (var command = connection.CreateCommand())
            {
                command.CommandText = gridBuilder.ToString();
                command.ExecuteNonQuery();
            }
        }


        private void Add2X1MatrixSet(SQLiteConnection connection)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "INSERT INTO \"gpkg_tile_matrix_set\" VALUES " +
                                      $"('{this._tileCache}',{Utils.GeoUtils.SRID},-180,-90,180,90);";
                command.ExecuteNonQuery();
            }
        }

        private void Add1X1MatrixSet(SQLiteConnection connection)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "INSERT INTO \"gpkg_tile_matrix_set\" VALUES " +
                                      $"('{this._tileCache}',{Utils.GeoUtils.SRID},-180,-180,180,180);";
                command.ExecuteNonQuery();
            }
        }

        private void CreateTileTable(SQLiteConnection connection)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = $"CREATE TABLE \"{this._tileCache}\" (" +
                                      "\"id\" INTEGER," +
                                      "\"zoom_level\" INTEGER NOT NULL," +
                                      "\"tile_column\" INTEGER NOT NULL," +
                                      "\"tile_row\" INTEGER NOT NULL," +
                                      "\"tile_data\" BLOB NOT NULL," +
                                      "UNIQUE(\"zoom_level\",\"tile_column\",\"tile_row\")," +
                                      "PRIMARY KEY(\"id\" AUTOINCREMENT));";
                command.ExecuteNonQuery();
            }
        }

        private void CreateGpkgContentsTable(SQLiteConnection connection, Extent extent)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "INSERT INTO \"gpkg_contents\" " +
                                      "(\"table_name\",\"data_type\",\"identifier\",\"min_x\",\"min_y\",\"max_x\",\"max_y\",\"srs_id\") VALUES " +
                                      $"('{this._tileCache}','tiles','{this._tileCache}',{extent.MinX},{extent.MinY},{extent.MaxX},{extent.MaxY},{Utils.GeoUtils.SRID});";
                command.ExecuteNonQuery();
            }
        }

        private void CreateTileMatrixValidationTriggers(SQLiteConnection connection)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    "CREATE TRIGGER 'gpkg_tile_matrix_zoom_level_insert' BEFORE INSERT ON 'gpkg_tile_matrix' FOR EACH ROW BEGIN SELECT RAISE(ABORT, 'insert on table ''gpkg_tile_matrix'' violates constraint: zoom_level cannot be less than 0') WHERE (NEW.zoom_level < 0); END;" +
                    "CREATE TRIGGER 'gpkg_tile_matrix_zoom_level_update' BEFORE UPDATE of zoom_level ON 'gpkg_tile_matrix' FOR EACH ROW BEGIN SELECT RAISE(ABORT, 'update on table ''gpkg_tile_matrix'' violates constraint: zoom_level cannot be less than 0') WHERE(NEW.zoom_level < 0); END; " +
                    "CREATE TRIGGER 'gpkg_tile_matrix_matrix_width_insert' BEFORE INSERT ON 'gpkg_tile_matrix' FOR EACH ROW BEGIN SELECT RAISE(ABORT, 'insert on table ''gpkg_tile_matrix'' violates constraint: matrix_width cannot be less than 1') WHERE(NEW.matrix_width < 1); END; " +
                    "CREATE TRIGGER 'gpkg_tile_matrix_matrix_width_update' BEFORE UPDATE OF matrix_width ON 'gpkg_tile_matrix' FOR EACH ROW BEGIN SELECT RAISE(ABORT, 'update on table ''gpkg_tile_matrix'' violates constraint: matrix_width cannot be less than 1') WHERE(NEW.matrix_width < 1); END; " +
                    "CREATE TRIGGER 'gpkg_tile_matrix_matrix_height_insert' BEFORE INSERT ON 'gpkg_tile_matrix' FOR EACH ROW BEGIN SELECT RAISE(ABORT, 'insert on table ''gpkg_tile_matrix'' violates constraint: matrix_height cannot be less than 1') WHERE(NEW.matrix_height < 1); END; " +
                    "CREATE TRIGGER 'gpkg_tile_matrix_matrix_height_update' BEFORE UPDATE OF matrix_height ON 'gpkg_tile_matrix' FOR EACH ROW BEGIN SELECT RAISE(ABORT, 'update on table ''gpkg_tile_matrix'' violates constraint: matrix_height cannot be less than 1') WHERE(NEW.matrix_height < 1); END; " +
                    "CREATE TRIGGER 'gpkg_tile_matrix_pixel_x_size_insert' BEFORE INSERT ON 'gpkg_tile_matrix' FOR EACH ROW BEGIN SELECT RAISE(ABORT, 'insert on table ''gpkg_tile_matrix'' violates constraint: pixel_x_size must be greater than 0') WHERE NOT(NEW.pixel_x_size > 0); END; " +
                    "CREATE TRIGGER 'gpkg_tile_matrix_pixel_x_size_update' BEFORE UPDATE OF pixel_x_size ON 'gpkg_tile_matrix' FOR EACH ROW BEGIN SELECT RAISE(ABORT, 'update on table ''gpkg_tile_matrix'' violates constraint: pixel_x_size must be greater than 0') WHERE NOT(NEW.pixel_x_size > 0); END; " +
                    "CREATE TRIGGER 'gpkg_tile_matrix_pixel_y_size_insert' BEFORE INSERT ON 'gpkg_tile_matrix' FOR EACH ROW BEGIN SELECT RAISE(ABORT, 'insert on table ''gpkg_tile_matrix'' violates constraint: pixel_y_size must be greater than 0') WHERE NOT(NEW.pixel_y_size > 0); END; " +
                    "CREATE TRIGGER 'gpkg_tile_matrix_pixel_y_size_update' BEFORE UPDATE OF pixel_y_size ON 'gpkg_tile_matrix' FOR EACH ROW BEGIN SELECT RAISE(ABORT, 'update on table ''gpkg_tile_matrix'' violates constraint: pixel_y_size must be greater than 0') WHERE NOT(NEW.pixel_y_size > 0); END; ";
                command.ExecuteNonQuery();
            }
        }

        public void CreateTileCacheValidationTriggers()
        {
            using (var connection = new SQLiteConnection($"Data Source={this.path}"))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText =
                        $"CREATE TRIGGER \"{this._tileCache}_tile_column_insert\" BEFORE INSERT ON \"{this._tileCache}\" " +
                        "FOR EACH ROW BEGIN " +
                        $"SELECT RAISE(ABORT, 'insert on table ''{this._tileCache}'' violates constraint: tile_column cannot be < 0') " +
                        "WHERE(NEW.tile_column < 0); " +
                        $"SELECT RAISE(ABORT, 'insert on table ''{this._tileCache}'' violates constraint: tile_column must by < matrix_width specified for table and zoom level in gpkg_tile_matrix') " +
                        $"WHERE NOT(NEW.tile_column<(SELECT matrix_width FROM gpkg_tile_matrix WHERE lower(table_name) = lower('{this._tileCache}') AND zoom_level = NEW.zoom_level)); END; " +
                        $"CREATE TRIGGER \"{this._tileCache}_tile_column_update\" BEFORE UPDATE OF tile_column ON \"{this._tileCache}\" " +
                        "FOR EACH ROW BEGIN " +
                        $"SELECT RAISE(ABORT, 'update on table ''{this._tileCache}'' violates constraint: tile_column cannot be < 0') " +
                        "WHERE(NEW.tile_column < 0); " +
                        $"SELECT RAISE(ABORT, 'update on table ''{this._tileCache}'' violates constraint: tile_column must by < matrix_width specified for table and zoom level in gpkg_tile_matrix') " +
                        $"WHERE NOT(NEW.tile_column<(SELECT matrix_width FROM gpkg_tile_matrix WHERE lower(table_name) = lower('{this._tileCache}') AND zoom_level = NEW.zoom_level)); END; " +
                        $"CREATE TRIGGER \"{this._tileCache}_tile_row_insert\" BEFORE INSERT ON \"{this._tileCache}\" " +
                        "FOR EACH ROW BEGIN " +
                        $"SELECT RAISE(ABORT, 'insert on table ''{this._tileCache}'' violates constraint: tile_row cannot be < 0') " +
                        "WHERE(NEW.tile_row < 0); " +
                        $"SELECT RAISE(ABORT, 'insert on table ''{this._tileCache}'' violates constraint: tile_row must by < matrix_height specified for table and zoom level in gpkg_tile_matrix') " +
                        $"WHERE NOT(NEW.tile_row<(SELECT matrix_height FROM gpkg_tile_matrix WHERE lower(table_name) = lower('{this._tileCache}') AND zoom_level = NEW.zoom_level)); END; " +
                        $"CREATE TRIGGER \"{this._tileCache}_tile_row_update\" BEFORE UPDATE OF tile_row ON \"{this._tileCache}\" " +
                        "FOR EACH ROW BEGIN " +
                        $"SELECT RAISE(ABORT, 'update on table ''{this._tileCache}'' violates constraint: tile_row cannot be < 0') " +
                        "WHERE(NEW.tile_row < 0); " +
                        $"SELECT RAISE(ABORT, 'update on table ''{this._tileCache}'' violates constraint: tile_row must by < matrix_height specified for table and zoom level in gpkg_tile_matrix') " +
                        $"WHERE NOT(NEW.tile_row<(SELECT matrix_height FROM gpkg_tile_matrix WHERE lower(table_name) = lower('{this._tileCache}') AND zoom_level = NEW.zoom_level)); END; " +
                        $"CREATE TRIGGER \"{this._tileCache}_zoom_insert\" BEFORE INSERT ON \"{this._tileCache}\" " +
                        "FOR EACH ROW BEGIN " +
                        "SELECT RAISE(ABORT, 'insert on table ''{this._tileCache}'' violates constraint: zoom_level not specified for table in gpkg_tile_matrix') " +
                        $"WHERE NOT(NEW.zoom_level IN (SELECT zoom_level FROM gpkg_tile_matrix WHERE lower(table_name) = lower('{this._tileCache}'))) ; END; " +
                        $"CREATE TRIGGER \"{this._tileCache}_zoom_update\" BEFORE UPDATE OF zoom_level ON \"{this._tileCache}\" " +
                        "FOR EACH ROW BEGIN " +
                        $"SELECT RAISE(ABORT, 'update on table ''{this._tileCache}'' violates constraint: zoom_level not specified for table in gpkg_tile_matrix') " +
                        $"WHERE NOT (NEW.zoom_level IN (SELECT zoom_level FROM gpkg_tile_matrix WHERE lower(table_name) = lower('{this._tileCache}'))) ; END; ";
                    command.ExecuteNonQuery();
                }
            }
        }

        public void DeleteTileTableTriggers()
        {
            using (var connection = new SQLiteConnection($"Data Source={this.path}"))
            {
                var cmdBuilder = new StringBuilder();
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText =
                        $"select name from sqlite_master where type = 'trigger' and tbl_name = '{this._tileCache}';";
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string trigger = reader.GetString(0);
                            cmdBuilder.Append($"DROP TRIGGER IF EXISTS \"{trigger}\"; ");
                        }
                    }
                }

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = cmdBuilder.ToString();
                    command.ExecuteNonQuery();
                }
            }
        }

        public bool Exist()
        {
            // Get full path to gpkg file
            string fullPath = this._fileSystem.Path.GetFullPath(this.path);
            return this._fileSystem.File.Exists(fullPath);
        }

        public void UpdateTileMatrixTable(bool isOneXOne = false)
        {
            using (var connection = new SQLiteConnection($"Data Source={this.path}"))
            {
                int maxZoom;
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = $"SELECT MAX(zoom_level) AS maxZoom FROM \'{this._tileCache}\';";
                    var res = command.ExecuteScalar();
                    maxZoom = res != DBNull.Value ? int.Parse(res.ToString()) : 0;
                }

                if (isOneXOne)
                {
                    this.CreateSqureGrid(connection, 0, maxZoom, 1, 1, 360, 2, 256); //creates 1X1 grid
                }
                else
                {
                    this.CreateSqureGrid(connection, 0, maxZoom, 2, 1, 180, 2, 256); //creates 2X1 grid
                }
            }
        }

        public override bool IsValidGrid(bool isOneXOne = false)
        {
            using (var connection = new SQLiteConnection($"Data Source={this.path}"))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    string maxY = isOneXOne ? "180" : "90";
                    command.CommandText = "SELECT count(*) FROM gpkg_tile_matrix_set " +
                                          $"WHERE table_name = '{this._tileCache}' AND srs_id = {Utils.GeoUtils.SRID} " +
                                          "AND min_x = -180 AND max_x = 180 " +
                                          $"AND min_y = -{maxY} AND max_y = {maxY}";
                    long count = (long)command.ExecuteScalar();
                    var isValid = count == 1;
                    if (!isValid)
                    {
                        this._logger.LogWarning($"[{MethodBase.GetCurrentMethod().Name}] gpkg {this.path} has failed grid tile matrix set validation");
                        return false;
                    }
                }

                using (var command = connection.CreateCommand())
                {
                    command.CommandText =
                        "SELECT zoom_level, matrix_width, matrix_height, tile_width, tile_height, pixel_x_size, pixel_y_size " +
                        "FROM gpkg_tile_matrix " +
                        $"WHERE table_name = '{this._tileCache}' ORDER BY zoom_level ASC";
                    using (var reader = command.ExecuteReader())
                    {
                        const int tileSize = 256;
                        const double doublePrecession = 1e-10;
                        int zoom = 0;
                        int yAxisSizeDeg = isOneXOne ? 360 : 180;
                        double res = (double)yAxisSizeDeg / 256;
                        int yTiles = 1;
                        int xTiles = isOneXOne ? 1 : 2;
                        while (reader.Read())
                        {
                            int rowZoom = reader.GetInt32(0);
                            while (zoom < rowZoom)
                            {
                                res = res / 2;
                                yTiles <<= 1;
                                xTiles <<= 1;
                                zoom++;
                            }

                            if (reader.GetInt32(1) != xTiles || reader.GetInt32(2) != yTiles ||
                                reader.GetInt32(3) != tileSize || reader.GetInt32(4) != tileSize ||
                                !reader.GetDouble(5).IsApproximatelyEqualTo(res, doublePrecession) ||
                                !reader.GetDouble(6).IsApproximatelyEqualTo(res, doublePrecession))
                            {
                                this._logger.LogWarning($"[{MethodBase.GetCurrentMethod().Name}] gpkg {this.path} has failed grid validation for zoom {rowZoom}");
                                return false;
                            }
                        }
                    }
                }
            }

            return true;
        }
    }
}
