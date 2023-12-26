﻿using MergerLogic.Batching;
using MergerLogic.DataTypes;
using MergerLogic.ImageProcessing;
using MergerLogic.Monitoring.Metrics;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;

namespace MergerLogicUnitTests.ImageProcessing
{
    [TestClass]
    [TestCategory("unit")]
    [TestCategory("imageProcessing")]
    public class TileScalerTest
    {

        #region mocks

        private MockRepository _mockRepository;

        private TileScaler _testTileScaler;

        #endregion

        [TestInitialize]
        public void BeforeEach()
        {
            this._mockRepository = new MockRepository(MockBehavior.Loose);

            var metricsProviderMock = this._mockRepository.Create<IMetricsProvider>();
            var tileScalerLoggerMock = this._mockRepository.Create<ILogger<TileScaler>>();

            this._testTileScaler = new TileScaler(metricsProviderMock.Object, tileScalerLoggerMock.Object);
        }

        #region Upscale


        public static IEnumerable<object[]> GetUpscaleTilesTestParameters()
        {
            yield return new object[] {
                "/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDACAWGBwYFCAcGhwkIiAmMFA0MCwsMGJGSjpQdGZ6eHJmcG6AkLicgIiuim5woNqirr7EztDOfJri8uDI8LjKzsb/2wBDASIkJDAqMF40NF7GhHCExsbGxsbGxsbGxsbGxsbGxsbGxsbGxsbGxsbGxsbGxsbGxsbGxsbGxsbGxsbGxsbGxsb/wAARCAEAAQADASIAAhEBAxEB/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAECAxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq8vP09fb3+Pn6/9oADAMBAAIRAxEAPwC5RRRWBoFFFFABRRRQAUUUUAFFFFABRRRQAtFFFABRRRQA6iiimAUUUUALRRRQAlFFFACUUUUAJRRRQAlFFFMAooopAFFFFIAooooAKKKKACiiigAooooAKKKKAFooooAKKKKACiiimAtFFFAC0UUUAFFFFACUUUUAJRRRQAUUUUAJRRRQAUUUUgCiiigAooooAKKKKACiiigAooooAWiiimAUUUUANooooAWiiigB1FFFABRRRQAUUUUAFFFFACUUUUAFFFFADaKKKACiiikAtFFFMAooopAFFFFMAooooAKKKKACiiigBKKKKACiiigBaKKKAFooooAKKKKACiiigAooooAKKKKAGUUUUAFFFFAC0UUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFAC0UUUgCiiimAUUUUAMooooAKKKKAFooooAKKKKACiiigAooooAKKKKACiiigB1FFFACUUUUAJRRRQAUUUUgFooooAKKKKAFooooAKKKKAG0UUUwCiiigAooooAWiiigAooooAKKKKACiiigAooooAWiiigBKKKKACiiikAUUUUALRRRQAUUUUAFFFFACUUUUAFFFFUAUUUUALRRRSAKKKKACiiigAooooAKKKKACiiigBKKKKACiiigAooooAWiiikAtFFFABRRRQAlFFFACUUUUAOoooqgCiiigAooooAKKKKQDaKKKACiiigBaKKKAFooooAKKKKAG0UUUAJRRRQA6iiikA+iiigAooooAZRRRQA2iiigB9FFFUAUUUUAFFFFACUUUUAJRRRSAKKKKACiiigBaKKKACiiigAooooAWiiigBKKKKAEooooAKKKKQBRRRQAtFFFMAooooAWiiigAooooASiiigBKKKKACiiigAooooAWiiigAooooAWiiigBaKKKAEooooAZRRRQAlFFFABRRRQAtFFFADqKKKACiiigBaKKKAEooooAKKKKACiiigAooooAKKKKACiiigAooooAKKKKACiiigBKKKKAGUUUUANooooAbRRRQBYooooAKKKKAFooooAKKKKACiiigAooooAKKKKACiiigBtFFFABRRRQAlFFFACUUUUANooooAbRRRQA2iiigBKKKKALVFFFABRRRQAtFFFABRRRQAUUUUAFFFFABRRRQAUUUUAJRRRQAUUUUAJRRRQAyiiigCOiiigBtFFFMB1FFFAD6KKKAJaKKKQBRRRQAtFFFABRRRQAUUUUAFFFFABRRRQAlFFFABRRRQAUUUUANooopgMooooAZRRRQAUUUUAPooooAfRRRQA+iiikAUUUUAFFFFABRRRQAUUUUAJRRRQAUUUUAJRRRQAUUUUANooooAZRRRQAlFFFMAooooAdRRRQAUUUUAFFFFAH//2Q==",
                new Coord(9, 0, 0),
                new Coord(10, 0, 0),
                "/9j/4AAQSkZJRgABAQAAAAAAAAD/2wBDAAMCAgICAgMCAgIDAwMDBAYEBAQEBAgGBgUGCQgKCgkICQkKDA8MCgsOCwkJDRENDg8QEBEQCgwSExIQEw8QEBD/2wBDAQMDAwQDBAgEBAgQCwkLEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBD/wAARCAEAAQADAREAAhEBAxEB/8QAFwABAQEBAAAAAAAAAAAAAAAAAAQFCP/EABQQAQAAAAAAAAAAAAAAAAAAAAD/xAAXAQEBAQEAAAAAAAAAAAAAAAAAAQQG/8QAFhEBAQEAAAAAAAAAAAAAAAAAABEB/9oADAMBAAIRAxEAPwDotw7rQAAAAAABIBAIBuAzaCAADJAAAAAAABWAAACsAAABrAAAAAAAAAABNBm0EAAGSAAAAAAACsAAAFYAAADWAAAAAAAAAACaDNoIAAMkAAAAAAAFYAAAKwAAAGsAAAAAAAAAAE0GfcBIBAIMlAAAAAAABWAAACsAAABrAAAAAAAAAAAEiQCAQDcEjNoIAAAAAAAAAAKwAAAVtYAAAAAAAAAAAkAAATRIzaCAAAAAAAAAACsAAAFbWAAAAAAAAAAAJAAAE0SM2ggAAAAAAAAAArAAABW1UCgUCgUCgUCgUCgUCiQoFAoG6JGbQQAAAAAAAAAAVgAAALQKBQKKygUCgUCgUCgUSFAoFAokQAAAAAAAAAAAVgAAAAAAArAAAAAAABIAAACQAAAAAAAAAAAFYAAAAAAAKwAAAAAAASAAAAkAAAAAAAAAAABWAAAAAAACsAAAAAAAEgAAAJAAAAAAAAAAAAVgAAAAAAAAAAArAAABIAAAAAAAAAAAAAAAAAAAAAAAAAAACsAAAEgAAAAAAAAAAAAAAAAAAAAAAAAAAAKwAAASAAAAAAAAAAAAAAAAAAAAAAAAAAAArAAABIAAAAAAAAAAAAAAAAAAAAAAAAAAACsAAAAAAAAAAAAAAAAAAAAAAAEgAAAAAAAKwAAAAAAAAAAAAAAAAAAAAAAASAAAAAAAArAAAAAAAAAAAAAAAAAAAAAAABIAAAAAAACsAAAAAAAAAAAAAAAAAAAAAAAEgAAAAAAAKwAAAAAAAAAAAAAAAAAAAAAAASAAAAAAAArAAAAAAAAAAAAAAAAAAAAAAABIAAAAAAACsAAAAAAAAAAAAAAAAAAAAAAAEgAAAAAAAKwAAAAAAAAAAAAAAAAAAAAAAASAAAAAAAArAAAAAAAAAAAAAAAAAAABIAAAAAAAAAAACsAAAAAAAAAAAAAAAAAAAEgAAAAAAAAAAAKwAAAAAAAAAAAAAAAAAAASAAAAAAAAAAAArAAAAAAAAAAAAAAAAAAABIAAACsAAAAAAAAAAAAAAAAAAAEgAAAAAAAJAAAAawAAAAAAAAAAAAAAAAAAAJAAAAAAAASAAAA1gAAAAAAAAAAAAAAAAAAASAAAAAAAAkAAABrAAAAAAAAAAAAAAAAAAAAkAAAAAAABIAAAD//Z",
            };
        }
        [TestMethod]
        [TestCategory("Upscale")]
        [DynamicData(nameof(GetUpscaleTilesTestParameters), DynamicDataSourceType.Method)]
        public void Upscale(string testTileBytesBase64, Coord testBaseTileCoord, Coord testTargetCoord, string expectedTileBytesBase64)
        {
            var testTile = new Tile(testBaseTileCoord, Convert.FromBase64String(testTileBytesBase64));
            var resultTile = this._testTileScaler.Upscale(testTile, testTargetCoord);

            Assert.IsNotNull(resultTile);
            Assert.AreEqual(expectedTileBytesBase64, Convert.ToBase64String(resultTile.GetImageBytes()));
        }

        #endregion
    }
}
