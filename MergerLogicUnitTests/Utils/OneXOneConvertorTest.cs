using MergerLogic.Batching;
using MergerLogic.DataTypes;
using MergerLogic.Utils;
using MergerLogicUnitTests.testUtils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;

namespace MergerLogicUnitTests.Utils
{
    [TestClass]
    [TestCategory("unit")]
    [TestCategory("1X1")]
    [TestCategory("OneXOneConvertor")]
    [DeploymentItem(@"../../../Utils/TestData")]

    public class OneXOneConvertorTest
    {
        public enum OneXOneConvertorParameterType
        {
            Cords,
            Ints,
            Tile
        }

        #region mocks

        private byte[] _jpegImageData;

        #endregion

        [TestInitialize]
        public void BeforeEach()
        {
            this._jpegImageData = File.ReadAllBytes("no_transparency.jpeg");
        }

        #region FromTwoXOne

        public static IEnumerable<object[]> GenFromTwoXOneCoordsParams()
        {
            return DynamicDataGenerator.GeneratePrams(new object[][]
                {
                 new object[]
                 {
                     OneXOneConvertorParameterType.Cords, OneXOneConvertorParameterType.Ints, OneXOneConvertorParameterType.Tile
                 }, //parameter type
                 new object[]
                 {
                     new TestExpected<Coord,Coord>(new Coord(5,7,2),new Coord(6,7,18)),
                     new TestExpected<Coord,Coord>(new Coord(1,0,0),new Coord(2,0,1)),
                     new TestExpected<Coord,Coord>(new Coord(20,2006279,1047924),new Coord(21,2006279,1572212))
                 } // test data
                });
        }

        [TestMethod]
        [TestCategory("FromTwoXOne")]
        [DynamicData(nameof(GenFromTwoXOneCoordsParams), DynamicDataSourceType.Method)]
        public void FromTwoXOneCoords(OneXOneConvertorParameterType convertorParameterType, TestExpected<Coord, Coord> testExpected)
        {
            var testData = testExpected.TestData;
            var convertor = new OneXOneConvertor();

            object? res = convertorParameterType switch
            {
                OneXOneConvertorParameterType.Cords => convertor.FromTwoXOne(testData),
                OneXOneConvertorParameterType.Ints => convertor.FromTwoXOne(testData.Z, testData.X, testData.Y),
                OneXOneConvertorParameterType.Tile => convertor.FromTwoXOne(new Tile(testData, this._jpegImageData)),
                _ => null
            };

            if (convertorParameterType != OneXOneConvertorParameterType.Tile)
            {
                var resCoords = res as Coord;
                Assert.IsNotNull(resCoords);
                Assert.AreEqual(testExpected.ExpectedData.Z, resCoords.Z);
                Assert.AreEqual(testExpected.ExpectedData.X, resCoords.X);
                Assert.AreEqual(testExpected.ExpectedData.Y, resCoords.Y);
            }
            else
            {
                var resTile = res as Tile;
                Assert.IsNotNull(resTile);
                Assert.AreEqual(testExpected.ExpectedData.Z, resTile.Z);
                Assert.AreEqual(testExpected.ExpectedData.X, resTile.X);
                Assert.AreEqual(testExpected.ExpectedData.Y, resTile.Y);
                CollectionAssert.AreEqual(this._jpegImageData, resTile.GetImageBytes());
            }
        }

        #endregion

        #region TryFromTwoXOne

        public static IEnumerable<object[]> GenTryFromTwoXOneCoordsParams()
        {
            return DynamicDataGenerator.GeneratePrams(new object[][]
                {
                 new object[]
                 {
                     OneXOneConvertorParameterType.Cords, OneXOneConvertorParameterType.Ints, OneXOneConvertorParameterType.Tile
                 }, //parameter type
                 new object[]
                 {
                     new TestExpected<Coord,Coord?>(new Coord(5,7,2),new Coord(6,7,18)),
                     new TestExpected<Coord,Coord?>(new Coord(1,0,0),new Coord(2,0,1)),
                     new TestExpected<Coord,Coord?>(new Coord(20,2006279,1047924),new Coord(21,2006279,1572212)),
                     new TestExpected<Coord,Coord?>(new Coord(0,0,0),null)
                 } //test data
                });
        }

        [TestMethod]
        [TestCategory("TryFromTwoXOne")]
        [DynamicData(nameof(GenTryFromTwoXOneCoordsParams), DynamicDataSourceType.Method)]
        public void TryFromTwoXOneCoords(OneXOneConvertorParameterType convertorParameterType, TestExpected<Coord, Coord?> testExpected)
        {
            var testData = testExpected.TestData;
            var convertor = new OneXOneConvertor();

            object? res = convertorParameterType switch
            {
                OneXOneConvertorParameterType.Cords => convertor.TryFromTwoXOne(testData),
                OneXOneConvertorParameterType.Ints => convertor.TryFromTwoXOne(testData.Z, testData.X, testData.Y),
                OneXOneConvertorParameterType.Tile => convertor.TryFromTwoXOne(new Tile(testData, this._jpegImageData)),
                _ => null
            };

            if (testExpected.ExpectedData == null)
            {
                Assert.IsNull(res);
            }
            else
            {
                if (convertorParameterType != OneXOneConvertorParameterType.Tile)
                {
                    var resCoords = res as Coord;
                    Assert.IsNotNull(resCoords);
                    Assert.AreEqual(testExpected.ExpectedData.Z, resCoords.Z);
                    Assert.AreEqual(testExpected.ExpectedData.X, resCoords.X);
                    Assert.AreEqual(testExpected.ExpectedData.Y, resCoords.Y);
                }
                else
                {
                    var resTile = res as Tile;
                    Assert.IsNotNull(resTile);
                    Assert.AreEqual(testExpected.ExpectedData.Z, resTile.Z);
                    Assert.AreEqual(testExpected.ExpectedData.X, resTile.X);
                    Assert.AreEqual(testExpected.ExpectedData.Y, resTile.Y);
                    CollectionAssert.AreEqual(this._jpegImageData, resTile.GetImageBytes());
                }
            }
        }

        #endregion

        #region FromTwoXOne

        public static IEnumerable<object[]> GenToTwoXOneCoordsParams()
        {
            return DynamicDataGenerator.GeneratePrams(new object[][]
                {
                 new object[]
                 {
                     OneXOneConvertorParameterType.Cords, OneXOneConvertorParameterType.Ints, OneXOneConvertorParameterType.Tile
                 }, //parameter type
                 new object[]
                 {
                     new TestExpected<Coord,Coord>(new Coord(6,7,18), new Coord(5,7,2)),
                     new TestExpected<Coord,Coord>(new Coord(2,0,1), new Coord(1,0,0)),
                     new TestExpected<Coord,Coord>(new Coord(21,2006279,1572212), new Coord(20,2006279,1047924))
                 } // test data
                });
        }

        [TestMethod]
        [TestCategory("ToTwoXOne")]
        [DynamicData(nameof(GenToTwoXOneCoordsParams), DynamicDataSourceType.Method)]
        public void ToTwoXOneCoords(OneXOneConvertorParameterType convertorParameterType, TestExpected<Coord, Coord> testExpected)
        {
            var testData = testExpected.TestData;
            var convertor = new OneXOneConvertor();

            object? res = convertorParameterType switch
            {
                OneXOneConvertorParameterType.Cords => convertor.ToTwoXOne(testData),
                OneXOneConvertorParameterType.Ints => convertor.ToTwoXOne(testData.Z, testData.X, testData.Y),
                OneXOneConvertorParameterType.Tile => convertor.ToTwoXOne(new Tile(testData, this._jpegImageData)),
                _ => null
            };

            if (convertorParameterType != OneXOneConvertorParameterType.Tile)
            {
                var resCoords = res as Coord;
                Assert.IsNotNull(resCoords);
                Assert.AreEqual(testExpected.ExpectedData.Z, resCoords.Z);
                Assert.AreEqual(testExpected.ExpectedData.X, resCoords.X);
                Assert.AreEqual(testExpected.ExpectedData.Y, resCoords.Y);
            }
            else
            {
                var resTile = res as Tile;
                Assert.IsNotNull(resTile);
                Assert.AreEqual(testExpected.ExpectedData.Z, resTile.Z);
                Assert.AreEqual(testExpected.ExpectedData.X, resTile.X);
                Assert.AreEqual(testExpected.ExpectedData.Y, resTile.Y);
                CollectionAssert.AreEqual(this._jpegImageData, resTile.GetImageBytes());
            }
        }

        #endregion

        #region TryToTwoXOne

        public static IEnumerable<object[]> GenTryToTwoXOneCoordsParams()
        {
            return DynamicDataGenerator.GeneratePrams(new object[][]
                {
                 new object[]
                 {
                     OneXOneConvertorParameterType.Ints, OneXOneConvertorParameterType.Tile
                 }, //parameter type
                 new object[]
                 {
                     new TestExpected<Coord,Coord>(new Coord(6,7,18), new Coord(5,7,2)),
                     new TestExpected<Coord,Coord>(new Coord(2,0,1), new Coord(1,0,0)),
                     new TestExpected<Coord,Coord>(new Coord(21,2006279,1572212), new Coord(20,2006279,1047924)),
                     new TestExpected<Coord,Coord?>(new Coord(0,0,0),null),
                     new TestExpected<Coord,Coord?>(new Coord(1,1,1),null)
                 } //test data
                });
        }

        [TestMethod]
        [TestCategory("TryToTwoXOne")]
        [DynamicData(nameof(GenTryToTwoXOneCoordsParams), DynamicDataSourceType.Method)]
        public void TryToTwoXOneCoords(OneXOneConvertorParameterType convertorParameterType, TestExpected<Coord, Coord?> testExpected)
        {
            var testData = testExpected.TestData;
            var convertor = new OneXOneConvertor();

            object? res = convertorParameterType switch
            {
                OneXOneConvertorParameterType.Ints => convertor.TryToTwoXOne(testData.Z, testData.X, testData.Y),
                OneXOneConvertorParameterType.Tile => convertor.TryToTwoXOne(new Tile(testData, this._jpegImageData)),
                _ => null
            };

            if (testExpected.ExpectedData == null)
            {
                Assert.IsNull(res);
            }
            else
            {
                if (convertorParameterType != OneXOneConvertorParameterType.Tile)
                {
                    var resCoords = res as Coord;
                    Assert.IsNotNull(resCoords);
                    Assert.AreEqual(testExpected.ExpectedData.Z, resCoords.Z);
                    Assert.AreEqual(testExpected.ExpectedData.X, resCoords.X);
                    Assert.AreEqual(testExpected.ExpectedData.Y, resCoords.Y);
                }
                else
                {
                    var resTile = res as Tile;
                    Assert.IsNotNull(resTile);
                    Assert.AreEqual(testExpected.ExpectedData.Z, resTile.Z);
                    Assert.AreEqual(testExpected.ExpectedData.X, resTile.X);
                    Assert.AreEqual(testExpected.ExpectedData.Y, resTile.Y);
                    CollectionAssert.AreEqual(this._jpegImageData, resTile.GetImageBytes());
                }
            }
        }

        #endregion
    }
}
