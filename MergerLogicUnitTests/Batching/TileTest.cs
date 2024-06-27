using MergerLogic.Batching;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
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
        private byte[] _jpeg;
        #endregion

        [TestInitialize]
        public void beforeEach()
        {
            this._repository = new MockRepository(MockBehavior.Strict);
            this._jpeg = File.ReadAllBytes("image.jpeg");
        }

        #region CreateTile

        [TestMethod]
        [TestCategory("CreateTile")]
        [DataRow(null)]
        [DataRow()]
        public void CreateTileWithUnknownDataFormatFails(byte[] data)
        {
            Assert.ThrowsException<ValidationException>(() => new Tile(0, 0, 0, data));
            this._repository.VerifyAll();
        }

        [TestMethod]
        [TestCategory("CreateTile")]
        [DataRow(new byte[] { 0xFF, 0xD8, 0xFF, 0xDB })]
        [DataRow(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })]
        public void CreateTile(byte[] data)
        {
            Tile tile = new Tile(0, 0, 0, data);
            Assert.AreEqual(tile.GetImageBytes(), data);
            this._repository.VerifyAll();
        }

        #endregion
    }
}

