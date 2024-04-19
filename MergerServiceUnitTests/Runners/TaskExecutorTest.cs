using MergerLogic.Batching;
using MergerLogic.DataTypes;
using MergerLogic.ImageProcessing;
using MergerLogic.Monitoring.Metrics;
using MergerLogic.Utils;
using MergerService.Controllers;
using MergerService.Models.Tasks;
using MergerService.Runners;
using MergerService.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Linq;

namespace MergerLogicUnitTests.Utils
{
    [TestClass]
    [TestCategory("unit")]
    [TestCategory("runners")]
    [DeploymentItem(@"../../../Runners/TestData")]
    public class TaskExecutorTest
    {
        #region mocks

        private MockRepository _mockRepository;

        private Mock<IDataFactory> _dataFactoryMock;
        private Mock<ITimeUtils> _timeUtilsMock;
        private Mock<IConfigurationManager> _configurationManagerMock;
        private Mock<ILogger<TaskExecutor>> _taskExecutorLoggerMock;
        private Mock<IMetricsProvider> _metricsProviderMock;
        private Mock<ITaskUtils> _taskUtilsMock;
        private Mock<ITileScaler> _tileScalerMock;
        private Mock<ILogger<TileMerger>> _tileMergerLoggerMock;

        private ActivitySource _testActivitySource;
        private ITileMerger _testTileMerger;
        private IFileSystem _testFileSystem;

        #endregion

        public static IEnumerable<object[]> GetSingleTileTestParameters()
        {
            yield return new object[] { 1, true };
            yield return new object[] { 3, true };
            yield return new object[] { 54, true };
            yield return new object[] { 1, false };
            yield return new object[] { 5, false };
            yield return new object[] { 14, false };
        }

        [TestInitialize]
        public void BeforeEach()
        {
            this._mockRepository = new MockRepository(MockBehavior.Loose);

            this._dataFactoryMock = this._mockRepository.Create<IDataFactory>();
            this._timeUtilsMock = this._mockRepository.Create<ITimeUtils>();
            this._configurationManagerMock = this._mockRepository.Create<IConfigurationManager>();
            this._taskExecutorLoggerMock = this._mockRepository.Create<ILogger<TaskExecutor>>();
            this._metricsProviderMock = this._mockRepository.Create<IMetricsProvider>();
            this._taskUtilsMock = this._mockRepository.Create<ITaskUtils>();
            this._tileScalerMock = this._mockRepository.Create<ITileScaler>();
            this._tileMergerLoggerMock = this._mockRepository.Create<ILogger<TileMerger>>();

            this._testActivitySource = new ActivitySource("test");
            this._testTileMerger = new TileMerger(_tileScalerMock.Object, _tileMergerLoggerMock.Object);
            this._testFileSystem = new FileSystem();
        }

        [TestMethod]
        [DynamicData(nameof(GetSingleTileTestParameters), DynamicDataSourceType.Method)]
        public void WhenGivenSourcesWithOneTile_ShouldWriteAllTilesToTarget(int numberOfSources, bool isTargetNew)
        {
            this._configurationManagerMock.Setup(configManager => configManager.GetConfiguration<int>("GENERAL", "batchSize", "batchMaxSize")).Returns(1);
            this._configurationManagerMock.Setup(configManager => configManager.GetConfiguration<bool>("GENERAL", "batchSize", "limitBatchSize")).Returns(true);
            this._configurationManagerMock.Setup(configManager => configManager.GetConfiguration<long>("GENERAL", "batchMaxBytes")).Returns(1);

            var taskTargetTuple = this.SetupTestTask(numberOfSources, isTargetNew);
            var testTask = taskTargetTuple.Item1;
            var targetDataMock = taskTargetTuple.Item2;
            var testSourceTiles = taskTargetTuple.Item3;
            var resultWrittenTiles = new List<Tile>();

            var testTaskExecutor = new TaskExecutor(_dataFactoryMock.Object, _testTileMerger, _timeUtilsMock.Object,
              _configurationManagerMock.Object, _taskExecutorLoggerMock.Object, _testActivitySource, _testFileSystem,
              _metricsProviderMock.Object);

            targetDataMock.Setup(targetData => targetData.UpdateTiles(It.IsAny<IEnumerable<Tile>>())).Callback<IEnumerable<Tile>>(
              resultWrittenTiles.AddRange
            );

            testTaskExecutor.ExecuteTask(testTask, _taskUtilsMock.Object, null);

            targetDataMock.Verify(targetData => targetData.UpdateTiles(It.Is<IEnumerable<Tile>>(
              tiles => tiles.All(
                tile => testSourceTiles.Any(sourceTile => sourceTile.Z == tile.Z && sourceTile.X == tile.X && sourceTile.Y == tile.Y)
              )
            )), Times.Exactly(numberOfSources));
            targetDataMock.Verify(targetData => targetData.Wrapup(), Times.Once);

            Assert.AreEqual(testSourceTiles.Length, resultWrittenTiles.Count);
            Assert.IsTrue(testSourceTiles.All(
              tile => resultWrittenTiles.Any(sourceTile => sourceTile.Z == tile.Z && sourceTile.X == tile.X && sourceTile.Y == tile.Y)
            ));
        }

        public static IEnumerable<object[]> GetBatchLimitTestParameters()
        {
            // Tests that given a batch limit size, it flushes every time it reaches the limit
            // When the limit is 2, and there are total 5 tiles, we expect 3 flushes
            // We set the bytes limit to a high value to avoid it being reached by the test
            yield return new object[] {
                true, 2, 1024 * 1024 * 80, 3, 5
            };

            // Tests that given a batch bytes limit, it flushes every time it reaches the limit
            // When the limit is one byte more then twice the size of the tile, and there are total 7 tiles, we expect 3 flushes
            // (Flush every 3rd tile, and additional flush at the end)
            // We disable the size limit to avoid it being used in the test
            yield return new object[] {
                false, 1, (File.ReadAllBytes("tile.jpeg").Length * 2) + 1, 3, 7
            };
        }

        [TestMethod]
        [DynamicData(nameof(GetBatchLimitTestParameters), DynamicDataSourceType.Method)]
        public void WhenConfiguringBatchLimits_ShouldWriteTilesEachTimeAfterReachingBatchLimit(bool limitBatchSize, int batchMaxSize, long batchMaxBytes, int amountOfFlushes, int totalAmountOfTiles)
        {
            this._configurationManagerMock.Setup(configManager => configManager.GetConfiguration<int>("GENERAL", "batchSize", "batchMaxSize")).Returns(batchMaxSize);
            this._configurationManagerMock.Setup(configManager => configManager.GetConfiguration<bool>("GENERAL", "batchSize", "limitBatchSize")).Returns(limitBatchSize);
            this._configurationManagerMock.Setup(configManager => configManager.GetConfiguration<long>("GENERAL", "batchMaxBytes")).Returns(batchMaxBytes);

            byte[] tileBytes = File.ReadAllBytes("tile.jpeg");
            Source testTarget = new Source("target", "target_type", new Extent(), GridOrigin.UPPER_LEFT, Grid.TwoXOne);
            Mock<IData> targetDataMock = this._mockRepository.Create<IData>();

            Source testSource = new Source($"source", $"source_type");
            Coord[] testSourceCoords = new int[totalAmountOfTiles].Select((_, index) => new Coord(1, index, 0)).ToArray();
            Tile[] testSourceTiles = testSourceCoords.Select(coord => new Tile(coord, tileBytes)).ToArray();
            TileBounds tileBounds = new TileBounds(1, 0, testSourceCoords.Length, 0, 1);
            Mock<IData> sourceDataMock = this._mockRepository.Create<IData>();

            for (var testSourceCoordIdx = 0; testSourceCoordIdx < testSourceCoords.Length; testSourceCoordIdx++)
            {
                sourceDataMock.Setup(sourceData => sourceData.GetCorrespondingTile(testSourceCoords[testSourceCoordIdx], It.IsAny<bool>())).Returns(testSourceTiles[testSourceCoordIdx]);
            }

            this._dataFactoryMock.Setup(
                dataFactory => dataFactory.CreateDataSource(
                  testSource.Type, testSource.Path, It.IsAny<int>(),
                  testSource.Grid, testSource.Origin, testSource.Extent, It.IsAny<bool>())
            ).Returns(sourceDataMock.Object);

            this._dataFactoryMock.Setup(
              dataFactory => dataFactory.CreateDataSource(
                testTarget.Type, testTarget.Path, It.IsAny<int>(),
                testTarget.Grid, testTarget.Origin, testTarget.Extent, It.IsAny<bool>())
            ).Returns(targetDataMock.Object);

            var testTask = new MergeTask("id", "type", "description",
              new MergeMetadata(
                TileFormat.Jpeg, true, new TileBounds[] { tileBounds },
                new Source[] { testTarget, testSource }
              ),
              Status.PENDING, null, "reason", 0, "jobId", true, new DateTime(), new DateTime()
            );

            var resultWrittenTiles = new List<Tile>();
            var testTaskExecutor = new TaskExecutor(_dataFactoryMock.Object, _testTileMerger, _timeUtilsMock.Object,
              _configurationManagerMock.Object, _taskExecutorLoggerMock.Object, _testActivitySource, _testFileSystem,
              _metricsProviderMock.Object);

            targetDataMock.Setup(targetData => targetData.UpdateTiles(It.IsAny<IEnumerable<Tile>>())).Callback<IEnumerable<Tile>>(
              resultWrittenTiles.AddRange
            );

            testTaskExecutor.ExecuteTask(testTask, _taskUtilsMock.Object, null);

            targetDataMock.Verify(targetData => targetData.UpdateTiles(It.IsAny<IEnumerable<Tile>>()), Times.Exactly(amountOfFlushes));
            targetDataMock.Verify(targetData => targetData.Wrapup(), Times.Once);
            Assert.AreEqual(testSourceTiles.Length, resultWrittenTiles.Count);
            Assert.IsTrue(testSourceTiles.All(
                tile => resultWrittenTiles.Any(sourceTile => sourceTile.Z == tile.Z && sourceTile.X == tile.X && sourceTile.Y == tile.Y)
            ));
        }

        private Tuple<MergeTask, Mock<IData>, Tile[]> SetupTestTask(int amountOfSources, bool isTargetNew)
        {
            byte[] tileBytes = File.ReadAllBytes("tile.jpeg");
            Coord testTargetCoord = new Coord(1, 1, 1);
            Coord[] testSourceCoords = new int[amountOfSources].Select((_, index) => new Coord(index + 2, index + 2, index + 2)).ToArray();
            Source testTarget = new Source("target", "target_type", new Extent(), GridOrigin.UPPER_LEFT, Grid.TwoXOne);
            Source[] testSources = testSourceCoords.Select((_, index) => new Source($"source_{index}", $"source_type_{index}")).ToArray();
            Tile testTargetTile = new Tile(testTargetCoord, tileBytes);
            Tile[] testSourcesTiles = testSourceCoords.Select(coord => new Tile(coord, tileBytes)).ToArray();
            TileBounds[] tileBounds = testSourceCoords.Select(coord => new TileBounds(coord.Z, coord.X, coord.X + 1, coord.Y, coord.Y + 1)).ToArray();
            Mock<IData> targetDataMock = this._mockRepository.Create<IData>();
            Mock<IData>[] sourcesDataMocks = testSourcesTiles.Select((tile, index) =>
            {
                var sourceDataMock = this._mockRepository.Create<IData>();
                sourceDataMock.Setup(sourceData => sourceData.GetCorrespondingTile(testSourceCoords[index], It.IsAny<bool>())).Returns(tile);
                this._dataFactoryMock.Setup(
            dataFactory => dataFactory.CreateDataSource(
              testSources[index].Type, testSources[index].Path, It.IsAny<int>(),
              testSources[index].Grid, testSources[index].Origin, testSources[index].Extent, It.IsAny<bool>())
          ).Returns(sourceDataMock.Object);
                return sourceDataMock;
            }).ToArray();

            this._dataFactoryMock.Setup(
              dataFactory => dataFactory.CreateDataSource(
                testTarget.Type, testTarget.Path, It.IsAny<int>(),
                testTarget.Grid, testTarget.Origin, testTarget.Extent, It.IsAny<bool>())
            ).Returns(targetDataMock.Object);

            if (!isTargetNew)
            {
                targetDataMock.Setup(targetData => targetData.GetCorrespondingTile(testTargetCoord, It.IsAny<bool>())).Returns(testTargetTile);
                tileBounds = tileBounds.Prepend(new TileBounds(testTargetCoord.Z, testTargetCoord.X, testTargetCoord.X + 1, testTargetCoord.Y, testTargetCoord.Y + 1)).ToArray();
            }

            var testTask = new MergeTask("id", "type", "description",
              new MergeMetadata(
                TileFormat.Jpeg, isTargetNew, tileBounds,
                testSources.Prepend(testTarget).ToArray()
              ),
              Status.PENDING, null, "reason", 0, "jobId", true, new DateTime(), new DateTime()
            );

            return new Tuple<MergeTask, Mock<IData>, Tile[]>(testTask, targetDataMock, testSourcesTiles);
        }
    }
}
