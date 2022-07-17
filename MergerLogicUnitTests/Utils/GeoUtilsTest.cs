using MergerLogic.Batching;
using MergerLogic.DataTypes;
using MergerLogic.Utils;
using MergerLogicUnitTests.testUtils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

namespace MergerLogicUnitTests.Utils
{
    [TestClass]
    [TestCategory("unit")]
    [TestCategory("geo")]
    [TestCategory("geoUtils")]
    public class GeoUtilsTest
    {
        #region FlipY

        public enum GetFlipYParamType { Tile, Coord, Ints }
        public static IEnumerable<object[]> GenFlipYParams()
        {
            return DynamicDataGenerator.GeneratePrams(new object[][]
            {
                new object[] { GetFlipYParamType.Coord, GetFlipYParamType.Ints, GetFlipYParamType.Tile}, //param type
                new object[] {new Coord(0,0,0), new Coord(1,1,0), new Coord(1, 0, 1), new Coord(19,152339, 371948) } //coords, X cord is used for expected y
            });
        }

        [TestMethod]
        [TestCategory("FlipY")]
        [DynamicData(nameof(GenFlipYParams), DynamicDataSourceType.Method)]
        public void FlipY(GetFlipYParamType paramType, Coord coords)
        {
            var geoUtils = new GeoUtils();
            int res = -1;

            switch (paramType)
            {
                case GetFlipYParamType.Coord:
                    res = geoUtils.FlipY(coords);
                    break;
                case GetFlipYParamType.Ints:
                    res = geoUtils.FlipY(coords.Z, coords.Y);
                    break;
                case GetFlipYParamType.Tile:
                    res = geoUtils.FlipY(new Tile(coords, Array.Empty<byte>()));
                    break;
            }
            Assert.AreEqual(coords.X, res);
        }

        #endregion

        #region DegreesPerTile

        [TestMethod]
        [TestCategory("DegreesPerTile")]
        [DataRow(0, 180)]
        [DataRow(19, 0.00034332275390625)]
        public void DegreesPerTile(int zoom, double expected)
        {
            var geoUtils = new GeoUtils();
            var res = geoUtils.DegreesPerTile(zoom);

            Assert.AreEqual(expected, res);
        }

        #endregion

        #region SnapExtentToTileGrid

        public static IEnumerable<object[]> GenSnapExtentToTileGridParams()
        {
            yield return new object[]
            {
                21, new Extent() { MinX = -180, MinY = -90, MaxX = 180, MaxY = 90 },
                new Extent() { MinX = -180, MinY = -90, MaxX = 180, MaxY = 90 }
            };
            yield return new object[]
            {
                0, new Extent() { MinX = 95.6471142, MinY = 12.42158146886, MaxX = 99.1275474144, MaxY = 12.42159 },
                new Extent() { MinX = 0, MinY = -90, MaxX = 180, MaxY = 90 }
            };
            yield return new object[]
            {
                7, new Extent() { MinX = 84.37469412158, MinY = 21.093768421484, MaxX = 106, MaxY = 88.46985612 },
                new Extent() { MinX = 82.96875, MinY = 21.09375, MaxX = 106.875, MaxY = 88.59375 }
            };
        }

        [TestMethod]
        [TestCategory("SnapExtentToTileGrid")]
        [DynamicData(nameof(GenSnapExtentToTileGridParams), DynamicDataSourceType.Method)]
        public void SnapExtentToTileGrid(int zoom, Extent extent, Extent expectedExtent)
        {
            var geoUtils = new GeoUtils();
            var res = geoUtils.SnapExtentToTileGrid(extent, zoom);

            Assert.AreEqual(expectedExtent, res);
        }

        #endregion

        #region ExtentToTileRange

        public static IEnumerable<object[]> GenExtentToTileRangeParams()
        {
            yield return new object[]
            {
                21, new Extent() { MinX = -180, MinY = -90, MaxX = 180, MaxY = 90 }, GridOrigin.LOWER_LEFT,
                new TileBounds(21,0,4194304,0,2097152)
            };
            yield return new object[]
            {
                21, new Extent() { MinX = -180, MinY = -90, MaxX = 180, MaxY = 90 }, GridOrigin.UPPER_LEFT,
                new TileBounds(21,0,4194304,0,2097152)
            };
            yield return new object[]
            {
                0, new Extent() { MinX = 95.6471142, MinY = 12.42158146886, MaxX = 99.1275474144, MaxY = 12.42159 }, GridOrigin.LOWER_LEFT,
                new TileBounds(0,1,2,0,1)
            };
            yield return new object[]
            {
                0, new Extent() { MinX = 95.6471142, MinY = 12.42158146886, MaxX = 99.1275474144, MaxY = 12.42159 }, GridOrigin.UPPER_LEFT,
                new TileBounds(0,1,2,0,1)
            };
            yield return new object[]
            {
                7, new Extent() { MinX = 84.37469412158, MinY = 21.093768421484, MaxX = 106, MaxY = 88.59375 }, GridOrigin.LOWER_LEFT,
                new TileBounds(7,187,204,79,127)
            };
            yield return new object[]
            {
                7, new Extent() { MinX = 84.37469412158, MinY = 21.093768421484, MaxX = 106, MaxY = 88.59375 }, GridOrigin.UPPER_LEFT,
                new TileBounds(7,187,204,1,49)
            };
            yield return new object[]
            {
                10, new Extent() { MinX = 0, MinY = 0, MaxX = 0.175781, MaxY = 0.175781 }, GridOrigin.LOWER_LEFT,
                new TileBounds(10,1024,1025,512,513)
            };
            yield return new object[]
            {
                10, new Extent() { MinX = 0, MinY = 0, MaxX = 0.175781, MaxY = 0.175781 }, GridOrigin.UPPER_LEFT,
                new TileBounds(10,1024,1025,511,512)
            };
        }

        [TestMethod]
        [TestCategory("ExtentToTileRange")]
        [DynamicData(nameof(GenExtentToTileRangeParams), DynamicDataSourceType.Method)]
        public void ExtentToTileRange(int zoom, Extent extent, GridOrigin origin, TileBounds expectedBounds)
        {
            var geoUtils = new GeoUtils();
            var res = geoUtils.ExtentToTileRange(extent, zoom, origin);

            Assert.AreEqual(expectedBounds.MinX, res.MinX);
            Assert.AreEqual(expectedBounds.MaxX, res.MaxX);
            Assert.AreEqual(expectedBounds.MinY, res.MinY);
            Assert.AreEqual(expectedBounds.MaxY, res.MaxY);
        }

        #endregion

        #region TileRangeToExtent

        public static IEnumerable<object[]> GenTileRangeToExtentParams()
        {
            yield return new object[]
            {
                new TileBounds(21,0,4194304,0,2097152), new Extent() { MinX = -180, MinY = -90, MaxX = 180, MaxY = 90 }
            };
            yield return new object[]
            {
                new TileBounds(0,1,2,0,1), new Extent() { MinX = 0, MinY = -90, MaxX = 180, MaxY = 90 }
            };
            yield return new object[]
            {
                new TileBounds(7,187,204,79,127), new Extent() { MinX = 82.96875, MinY = 21.09375, MaxX = 106.875, MaxY = 88.59375 }
            };
            yield return new object[]
            {
                new TileBounds(10,1024,1025,512,513), new Extent() { MinX = 0, MinY = 0, MaxX = 0.17578125, MaxY = 0.17578125 }
            };
        }

        [TestMethod]
        [TestCategory("TileRangeToExtent")]
        [DynamicData(nameof(GenTileRangeToExtentParams), DynamicDataSourceType.Method)]
        public void TileRangeToExtent(TileBounds bounds, Extent expectedExtent)
        {
            var geoUtils = new GeoUtils();
            var res = geoUtils.TileRangeToExtent(bounds);

            Assert.AreEqual(expectedExtent.MinX, res.MinX);
            Assert.AreEqual(expectedExtent.MaxX, res.MaxX);
            Assert.AreEqual(expectedExtent.MinY, res.MinY);
            Assert.AreEqual(expectedExtent.MaxY, res.MaxY);
            Assert.AreEqual(expectedExtent, res);
        }

        #endregion
    }
}
