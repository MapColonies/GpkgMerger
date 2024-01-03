﻿using MergerLogic.Batching;
using MergerLogic.DataTypes;
using MergerLogic.ImageProcessing;
using MergerLogic.Monitoring.Metrics;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MergerLogicUnitTests.ImageProcessing
{
    [TestClass]
    [TestCategory("unit")]
    [TestCategory("imageProcessing")]
    [DeploymentItem(@"../../../ImageProcessing/TestImages")]
    public class TileMergerTest
    {

        #region mocks

        private MockRepository _mockRepository;

        private TileMerger _testTileMerger;

        #endregion

        [TestInitialize]
        public void BeforeEach()
        {
            this._mockRepository = new MockRepository(MockBehavior.Loose);

            var metricsProviderMock = this._mockRepository.Create<IMetricsProvider>();
            var tileScalerLoggerMock = this._mockRepository.Create<ILogger<TileScaler>>();
            var tileMergerLoggerMock = this._mockRepository.Create<ILogger<TileMerger>>();

            var testTileScaler = new TileScaler(metricsProviderMock.Object, tileScalerLoggerMock.Object);
            this._testTileMerger = new TileMerger(testTileScaler, tileMergerLoggerMock.Object);
        }

        #region MergeTiles

        public static IEnumerable<object[]> GetMergeTilesTestParameters()
        {
            var targetCoordLowZoom = new Coord(5, 0, 0);
            var targetCoordMediumZoom = new Coord(14, 0, 0);
            var targetCoordHighZoom = new Coord(15, 0, 0);

            // yield return new object[] {
            //     new Tile[] {
            //         new Tile(targetCoordHighZoom, File.ReadAllBytes("2.png")),
            //         new Tile(targetCoordHighZoom, File.ReadAllBytes("1.png"))
            //     }, targetCoordHighZoom, TileFormat.Png,
            //     File.ReadAllBytes("2_1_merged.png"),
            // };

            // yield return new object[] {
            //     new Tile[] {
            //         new Tile(targetCoordHighZoom, File.ReadAllBytes("3.jpeg")),
            //         new Tile(targetCoordHighZoom, File.ReadAllBytes("1.png"))
            //     }, targetCoordHighZoom, TileFormat.Jpeg,
            //     File.ReadAllBytes("3_1_merged.jpeg"),
            // };

            // yield return new object[] {
            //     new Tile[] {
            //         new Tile(targetCoordHighZoom, File.ReadAllBytes("3.jpeg")),
            //         new Tile(targetCoordHighZoom, File.ReadAllBytes("4.jpeg"))
            //     }, targetCoordHighZoom, TileFormat.Jpeg,
            //     File.ReadAllBytes("3_4_merged.jpeg"),
            // };

            // yield return new object[] {
            //     new Tile[] {
            //         new Tile(targetCoordHighZoom, File.ReadAllBytes("3.jpeg")),
            //         new Tile(targetCoordLowZoom, File.ReadAllBytes("1.png"))
            //     }, targetCoordHighZoom, TileFormat.Jpeg,
            //     File.ReadAllBytes("3_1_merged_upscaled_5_15.jpeg"),
            // };

            yield return new object[] {
                new Tile[] {
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("3.jpeg")),
                    new Tile(targetCoordMediumZoom, File.ReadAllBytes("1.png"))
                }, targetCoordHighZoom, TileFormat.Jpeg,
                File.ReadAllBytes("3_1_merged.jpeg"),
            };
        }

        [TestMethod]
        [TestCategory("MergeTiles")]
        [DynamicData(nameof(GetMergeTilesTestParameters), DynamicDataSourceType.Method)]
        public void MergeTiles(Tile[] tiles, Coord targetCoord, TileFormat tileFormat, byte[] expectedTileBytes)
        {
            var tileBuilders = tiles.Select<Tile, CorrespondingTileBuilder>(tile => () => tile).ToList();
            var result = this._testTileMerger.MergeTiles(tileBuilders, targetCoord, tileFormat);

            Console.WriteLine("------------- VIT --------------");
            Console.WriteLine($"{Convert.ToBase64String(result)}");
            Console.WriteLine("------------- VIT --------------");

            Assert.IsNotNull(result);
            CollectionAssert.AreEqual(expectedTileBytes, result);
        }

        #endregion
    }
}
