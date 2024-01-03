using MergerLogic.ImageProcessing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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
        #region ConvertToFormat

        public static IEnumerable<object[]> GetConvertToFormatTestParameters()
        {
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
                File.ReadAllBytes("3.jpeg"),
                TileFormat.Png,
                false,
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
            var resultTile = ImageFormatter.ConvertToFormat(tileBytes, expectedTileFormat);
            var resultFormat = ImageFormatter.GetTileFormat(resultTile);

            Assert.IsNotNull(resultTile);
            Assert.AreEqual(expectedTileFormat, resultFormat);

            if (expectedUnchanged)
            {
                CollectionAssert.AreEqual(tileBytes, resultTile);
            }
        }

        #endregion

        #region GetTileFormat

        public static IEnumerable<object?[]> GetTileFormatTestParameters()
        {
            yield return new object[] {
                File.ReadAllBytes("1.png"),
                TileFormat.Png,
            };

            yield return new object[] {
                File.ReadAllBytes("3.jpeg"),
                TileFormat.Jpeg,
            };

            yield return new object?[] {
                File.ReadAllBytes("invalid_image"),
                null,
            };

            yield return new object[] {
                File.ReadAllBytes("5_8bit.png"),
                TileFormat.Png,
            };

            yield return new object[] {
                File.ReadAllBytes("5_24bit.png"),
                TileFormat.Png,
            };

            yield return new object[] {
                File.ReadAllBytes("5_32bit.png"),
                TileFormat.Png,
            };

            yield return new object[] {
                File.ReadAllBytes("5_64bit.png"),
                TileFormat.Png,
            };
        }
        [TestMethod]
        [TestCategory("GetTileFormat")]
        [DynamicData(nameof(GetTileFormatTestParameters), DynamicDataSourceType.Method)]
        public void GetTileFormat(byte[] tileBytes, TileFormat? expectedResultFormat)
        {
            var resultFormat = ImageFormatter.GetTileFormat(tileBytes);

            if (expectedResultFormat is null)
            {
                Assert.IsNull(resultFormat);
            }
            else
            {
                Assert.IsNotNull(resultFormat);
                Assert.AreEqual(expectedResultFormat, resultFormat);
            }
        }

        #endregion
    }
}
