using MergerLogic.Batching;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading.Tasks;

namespace MergerLogicUnitTests.Batching
{
    [TestClass]
    [TestCategory("unit")]
    [TestCategory("ZoomOrderGate")]
    public class ZoomOrderGateTest
    {
        private static readonly TimeSpan Blocked = TimeSpan.FromMilliseconds(200);
        private static readonly TimeSpan Released = TimeSpan.FromSeconds(10);

        [TestMethod]
        public void HigherZoomWaitsForEarlierLowerZoomBatch()
        {
            var gate = new ZoomOrderGate();
            long lower = gate.Register(16, 16);
            long higher = gate.Register(17, 17);

            var wait = Task.Run(() => gate.WaitForLowerZooms(higher));

            Assert.IsFalse(wait.Wait(Blocked), "a z17 batch started while an earlier z16 batch was still open");
            gate.Complete(lower);
            Assert.IsTrue(wait.Wait(Released), "the z17 batch did not start after the z16 batch completed");
        }

        [TestMethod]
        public void BatchSpanningZoomsWaitsForEarlierBatchOfItsLowerZoom()
        {
            var gate = new ZoomOrderGate();
            long first = gate.Register(16, 16);
            long spanning = gate.Register(16, 17);

            var wait = Task.Run(() => gate.WaitForLowerZooms(spanning));

            Assert.IsFalse(wait.Wait(Blocked));
            gate.Complete(first);
            Assert.IsTrue(wait.Wait(Released));
        }

        [TestMethod]
        public void SameZoomBatchesDoNotWait()
        {
            var gate = new ZoomOrderGate();
            gate.Register(18, 18);
            long second = gate.Register(18, 18);

            Assert.IsTrue(Task.Run(() => gate.WaitForLowerZooms(second)).Wait(Released));
        }

        [TestMethod]
        public void LaterLowerZoomBatchDoesNotBlockEarlierBatch()
        {
            var gate = new ZoomOrderGate();
            long first = gate.Register(17, 17);
            gate.Register(16, 16);

            Assert.IsTrue(Task.Run(() => gate.WaitForLowerZooms(first)).Wait(Released));
        }

        [TestMethod]
        public void BatchWaitsUntilEveryEarlierLowerZoomBatchCompletes()
        {
            var gate = new ZoomOrderGate();
            long a = gate.Register(15, 15);
            long b = gate.Register(16, 16);
            long c = gate.Register(17, 17);

            var wait = Task.Run(() => gate.WaitForLowerZooms(c));

            gate.Complete(a);
            Assert.IsFalse(wait.Wait(Blocked), "the z17 batch started while the z16 batch was still open");
            gate.Complete(b);
            Assert.IsTrue(wait.Wait(Released));
        }
    }
}
