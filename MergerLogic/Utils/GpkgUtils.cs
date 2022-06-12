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

        public int GetTileCount()
        {
            int tileCount = 0;

            using (var connection = new SQLiteConnection($"Data Source={this.path}"))
            {
                connection.Open();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = $"SELECT count(*) FROM {this._tileCache}";

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

        public void InsertTiles(IEnumerable<Tile> tiles)
        {
            using (var connection = new SQLiteConnection($"Data Source={this.path}"))
            {
                connection.Open();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = $"REPLACE INTO {this._tileCache} (zoom_level, tile_column, tile_row, tile_data) VALUES ($z, $x, $y, $blob)";

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

        public List<Tile> GetBatch(int batchSize, int offset)
        {
            List<Tile> tiles = new List<Tile>();

            using (var connection = new SQLiteConnection($"Data Source={this.path}"))
            {
                connection.Open();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = $"SELECT zoom_level, tile_column, tile_row, hex(tile_data), length(hex(tile_data)) as blob_size FROM {this._tileCache} limit $limit offset $offset";
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

        public Tile GetLastTile(int[] coords, Coord baseCoords)
        {
            Tile lastTile = null;
            using (var connection = new SQLiteConnection($"Data Source={this.path}"))
            {
                connection.Open();

                using (var command = connection.CreateCommand())
                {

                    // Build command
                    StringBuilder commandBuilder = new StringBuilder($"SELECT zoom_level, tile_column, tile_row, hex(tile_data), length(hex(tile_data)) as blob_size FROM {this._tileCache} where ");

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

        public void CreateTileIndex()
        {
            using (var connection = new SQLiteConnection($"Data Source={this.path}"))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = $"CREATE UNIQUE INDEX IF NOT EXISTS index_tiles on {this._tileCache} (zoom_level, tile_row, tile_column)";
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

        public void Create(Extent extent,int maxZoom)
        {
            SQLiteConnection.CreateFile(this.path);
            using (var connection = new SQLiteConnection($"Data Source={this.path}"))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    SetPragma(connection);
                    CreateSpatialRefTable(connection);
                    CreateContentsTable(connection);
                    CreateGeometryColumnsTable(connection);
                    CreateTileMatrixSetTable(connection);
                    CreateTileMatrixTable(connection);
                    CreateExtentionTable(connection);
                    CreateTileTable(connection,extent);
                    Add2X1Data(connection, maxZoom);
                    CreateTileMatrixValidationTriggers(connection);
                    transaction.Commit();
                }
            }
            // Vacuum is required is page size pragma is changed
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

        private void CreateSqureGrid(SQLiteConnection connection, int maxZoom, int baseWidth, int baseHeight, double baseRes, int zoomMultipiler, int tileSize)
        {
            StringBuilder gridBuilder = new StringBuilder("INSERT INTO \"gpkg_tile_matrix\" VALUES ");
            
            int width = baseWidth;
            int height = baseHeight;
            double res = baseRes;
            for (int z = 0; z <= maxZoom; z++)
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

        private void Add2X1Data(SQLiteConnection connection, int maxZoom)
        {
            //TODO: add support for 1x1? (copy this function and change grid bbox and base zoom parameters)
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "INSERT INTO \"gpkg_tile_matrix_set\" VALUES " +
                    $"({this._tileCache},4326,-180,-90,180,90);";
                command.ExecuteNonQuery();
            }
            CreateSqureGrid(connection, maxZoom,1,2, 0.703125,2,256);//creates 2X1 grid
        }

        private void CreateTileTable(SQLiteConnection connection, Extent extent)
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
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "INSERT INTO \"gpkg_contents\" " +
                    "(\"table_name\",\"data_type\",\"identifier\",\"min_x\",\"min_y\",\"max_x\",\"max_y\",\"srs_id\") VALUES " +
                    $"({this._tileCache},'tiles',{this._tileCache},${extent.minX},${extent.minY},{extent.maxX},{extent.maxY},4326);";
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
                                $"SELECT RAISE(ABORT, 'update on table ''{_tileCache}'' violates constraint: zoom_level not specified for table in gpkg_tile_matrix') " +
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
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = $"select name from sqlite_master where type = 'trigger' and tbl_name = '{this._tileCache}';";
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string trigger = reader.GetString(0);
                            cmdBuilder.Append("DROP TRIGGER IF EXISTS \"{this.trigger}\"; ");
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
    }
}
