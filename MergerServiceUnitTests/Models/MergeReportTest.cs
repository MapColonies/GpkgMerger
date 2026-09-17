using MergerLogic.DataTypes;
using MergerLogic.ImageProcessing;
using MergerService.Models.Reports;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using System;

namespace MergerServiceUnitTests.Models
{
    [TestClass]
    [TestCategory("unit")]
    public class MergeReportTest
    {
        private static readonly Coord AnyCoord = new Coord(10, 1, 2);

        [TestMethod]
        public void RecordOutcome_TileNotProduced_CountsAsSkipped()
        {
            var report = new MergeReport("job1", "task1", "MERGE", "PNG", false);
            report.RecordOutcome(AnyCoord, existedBefore: true, tileProduced: false, new MergeStats(true, true));
            Assert.AreEqual(1, report.Skipped);
            Assert.AreEqual(0, report.Added + report.Merged + report.Replaced);
        }

        [TestMethod]
        public void RecordOutcome_NoSourceData_CountsAsSkipped()
        {
            var report = new MergeReport("job1", "task1", "MERGE", "PNG", false);
            report.RecordOutcome(AnyCoord, existedBefore: false, tileProduced: true, new MergeStats(true, false));
            Assert.AreEqual(1, report.Skipped);
            Assert.AreEqual(0, report.Added);
        }

        [TestMethod]
        public void RecordOutcome_NotExistedBefore_CountsAsAdded()
        {
            var report = new MergeReport("job1", "task1", "MERGE", "PNG", false);
            report.RecordOutcome(new Coord(10, 5, 6), existedBefore: false, tileProduced: true, new MergeStats(false, true));
            Assert.AreEqual(1, report.Added);
            Assert.AreEqual(1, report.AddedTiles.Count);
            Assert.AreEqual(new Coord(10, 5, 6), report.AddedTiles[0]);
        }

        [TestMethod]
        public void RecordOutcome_ExistedAndTargetBlended_CountsAsMerged()
        {
            var report = new MergeReport("job1", "task1", "MERGE", "PNG", false);
            report.RecordOutcome(AnyCoord, existedBefore: true, tileProduced: true, new MergeStats(true, true));
            Assert.AreEqual(1, report.Merged);
            Assert.AreEqual(0, report.Added);
        }

        [TestMethod]
        public void RecordOutcome_ExistedAndOpaqueSource_CountsAsReplaced()
        {
            var report = new MergeReport("job1", "task1", "MERGE", "PNG", false);
            report.RecordOutcome(AnyCoord, existedBefore: true, tileProduced: true, new MergeStats(false, true));
            Assert.AreEqual(1, report.Replaced);
            Assert.AreEqual(0, report.Merged);
        }

        [TestMethod]
        public void RecordOutcome_StoresCopyOfAddedCoord()
        {
            var report = new MergeReport("job1", "task1", "MERGE", "PNG", false);
            var coord = new Coord(10, 7, 8);
            report.RecordOutcome(coord, existedBefore: false, tileProduced: true, new MergeStats(false, true));

            // mutating the caller's coord must not affect the stored one
            coord.Y = 999;
            Assert.AreEqual(8, report.AddedTiles[0].Y);
        }

        [TestMethod]
        public void Counts_And_Percentages_Are_Computed()
        {
            var report = new MergeReport("job1", "task1", "MERGE", "PNG", false);
            report.RecordOutcome(new Coord(10, 1, 2), existedBefore: false, tileProduced: true, new MergeStats(false, true)); // added
            report.RecordOutcome(new Coord(10, 1, 3), existedBefore: false, tileProduced: true, new MergeStats(false, true)); // added
            report.RecordOutcome(AnyCoord, existedBefore: true, tileProduced: true, new MergeStats(true, true));   // merged
            report.RecordOutcome(AnyCoord, existedBefore: true, tileProduced: true, new MergeStats(false, true));  // replaced
            report.RecordOutcome(AnyCoord, existedBefore: false, tileProduced: false, new MergeStats(false, false)); // skipped

            report.Finalize(new DateTime(2026, 9, 15, 0, 0, 0, DateTimeKind.Utc),
                            new DateTime(2026, 9, 15, 0, 1, 0, DateTimeKind.Utc));

            Assert.AreEqual(2, report.Added);
            Assert.AreEqual(1, report.Merged);
            Assert.AreEqual(1, report.Replaced);
            Assert.AreEqual(1, report.Skipped);
            Assert.AreEqual(5, report.Total);
            Assert.AreEqual(60, report.DurationSeconds);
            Assert.AreEqual(40.0, report.AddedPercentage, 0.01);
        }

        [TestMethod]
        public void Json_Includes_AddedTiles_LogString_Excludes_Them()
        {
            var report = new MergeReport("job1", "task1", "MERGE", "PNG", false);
            report.RecordOutcome(new Coord(10, 1, 2), existedBefore: false, tileProduced: true, new MergeStats(false, true));
            report.Finalize(DateTime.UnixEpoch, DateTime.UnixEpoch);

            JObject json = JObject.Parse(report.ToJson());
            Assert.AreEqual(1, ((JArray)json["addedTiles"]).Count);

            Assert.IsFalse(report.ToLogString().Contains("addedTiles"));
            Assert.IsTrue(report.ToLogString().Contains("\"added\""));
        }

        [TestMethod]
        public void Percentages_Are_Zero_When_No_Tiles()
        {
            var report = new MergeReport("job1", "task1", "MERGE", "PNG", true);
            report.Finalize(DateTime.UnixEpoch, DateTime.UnixEpoch);
            Assert.AreEqual(0, report.Total);
            Assert.AreEqual(0.0, report.AddedPercentage, 0.01);
        }
    }
}
