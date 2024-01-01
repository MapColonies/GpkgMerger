using MergerLogic.Batching;
using MergerLogic.DataTypes;
using MergerLogic.ImageProcessing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;

namespace MergerLogicUnitTests.ImageProcessing
{
    [TestClass]
    [TestCategory("unit")]
    [TestCategory("imageProcessing")]
    [DeploymentItem(@"../../../ImageProcessing/TestImages")]
    public class ImageFormatterTest
    {

        #region mocks

        private ImageFormatter _testImageFormatter;

        #endregion

        [TestInitialize]
        public void BeforeEach()
        {
            this._testImageFormatter = new ImageFormatter();
        }

        #region ConvertToFormat

        public static IEnumerable<object[]> GetConvertToFormatTestParameters()
        {
            yield return new object[] {
                File.ReadAllBytes("3.jpeg"),
                TileFormat.Png,
                false,
            };

            yield return new object[] {
                File.ReadAllBytes("1.png"),
                TileFormat.Jpeg,
                false,
            };

            yield return new object[] {
                File.ReadAllBytes("2.png"),
                TileFormat.Png,
                true,
            };

            yield return new object[] {
                File.ReadAllBytes("4.jpeg"),
                TileFormat.Jpeg,
                true,
            };
        }
        [TestMethod]
        [TestCategory("ConvertToFormat")]
        [DynamicData(nameof(GetConvertToFormatTestParameters), DynamicDataSourceType.Method)]
        public void ConvertToFormat(byte[] tileBytes, TileFormat expectedTileFormat, bool expectedUnchanged)
        {
            var testTile = new Tile(new Coord(0, 0, 0), tileBytes);
            var resultTile = this._testImageFormatter.ConvertToFormat(testTile, expectedTileFormat);
            var resultFormat = this._testImageFormatter.GetTileFormat(resultTile.GetImageBytes());

            Assert.IsNotNull(resultTile);
            Assert.AreEqual(expectedTileFormat, resultFormat);

            if (expectedUnchanged)
            {
                CollectionAssert.AreEqual(tileBytes, resultTile.GetImageBytes());
            }
        }

        #endregion

        #region GetTileFormat

        public static IEnumerable<object[]> GetTileFormatTestParameters()
        {
            yield return new object[] {
                File.ReadAllBytes("1.png"),
                TileFormat.Png,
            };

            yield return new object[] {
                File.ReadAllBytes("3.jpeg"),
                TileFormat.Jpeg,
            };
        }
        [TestMethod]
        [TestCategory("GetTileFormat")]
        [DynamicData(nameof(GetTileFormatTestParameters), DynamicDataSourceType.Method)]
        public void GetTileFormat(byte[] tileBytes, TileFormat expectedResultFormat)
        {
            var resultFormat = this._testImageFormatter.GetTileFormat(tileBytes);

            Assert.IsNotNull(resultFormat);
            Assert.AreEqual(expectedResultFormat, resultFormat);
        }

        #endregion
    }
}
