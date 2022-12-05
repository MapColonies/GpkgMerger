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
    [TestCategory("PathPatternUtils")]
    public class PathPatternUtilsTest
    {
        #region CompilePattern

        [TestMethod]
        [TestCategory("CompilePattern")]
        [DataRow("https://test.com!@#$%{^}&*()?,<>[]=\\zxy{z}/{x}/{y}.png", "https://test.com!@#$%{^}&*()?,<>[]=\\zxy1/2/3.png")]
        [DataRow("https://test.com!@#$%^{a}&*()?,<>[]=\\ZXY{Z}/{X}/{Y}.png", "https://test.com!@#$%^{a}&*()?,<>[]=\\ZXY1/2/3.png")]
        [DataRow("https://test.com!@#$%^{a}&*()?,<>[]=\\TileCol TileRow TileMatrix{TileMatrix}-{TileCol}-{TileRow}.png",
            "https://test.com!@#$%^{a}&*()?,<>[]=\\TileCol TileRow TileMatrix1-2-3.png")]
        [DataRow("https://test.com!@#$%{^}&*()?,<>[]=\\zxy{z}_{x}_{y}", "https://test.com!@#$%{^}&*()?,<>[]=\\zxy1_2_3")]
        [DataRow("https://test.com!@#$%^{a}&*()?,<>[]=\\ZXY{Z}+{X}+{Y}", "https://test.com!@#$%^{a}&*()?,<>[]=\\ZXY1+2+3")]
        [DataRow("https://test.com!@#$%^{a}&*()?,<>[]=\\TileCol TileRow{TileMatrix};{TileCol};{TileRow}",
            "https://test.com!@#$%^{a}&*()?,<>[]=\\TileCol TileRow1;2;3")]
        [DataRow("https://test.com!@#$%{^}&*()?,<>[]=\\zxy{z}:{X}:{TileRow}.png", "https://test.com!@#$%{^}&*()?,<>[]=\\zxy1:2:3.png")]
        public void CompilePatternWithValidPattern(string pattern, string expected)
        {
            var patternUtils = new PathPatternUtils(pattern);
            Assert.AreEqual(expected, patternUtils.RenderUrlTemplate(2, 3, 1));
        }

        [TestMethod]
        [TestCategory("CompilePattern")]
        [DataRow("{X}")]
        [DataRow("{X}{Y}{Z}")]
        [DataRow("aaa{X}{Y}{Z}.png")]
        [DataRow("aaa/{X}/{Y}/{Z}.png{z}")]
        public void CompilePatternWithInvalidPattern(string pattern)
        {
            Assert.ThrowsException<Exception>(() => new PathPatternUtils(pattern));
        }

        #endregion

        #region RenderUrlTemplate

        public static IEnumerable<object[]> GenRenderUrlTemplateParams()
        {
            return DynamicDataGenerator.GeneratePrams(new object[][]
            {
                new object[]
                {
                    new TestExpected<Coord,string>(new Coord(0,0,0),"https://test.com/wmts/grid/0/0/0.png"),
                    new TestExpected<Coord,string>(new Coord(21,3494272,2097152),"https://test.com/wmts/grid/21/3494272/2097152.png"),
                    new TestExpected<Coord,string>(new Coord(7,126,784),"https://test.com/wmts/grid/7/126/784.png")
                }, //test data
                new object[] { true, false} //use coords
            });
        }

        [TestMethod]
        [TestCategory("RenderUrlTemplate")]
        [DynamicData(nameof(GenRenderUrlTemplateParams), DynamicDataSourceType.Method)]
        public void RenderUrlTemplate(TestExpected<Coord, string> testData, bool useCoords)
        {
            var patternUtils = new PathPatternUtils("https://test.com/wmts/grid/{TileMatrix}/{TileCol}/{TileRow}.png");
            if (useCoords)
            {
                Assert.AreEqual(testData.ExpectedData, patternUtils.RenderUrlTemplate(testData.TestData));
            }
            else
            {
                Assert.AreEqual(testData.ExpectedData, patternUtils.RenderUrlTemplate(testData.TestData.X, testData.TestData.Y, testData.TestData.Z));
            }
        }


        #endregion
    }
}
