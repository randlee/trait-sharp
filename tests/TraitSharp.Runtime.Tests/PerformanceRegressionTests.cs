using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TraitSharp.Runtime.Tests
{
    [TestClass]
    public class PerformanceRegressionTests
    {
        /// <summary>
        /// Sanity check: trait layout cast must not allocate.
        /// </summary>
        [TestMethod]
        public void LayoutCast_ZeroAllocation()
        {
            var dp = new DataPoint { X = 1, Y = 2 };
            long before = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < 10000; i++)
            {
                ref readonly var coord = ref dp.AsCoordinate();
                _ = coord.X + coord.Y;
            }

            long after = GC.GetAllocatedBytesForCurrentThread();
            Assert.AreEqual(before, after, "Layout cast should not allocate!");
        }

        /// <summary>
        /// Sanity check: trait span iteration must not allocate.
        /// </summary>
        [TestMethod]
        public void TraitSpan_Iteration_ZeroAllocation()
        {
            var data = new Rectangle[100];
            long before = GC.GetAllocatedBytesForCurrentThread();

            var span = data.AsSpan().AsPoint2DSpan();
            int sum = 0;
            foreach (ref readonly var pos in span)
                sum += pos.X + pos.Y;

            long after = GC.GetAllocatedBytesForCurrentThread();
            Assert.AreEqual(before, after, "TraitSpan iteration should not allocate!");
        }
    }
}
