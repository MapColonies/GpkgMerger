using ImageMagick;
using MergerLogic.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;

namespace MergerLogicUnitTests.Utils
{
  [TestClass]
  [TestCategory("unit")]
  [TestCategory("imageProcessing")]
  [TestCategory("ImageUtils")]
  [DeploymentItem(@"../../../Utils/TestData")]
  public class ImageUtilsTest
  {
    #region IsTransparent

    public static IEnumerable<object[]> GetIsTransparentTestParameters()
    {
      yield return new object[] {
        File.ReadAllBytes("full_transparent.png"),
        true,
      };

      yield return new object[] {
        File.ReadAllBytes("partial_transparent.png"),
        true,
      };

      yield return new object[] {
        File.ReadAllBytes("no_transparency.png"),
        false,
      };

      yield return new object[] {
        File.ReadAllBytes("no_transparency.jpeg"),
        false,
      };
    }

    [TestMethod]
    [DynamicData(nameof(GetIsTransparentTestParameters), DynamicDataSourceType.Method)]
    public void IsTransparent(byte[] imageBytes, bool expectedResult)
    {
      using (var image = new MagickImage(imageBytes))
      {
        var result = ImageUtils.IsTransparent(image);
        Assert.AreEqual(expectedResult, result);
      }
    }

    #endregion

    #region IsFullyTransparent

    public static IEnumerable<object[]> GetIsFullyTransparentTestParameters()
    {
      yield return new object[] {
        File.ReadAllBytes("full_transparent.png"),
        true,
      };

      yield return new object[] {
        File.ReadAllBytes("partial_transparent.png"),
        false,
      };

      yield return new object[] {
        File.ReadAllBytes("no_transparency.png"),
        false,
      };

      yield return new object[] {
        File.ReadAllBytes("no_transparency.jpeg"),
        false,
      };
    }

    [TestMethod]
    [DynamicData(nameof(GetIsFullyTransparentTestParameters), DynamicDataSourceType.Method)]
    public void IsFullyTransparent(byte[] imageBytes, bool expectedResult)
    {
      using (var image = new MagickImage(imageBytes))
      {
        var result = ImageUtils.IsFullyTransparent(image);
        Assert.AreEqual(expectedResult, result);
      }
    }

    #endregion
  }
}
