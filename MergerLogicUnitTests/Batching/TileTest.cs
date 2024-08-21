using MergerLogic.Batching;
using MergerLogic.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
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
        private Mock<IConfigurationManager> _configurationManagerMock;
        #endregion
        private readonly Times anyNumberOfTimes = Times.AtMost(int.MaxValue);

        [TestInitialize]
        public void beforeEach()
        {
            this._repository = new MockRepository(MockBehavior.Strict);
            this._configurationManagerMock = this._repository.Create<IConfigurationManager>();

            this._configurationManagerMock.Setup(configManager => configManager.GetConfiguration<int>("GENERAL", "allowedPixelSize"))
                .Returns(256).Verifiable(anyNumberOfTimes);
        }

        #region CreateTile

        public static IEnumerable<object[]> GenCreateTileUnknownFormatParams()
        {
#nullable disable
            yield return new object[] { null };
#nullable enable
            yield return new object[] { File.ReadAllBytes("image.gif") };
        }

        [TestMethod]
        [TestCategory("CreateTile")]
        [DynamicData(nameof(GenCreateTileUnknownFormatParams), DynamicDataSourceType.Method)]
        public void CreateTileWithUnknownDataFormatFails(byte[] data)
        {
            Assert.ThrowsException<ValidationException>(() => new Tile(this._configurationManagerMock.Object, 0, 0, 0, data));
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
            Tile tile = new Tile(this._configurationManagerMock.Object, 0, 0, 0, data);
            Assert.AreEqual(tile.GetImageBytes(), data);
            this._repository.VerifyAll();
        }

        #endregion

        #region imageDimensions
        public static IEnumerable<object[]> ValidTilesSizeTestParameters()
        {
            yield return new object[] {
                File.ReadAllBytes("no_transparency.jpeg"),
                (256, 256)
            };
            yield return new object[] {
                File.ReadAllBytes("no_transparency.png"),
                (256, 256)
            };
        }
        public static IEnumerable<object[]> InvalidTilesSizeTestParameters()
        {
            yield return new object[] {
                File.ReadAllBytes("100x100.jpeg"),
                (100, 100)
            };
            yield return new object[] {
                File.ReadAllBytes("100x100.png"),
                (100, 100)
            };
            yield return new object[] {
                File.ReadAllBytes("100x256.jpeg"),
                (100, 256)
            };
            yield return new object[] {
                File.ReadAllBytes("100x256.png"),
                (100, 256)
            };
            yield return new object[] {
                File.ReadAllBytes("256x100.jpeg"),
                (256, 100)
            };
            yield return new object[] {
                File.ReadAllBytes("256x100.png"),
                (256, 100)
            };
        }

        [TestMethod]
        [DynamicData(nameof(ValidTilesSizeTestParameters), DynamicDataSourceType.Method)]
        public void IsAcceptsValidTileSize(byte[] imageBytes, (int, int) dimensions)
        {
            Tile tile = new Tile(this._configurationManagerMock.Object, 0, 0, 0, imageBytes);
            Assert.AreEqual((tile.Width, tile.Height), dimensions);
        }

        [TestMethod]
        [DynamicData(nameof(InvalidTilesSizeTestParameters), DynamicDataSourceType.Method)]
        [ExpectedException(typeof(ArgumentException))]
        public void IsRejectsInvalidTileSize(byte[] imageBytes, (int, int) dimensions)
        {
            new Tile(this._configurationManagerMock.Object, 0, 0, 0, imageBytes);
        }
        #endregion
    }
}

