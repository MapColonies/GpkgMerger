using MergerLogic.DataTypes;
using MergerLogic.Utils;
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

        }

        [TestMethod]
        [TestCategory("RenderUrlTemplate")]
        [DynamicData(nameof(GenRenderUrlTemplateParams), DynamicDataSourceType.Method)]
        public void RenderUrlTemplate(Coord coords, bool useCoords, string expected)
        {
            var patternUtils = new PathPatternUtils("https://test.com/wmts/grid/{TileMatrix}/{TileCol}/{TileRow}.png");
            if (useCoords)
            {
                Assert.AreEqual(expected, patternUtils.RenderUrlTemplate(coords));
            }
            else
            {
                Assert.AreEqual(expected, patternUtils.RenderUrlTemplate(coords.X, coords.Y, coords.Z));
            }
        }


        #endregion
    }
}
