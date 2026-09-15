using MergerLogic.DataTypes;
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
        [TestMethod]
        public void Counts_And_Percentages_Are_Computed()
        {
            var report = new MergeReport("job1", "task1", "MERGE", "PNG", false);
            report.RecordAdded(new Coord(10, 1, 2));
            report.RecordAdded(new Coord(10, 1, 3));
            report.RecordMerged();
            report.RecordReplaced();
            report.RecordSkipped();

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
            report.RecordAdded(new Coord(10, 1, 2));
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
