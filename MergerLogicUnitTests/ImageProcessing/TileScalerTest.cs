using MergerLogic.Batching;
using MergerLogic.DataTypes;
using MergerLogic.ImageProcessing;
using MergerLogic.Monitoring.Metrics;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;

namespace MergerLogicUnitTests.ImageProcessing
{
    [TestClass]
    [TestCategory("unit")]
    [TestCategory("imageProcessing")]
    [DeploymentItem(@"../../../ImageProcessing/TestImages")]
    public class TileScalerTest
    {

        #region mocks

        private MockRepository _mockRepository;

        private TileScaler _testTileScaler;

        #endregion

        [TestInitialize]
        public void BeforeEach()
        {
            this._mockRepository = new MockRepository(MockBehavior.Loose);

            var metricsProviderMock = this._mockRepository.Create<IMetricsProvider>();
            var tileScalerLoggerMock = this._mockRepository.Create<ILogger<TileScaler>>();

            this._testTileScaler = new TileScaler(metricsProviderMock.Object, tileScalerLoggerMock.Object);
        }

        #region Upscale


        public static IEnumerable<object[]> GetUpscaleTilesTestParameters()
        {
            // yield return new object[] {
            //     File.ReadAllBytes("3.jpeg"),
            //     new Coord(9, 0, 0),
            //     new Coord(10, 0, 0),
            //     File.ReadAllBytes("3_upscaled_9_10.jpeg"),
            // };

            // yield return new object[] {
            //     File.ReadAllBytes("4.jpeg"),
            //     new Coord(3, 0, 0),
            //     new Coord(13, 0, 0),
            //     File.ReadAllBytes("4_upscaled_3_13.jpeg"),
            // };

            // yield return new object[] {
            //     File.ReadAllBytes("5.png"),
            //     new Coord(16, 0, 0),
            //     new Coord(18, 0, 0),
            //     File.ReadAllBytes("5_upscaled_16_18.png"),
            // };

            // yield return new object[] {
            //     File.ReadAllBytes("5_8bit.png"),
            //     new Coord(16, 0, 0),
            //     new Coord(18, 0, 0),
            //     File.ReadAllBytes("5_8bit_upscaled_16_18.png"),
            // };

            // yield return new object[] {
            //     File.ReadAllBytes("5_24bit.png"),
            //     new Coord(16, 0, 0),
            //     new Coord(18, 0, 0),
            //     File.ReadAllBytes("5_24bit_upscaled_16_18.png"),
            // };

            // yield return new object[] {
            //     File.ReadAllBytes("5_32bit.png"),
            //     new Coord(16, 0, 0),
            //     new Coord(18, 0, 0),
            //     File.ReadAllBytes("5_upscaled_16_18.png"),
            // };

            yield return new object[] {
                File.ReadAllBytes("5_64bit.png"),
                new Coord(16, 0, 0),
                new Coord(18, 0, 0),
                File.ReadAllBytes("5_upscaled_16_18.png"),
            };
        }
        [TestMethod]
        [TestCategory("Upscale")]
        [DynamicData(nameof(GetUpscaleTilesTestParameters), DynamicDataSourceType.Method)]
        public void Upscale(byte[] tileBytes, Coord baseTileCoord, Coord targetCoord, byte[] expectedTileBytes)
        {
            var testTile = new Tile(baseTileCoord, tileBytes);
            var resultTile = this._testTileScaler.Upscale(testTile, targetCoord);

            Console.WriteLine("----------- VIT ------------");
            Console.WriteLine($"{Convert.ToBase64String(resultTile.GetImageBytes())}");
            Console.WriteLine("----------- VIT ------------");

            Assert.IsNotNull(resultTile);
            CollectionAssert.AreEqual(expectedTileBytes, resultTile.GetImageBytes());
        }

        #endregion
    }
}
