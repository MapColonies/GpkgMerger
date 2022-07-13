using MergerLogic.Batching;
using MergerLogic.DataTypes;
using MergerLogic.Utils;
using MergerLogicUnitTests.testUtils;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO.Abstractions;
using System.Linq;

namespace MergerLogicUnitTests.Utils
{
    [TestClass]
    [TestCategory("unit")]
    [TestCategory("gpkg")]
    [TestCategory("gpkgUtils")]
    public class GpkgUtilsTest
    {
        #region mocks

        private MockRepository _repository;
        private Mock<IGeoUtils> _geoUtilsMock;
        private Mock<ITimeUtils> _timeUtilsMock;
        private Mock<ILogger<GpkgUtils>> _loggerMock;
        private Mock<IFileSystem> _fileSystemMock;
        private Mock<IPath> _pathMock;
        private Mock<IFile> _fileMock;

        #endregion

        [TestInitialize]
        public void BeforeEach()
        {
            this._repository = new MockRepository(MockBehavior.Strict);
            this._geoUtilsMock = this._repository.Create<IGeoUtils>();
            this._timeUtilsMock = this._repository.Create<ITimeUtils>();
            this._pathMock = this._repository.Create<IPath>();
            this._fileMock = this._repository.Create<IFile>();
            this._fileSystemMock = this._repository.Create<IFileSystem>();
            this._fileSystemMock.SetupGet(fs => fs.File).Returns(this._fileMock.Object);
            this._fileSystemMock.SetupGet(fs => fs.Path).Returns(this._pathMock.Object);
            this._loggerMock = this._repository.Create<ILogger<GpkgUtils>>(MockBehavior.Loose);
        }

        #region CreateTileIndex

        [TestMethod]
        [TestCategory("CreateTileIndex")]
        public void CreateTileIndex()
        {
            string path = this.GetGpkgPath();
            using (var connection = new SQLiteConnection($"Data Source={path}"))
            {
                connection.Open();
                this.SetupConstructorRequiredMocks(connection);
                this.CreateTestTiles(connection, Array.Empty<Tile>()); //create tile table

                var gpkgUtils = new GpkgUtils(path, this._timeUtilsMock.Object, this._loggerMock.Object,
                    this._fileSystemMock.Object, this._geoUtilsMock.Object);

                gpkgUtils.CreateTileIndex();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText =
                        "SELECT count(*) FROM sqlite_master WHERE type = 'index' " +
                        "AND tbl_name = 'test' " +
                        "AND sql LIKE 'CREATE UNIQUE INDEX%'" +
                        "AND sql LIKE '%zoom_level%'" +
                        "AND sql LIKE '%tile_row%'" +
                        "AND sql LIKE '%tile_column%'";
                    Assert.AreEqual(1l, command.ExecuteScalar());
                }
            }
            this.VerifyAll();
        }

        #endregion

        #region GetBatch
        public static IEnumerable<object[]> GenGetBatchParams()
        {
            return DynamicDataGenerator.GeneratePrams(
                new object[] { 1, 2, 10 }, //batchSize
                new object[] { 0, 1, 3, 10 } //offset
            );
        }

        [TestMethod]
        [TestCategory("GetBatch")]
        [DynamicData(nameof(GenGetBatchParams), DynamicDataSourceType.Method)]
        public void GetBatch(int batchSize, int offset)
        {
            string path = this.GetGpkgPath();
            var testTiles = new Tile[]
            {
                new Tile(0, 0, 0, Array.Empty<byte>()), new Tile(1, 1, 1, Array.Empty<byte>()),
                new Tile(2, 2, 2, Array.Empty<byte>()),new Tile(3, 3, 3, Array.Empty<byte>()),
                new Tile(4, 4, 4, Array.Empty<byte>()),new Tile(5, 5, 5, Array.Empty<byte>()),
            };

            using (var connection = new SQLiteConnection($"Data Source={path}"))
            {
                connection.Open();
                this.SetupConstructorRequiredMocks(connection);
                this.CreateTestTiles(connection, testTiles);

                var gpkgUtils = new GpkgUtils(path, this._timeUtilsMock.Object, this._loggerMock.Object,
                    this._fileSystemMock.Object, this._geoUtilsMock.Object);

                var comparer = ComparerFactory.Create<Tile>((t1, t2) => t1?.Z == t2?.Z && t1?.X == t2?.X && t1?.Y == t2?.Y ? 0 : -1);
                var res = gpkgUtils.GetBatch(batchSize, offset);
                var expected = testTiles.Skip(offset).Take(batchSize);
                CollectionAssert.AreEqual(expected.ToArray(), res, comparer);
            }
            this.VerifyAll();
        }

        #endregion

        #region GetExtent

        public static IEnumerable<object[]> GenGetExtentParams()
        {
            return DynamicDataGenerator.GeneratePrams(
                new object[]
                {
                    new Extent() { MinX = -180, MinY = -90, MaxX = 180, MaxY = 90 },
                    new Extent() { MinX = -55, MinY = -73, MaxX = -10, MaxY = 30 },
                    new Extent() { MinX = 10, MinY = 15, MaxX = 17, MaxY = 14 }
                } //extent

            );
        }

        [TestMethod]
        [TestCategory("GetExtent")]
        [DynamicData(nameof(GenGetExtentParams), DynamicDataSourceType.Method)]
        public void GetExtent(Extent extent)
        {
            string path = this.GetGpkgPath();

            using (var connection = new SQLiteConnection($"Data Source={path}"))
            {
                connection.Open();
                this.SetupConstructorRequiredMocks(connection, extent: extent);

                var gpkgUtils = new GpkgUtils(path, this._timeUtilsMock.Object, this._loggerMock.Object,
                    this._fileSystemMock.Object, this._geoUtilsMock.Object);

                var res = gpkgUtils.GetExtent();
                Assert.AreEqual(extent, res);
            }
            this.VerifyAll();
        }

        #endregion

        #region GetLastTile
        public static IEnumerable<object[]> GenGetLastTileParams()
        {
            yield return new object[] { new Coord(0, 0, 0) };
            yield return new object[] { new Coord(2, 2, 2) };
            yield return new object[] { new Coord(3, 3, 3) };
            yield return new object[] { new Coord(4, 4, 4) };
            yield return new object[] { new Coord(5, 5, 5) };
            yield return new object[] { new Coord(7, 7, 7) };
            yield return new object[] { new Coord(10, 10, 10) };
        }

        [TestMethod]
        [TestCategory("GetLastTile")]
        [DynamicData(nameof(GenGetLastTileParams), DynamicDataSourceType.Method)]
        public void GetLastTile(Coord baseCoords)
        {
            string path = this.GetGpkgPath();
            var testTiles = new Tile[]
            {
                new Tile(3, 3, 3, new byte[1]),
                new Tile(4, 4, 4, new byte[1]),new Tile(5, 5, 5, new byte[1]),
            };

            using (var connection = new SQLiteConnection($"Data Source={path}"))
            {
                connection.Open();
                this.SetupConstructorRequiredMocks(connection);
                this.CreateTestTiles(connection, testTiles);

                var gpkgUtils = new GpkgUtils(path, this._timeUtilsMock.Object, this._loggerMock.Object,
                    this._fileSystemMock.Object, this._geoUtilsMock.Object);
                var coords = new List<int>();
                for (int i = 0; i < baseCoords.Z; i++)
                {
                    coords.Add(i);
                    coords.Add(i);
                }
                var res = gpkgUtils.GetLastTile(coords.ToArray(), baseCoords);

                Tile expected = null;
                if (baseCoords.Z > 6)
                    expected = testTiles[2];
                else if (baseCoords.Z >= 4)
                    expected = testTiles[baseCoords.Z - 4];
                if (expected == null)
                {
                    Assert.IsNull(res);
                }
                else
                {
                    Assert.AreEqual(expected.Z, res.Z);
                    Assert.AreEqual(expected.X, res.X);
                    Assert.AreEqual(expected.Y, res.Y);
                    CollectionAssert.AreEqual(expected.GetImageBytes(), res.GetImageBytes());
                }
            }
            this.VerifyAll();
        }

        #endregion

        #region GetTileCount

        [TestMethod]
        [DataRow(0)]
        [DataRow(1)]
        [DataRow(7)]
        [DataRow(20)]
        [DataRow(134)]
        public void GetTileCount(int tileCount)
        {
            string path = this.GetGpkgPath();
            var testTiles = new List<Tile>(tileCount);
            for (int i = 0; i < tileCount; i++)
            {
                testTiles.Add(new Tile(0, i, 0, Array.Empty<byte>()));
            };

            using (var connection = new SQLiteConnection($"Data Source={path}"))
            {
                connection.Open();
                this.SetupConstructorRequiredMocks(connection);
                this.CreateTestTiles(connection, testTiles);

                var gpkgUtils = new GpkgUtils(path, this._timeUtilsMock.Object, this._loggerMock.Object,
                    this._fileSystemMock.Object, this._geoUtilsMock.Object);

                var res = gpkgUtils.GetTileCount();
                Assert.AreEqual(tileCount, res);
            }
            this.VerifyAll();
        }

        #endregion

        #region InsertTiles

        [TestMethod]
        [TestCategory("InsertTiles")]
        public void InsertTiles()
        {
            string path = this.GetGpkgPath();
            var existingTiles = new Tile[] { new Tile(0, 0, 0, new byte[] { 1, 2, 3 }), new Tile(3, 3, 3, new byte[] { 1, 2, 3 }) };
            var testTiles = new Tile[]
            {
                new Tile(3, 3, 3, new byte[1]),
                new Tile(4, 4, 4, new byte[1]),new Tile(5, 5, 5, new byte[1]),
            };

            using (var connection = new SQLiteConnection($"Data Source={path}"))
            {
                connection.Open();
                this.SetupConstructorRequiredMocks(connection);
                this.CreateTestTiles(connection, existingTiles);

                var gpkgUtils = new GpkgUtils(path, this._timeUtilsMock.Object, this._loggerMock.Object,
                    this._fileSystemMock.Object, this._geoUtilsMock.Object);

                gpkgUtils.InsertTiles(testTiles);

                var expectedTiles = new Tile[] { existingTiles[0] }.Concat(testTiles).ToArray();
                var res = new List<Tile>();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "select zoom_level,tile_column,tile_row, hex(tile_data) from test";
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            res.Add(new BlobTile(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetString(3), 0));
                        }
                    }
                }

                var compFunc = (Tile? t1, Tile? t2) => t1?.Z == t2?.Z && t1?.X == t2?.X && t1?.Y == t2?.Y && t1?.GetImageBytes().SequenceEqual(t2?.GetImageBytes()) != false ? 0 : -1;
                CollectionAssert.AreEqual(expectedTiles, res, ComparerFactory.Create(compFunc));
            }
            this.VerifyAll();
        }

        #endregion

        #region UpdateExtent
        public static IEnumerable<object[]> GenUpdateExtentParams()
        {
            yield return new object[] { new Extent() { MinX = -180, MinY = -90, MaxX = 180, MaxY = 90 } };
            yield return new object[] { new Extent() { MinX = -153, MinY = 41, MaxX = -30, MaxY = 47.56556 } };
            yield return new object[] { new Extent() { MinX = 15.94, MinY = 7.00035, MaxX = 17, MaxY = 36.184515 } };
        }

        [TestMethod]
        [TestCategory("UpdateExtent")]
        [DynamicData(nameof(GenUpdateExtentParams), DynamicDataSourceType.Method)]
        public void UpdateExtent(Extent extent)
        {
            string path = this.GetGpkgPath();

            using (var connection = new SQLiteConnection($"Data Source={path}"))
            {
                connection.Open();
                this.SetupConstructorRequiredMocks(connection);

                var gpkgUtils = new GpkgUtils(path, this._timeUtilsMock.Object, this._loggerMock.Object,
                    this._fileSystemMock.Object, this._geoUtilsMock.Object);
                gpkgUtils.UpdateExtent(extent);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT min_x,min_y,max_x,max_y FROM gpkg_contents";
                    using (var reader = command.ExecuteReader(CommandBehavior.SingleRow))
                    {
                        reader.Read();
                        var res = new Extent()
                        {
                            MinX = reader.GetDouble(0),
                            MinY = reader.GetDouble(1),
                            MaxX = reader.GetDouble(2),
                            MaxY = reader.GetDouble(3)
                        };
                        Assert.AreEqual(extent, res);
                    }
                }
            }
            this.VerifyAll();
        }

        #endregion

        #region Exist

        [TestMethod]
        [TestCategory("Exist")]
        [DataRow(true)]
        [DataRow(false)]
        public void Exist(bool exist)
        {
            string path = this.GetGpkgPath();

            using (var connection = new SQLiteConnection($"Data Source={path}"))
            {
                connection.Open();
                var seq = new MockSequence();
                this.SetupConstructorRequiredMocks(connection, sequence: seq);
                this._pathMock
                    .InSequence(seq)
                    .Setup(path => path.GetFullPath(It.IsAny<string>()))
                    .Returns("test");
                this._fileMock
                    .InSequence(seq)
                    .Setup(file => file.Exists("test"))
                    .Returns(exist);

                var gpkgUtils = new GpkgUtils(path, this._timeUtilsMock.Object, this._loggerMock.Object,
                    this._fileSystemMock.Object, this._geoUtilsMock.Object);


                Assert.AreEqual(exist, gpkgUtils.Exist());
            }
            this._fileMock.Verify(file => file.Exists("test"), Times.Exactly(2));
            this._pathMock.Verify(p => p.GetFullPath(path), Times.Exactly(2));
            this.VerifyAll();
        }

        #endregion

        //TODO: test create?

        #region DeleteTileTableTriggers

        [TestMethod]
        [TestCategory("DeleteTileTableTriggers")]
        public void DeleteTileTableTriggers()
        {
            string path = this.GetGpkgPath();
            using (var connection = new SQLiteConnection($"Data Source={path}"))
            {
                connection.Open();
                this.SetupConstructorRequiredMocks(connection);
                this.CreateTestTiles(connection, Array.Empty<Tile>());
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "CREATE TRIGGER t1 BEFORE INSERT ON test FOR EACH ROW BEGIN SELECT 'a'; END;" +
                                          "CREATE TRIGGER t2 BEFORE UPDATE ON test FOR EACH ROW BEGIN SELECT 'a'; END;" +
                                          "CREATE TRIGGER t3 BEFORE DELETE ON test FOR EACH ROW BEGIN SELECT 'a'; END;";
                    command.ExecuteNonQuery();
                }

                var gpkgUtils = new GpkgUtils(path, this._timeUtilsMock.Object, this._loggerMock.Object,
                    this._fileSystemMock.Object, this._geoUtilsMock.Object);

                gpkgUtils.DeleteTileTableTriggers();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText =
                        "SELECT count(*) FROM sqlite_master WHERE type = 'index' " +
                        "AND tbl_name = 'test' AND name NOT LIKE '%autoindex%';";
                    Assert.AreEqual(0l, command.ExecuteScalar());
                }
            }

            this.VerifyAll();
        }

        #endregion

        #region CreateTileCacheValidationTriggers

        [TestMethod]
        [TestCategory("CreateTileCacheValidationTriggers")]
        public void CreateTileCacheValidationTriggers()
        {
            string path = this.GetGpkgPath();
            using (var connection = new SQLiteConnection($"Data Source={path}"))
            {
                connection.Open();
                this.SetupConstructorRequiredMocks(connection);
                this.CreateTestTiles(connection, Array.Empty<Tile>());

                var gpkgUtils = new GpkgUtils(path, this._timeUtilsMock.Object, this._loggerMock.Object,
                    this._fileSystemMock.Object, this._geoUtilsMock.Object);

                gpkgUtils.CreateTileCacheValidationTriggers();

                foreach (var action in new string[] { "BEFORE INSERT", "BEFORE UPDATE" })
                {
                    foreach (var col in new string[] { "tile_column", "tile_row", "zoom" })
                    {
                        using (var command = connection.CreateCommand())
                        {
                            command.CommandText =
                                "SELECT count(*) FROM sqlite_master WHERE type = 'trigger' " +
                                "AND tbl_name = 'test' AND name NOT LIKE '%autoindex%' " +
                                $"AND sql Like '%{action}%' AND name LIKE '%{col}%';";
                            Assert.AreEqual(1l, command.ExecuteScalar(), $"{action} trigger is missing for {col}");
                        }
                    }
                }
            }
            this.VerifyAll();
        }

        #endregion

        #region UpdateTileMatrixTable

        public static IEnumerable<object[]> GenUpdateTileMatrixTableParams()
        {
            return DynamicDataGenerator.GeneratePrams(new object[][]
            {
                new object[] { true, false }, //is 1X1
                new object[] { 0, 1,5,12,21 } // max zoom
            });
        }

        [TestMethod]
        [TestCategory("UpdateTileMatrixTable")]
        [DynamicData(nameof(GenUpdateTileMatrixTableParams), DynamicDataSourceType.Method)]
        public void UpdateTileMatrixTable(bool isOneXOne, int maxZoom)
        {
            string path = this.GetGpkgPath();
            using (var connection = new SQLiteConnection($"Data Source={path}"))
            {
                connection.Open();
                this.SetupConstructorRequiredMocks(connection);
                this.CreateTestTiles(connection, new Tile[] { new Tile(maxZoom, 0, 0, Array.Empty<byte>()) });
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "CREATE TABLE gpkg_tile_matrix (" +
                                          "table_name TEXT NOT NULL," +
                                          "zoom_level INTEGER NOT NULL," +
                                          "matrix_width INTEGER NOT NULL," +
                                          "matrix_height INTEGER NOT NULL," +
                                          "tile_width INTEGER NOT NULL," +
                                          "tile_height INTEGER NOT NULL," +
                                          "pixel_x_size DOUBLE NOT NULL," +
                                          "pixel_y_size DOUBLE NOT NULL);";
                    command.ExecuteNonQuery();
                }

                var gpkgUtils = new GpkgUtils(path, this._timeUtilsMock.Object, this._loggerMock.Object,
                    this._fileSystemMock.Object, this._geoUtilsMock.Object);

                gpkgUtils.UpdateTileMatrixTable(isOneXOne);

                long expectedMatrixCount = maxZoom + 1;
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT count(*) FROM gpkg_tile_matrix";
                    Assert.AreEqual(expectedMatrixCount, command.ExecuteScalar());
                }

                int expetedTileSize = 256;
                int[] expectedMatrixYSize =
                { 
                    //0 1 2  3   4  5    6   7    8    9    10    11    12    13    14      15     16     17
                    1, 2, 4, 8, 16, 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192, 16384, 32768, 65536, 131072,
                    //18      19      20       21
                    262144, 524288, 1048576, 2097152
                };
                var getExpectedMatrixXSize = (int z) => isOneXOne ? expectedMatrixYSize[z] : 2 * expectedMatrixYSize[z];
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT table_name,zoom_level,matrix_width,matrix_height,tile_width,tile_height," +
                                          "pixel_x_size,pixel_y_size FROM gpkg_tile_matrix " +
                                          "ORDER BY zoom_level ASC";
                    using (var reader = command.ExecuteReader())
                    {
                        int i = 0;
                        while (reader.Read())
                        {
                            Assert.AreEqual("test", reader.GetString(0));
                            Assert.AreEqual(i, reader.GetInt32(1));
                            var expectedMatrixWidth = getExpectedMatrixXSize(i);
                            Assert.AreEqual(expectedMatrixWidth, reader.GetInt32(2));
                            Assert.AreEqual(expectedMatrixYSize[i], reader.GetInt32(3));
                            Assert.AreEqual(expetedTileSize, reader.GetInt32(4));
                            Assert.AreEqual(expetedTileSize, reader.GetInt32(5));
                            var expectedPixelSize = (360d / expectedMatrixWidth) / expetedTileSize;
                            Assert.AreEqual(expectedPixelSize, reader.GetDouble(6), 1e-22);
                            Assert.AreEqual(expectedPixelSize, reader.GetDouble(7), 1e-22);
                            i++;
                        }
                        Assert.AreEqual(maxZoom + 1, i);
                    }
                }
            }
            this.VerifyAll();
        }

        #endregion

        #region helpers

        private string GetGpkgPath()
        {
            //create "path" that manipulates sqlite connection string to use shared in memory db.
            //guid (uuid)  is used to make the db unique per test - this is required when running multiple tests in parallel
            return $"{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        }

        private void SetupConstructorRequiredMocks(SQLiteConnection connection, bool exists = true, Extent? extent = null, MockSequence sequence = null)
        {
            var seq = sequence ?? new MockSequence();
            this._pathMock
                .InSequence(seq)
                .Setup(path => path.GetFullPath(It.IsAny<string>()))
                .Returns("test");
            this._fileMock
                .InSequence(seq)
                .Setup(file => file.Exists("test"))
                .Returns(exists);
            if (!exists)
            {
                this._pathMock
                    .InSequence(seq)
                    .Setup(path => path.GetFileNameWithoutExtension(It.IsAny<string>()))
                    .Returns("test");
            }
            else
            {
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "CREATE TABLE \"gpkg_contents\" (\"table_name\" TEXT NOT NULL," +
                        "\"min_x\" DOUBLE," +
                        "\"min_y\" DOUBLE," +
                        "\"max_x\" DOUBLE," +
                        "\"max_y\" DOUBLE);";
                    command.ExecuteNonQuery();
                }
                using (var command = connection.CreateCommand())
                {
                    if (extent is null)
                    {
                        command.CommandText = "INSERT INTO \"gpkg_contents\" (\"table_name\") VALUES ('test');";
                    }
                    else
                    {
                        command.CommandText = "INSERT INTO \"gpkg_contents\" (\"table_name\",\"min_x\",\"min_y\",\"max_x\",\"max_y\") " +
                                              $"VALUES ('test',{extent.Value.MinX},{extent.Value.MinY},{extent.Value.MaxX},{extent.Value.MaxY});";
                    }
                    command.ExecuteNonQuery();
                }
            }
        }

        private void CreateTestTiles(SQLiteConnection connection, IEnumerable<Tile> testTiles)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "CREATE TABLE \"test\" (" +
                                      "\"zoom_level\" INTEGER NOT NULL," +
                                      "\"tile_column\" INTEGER NOT NULL," +
                                      "\"tile_row\" INTEGER NOT NULL," +
                                      "\"tile_data\" BLOB NOT NULL," +
                                      "UNIQUE(\"zoom_level\",\"tile_column\",\"tile_row\"));";
                command.ExecuteNonQuery();
            }

            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    "REPLACE INTO \"test\" (zoom_level, tile_column, tile_row, tile_data) VALUES ($z, $x, $y, $blob)";
                foreach (Tile tile in testTiles)
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
            }
        }

        private void VerifyAll()
        {
            this._fileMock.VerifyAll();
            this._pathMock.VerifyAll();
            this._timeUtilsMock.VerifyAll();
            this._geoUtilsMock.VerifyAll();
        }

        #endregion
    }
}
