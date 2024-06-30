using MergerLogic.Batching;
using MergerLogicUnitTests.testUtils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;

namespace MergerLogicUnitTests.Clients
{
    [TestClass]
    [TestCategory("unit")]
    [TestCategory("Tile")]
    [DeploymentItem(@"../../../Batching/TestImages")]
    public class TilesTest
    {
        #region mocks
        private MockRepository _repository;
        #endregion

        [TestInitialize]
        public void beforeEach()
        {
            this._repository = new MockRepository(MockBehavior.Strict);
        }

        #region CreateTile

        public static IEnumerable<object[]> GenCreateTileUnknownFormatParams()
        {
            yield return new object[] { null };
            yield return new object[] { File.ReadAllBytes("image.gif") };
        }

        [TestMethod]
        [TestCategory("CreateTile")]
        [DynamicData(nameof(GenCreateTileUnknownFormatParams), DynamicDataSourceType.Method)]
        public void CreateTileWithUnknownDataFormatFails(byte[] data)
        {
            Assert.ThrowsException<ValidationException>(() => new Tile(0, 0, 0, data));
            this._repository.VerifyAll();
        }

        public static IEnumerable<object[]> GenCreateTileParams()
        {
            yield return new object[] { File.ReadAllBytes("image.jpeg") };
            yield return new object[] { File.ReadAllBytes("image.png") };
        }

        [TestMethod]
        [TestCategory("CreateTile")]
        [DynamicData(nameof(GenCreateTileParams), DynamicDataSourceType.Method)]
        public void CreateTile(byte[] data)
        {
            Tile tile = new Tile(0, 0, 0, data);
            Assert.AreEqual(tile.GetImageBytes(), data);
            this._repository.VerifyAll();
        }

        #endregion
    }
}

