using System;
using NUnit.Framework;
using UnityEngine;
using UnityOSC;

namespace Theoriz.UnityOSC.Tests.Editor
{
    /// <summary>
    /// Measurement harness, not a pass/fail test — it reports how many garbage collections
    /// a fixed workload forces, so pack/unpack changes can be compared before and after.
    ///
    /// The Unity CPU Profiler cannot show this: unpacking runs on OSCServer's background
    /// receive thread, which is a plain Thread that never registers with the profiler, so
    /// its allocations appear in no Hierarchy row.
    ///
    /// Marked [Explicit] so it stays out of normal runs. To run it, open the Test Runner,
    /// select the test and press "Run Selected", then read the numbers from the console.
    /// </summary>
    [Explicit("Measurement harness — run manually and read the console.")]
    public class OSCAllocationBenchmark
    {
        // Large, because the metric is how many garbage collections the workload forces.
        // Collections are proportional to total bytes allocated, so for a fixed iteration
        // count, fewer collections means less garbage. Heap size (GC.GetTotalMemory) cannot
        // be used here: allocations are served from existing free space without the heap
        // growing, so it reports zero regardless of how much is allocated.
        private const int Iterations = 2000000;

        [Test]
        public void Measure_Unpack_BytesPerMessage()
        {
            // Matches the real workload: one float on a typical OCF address, received repeatedly.
            var template = new OSCMessage("/OCF/TestScript/myFloat");
            template.Append(0.5f);
            byte[] wire = template.BinaryData;

            for (int i = 0; i < 200; i++) OSCPacket.Unpack(wire);   // warm up

            int collectionsBefore = Settle();
            for (int i = 0; i < Iterations; i++) OSCPacket.Unpack(wire);
            Report("Unpack (receive path)", collectionsBefore);
        }

        [Test]
        public void Measure_Pack_BytesPerMessage()
        {
            for (int i = 0; i < 200; i++)
            {
                var warm = new OSCMessage("/OCF/TestScript/myFloat");
                warm.Append(0.5f);
                var unused = warm.BinaryData;
            }

            int collectionsBefore = Settle();
            for (int i = 0; i < Iterations; i++)
            {
                var msg = new OSCMessage("/OCF/TestScript/myFloat");
                msg.Append((float)i);
                var bytes = msg.BinaryData;
            }
            Report("Pack (send path)", collectionsBefore);
        }

        [Test]
        public void Measure_Pack_MultiArgument_BytesPerMessage()
        {
            // Four floats, as a Vector4 or Color widget sends — the case the type tag
            // and the per-value endian buffers hit hardest.
            int collectionsBefore = Settle();
            for (int i = 0; i < Iterations; i++)
            {
                var msg = new OSCMessage("/OCF/TestScript/myColor");
                msg.Append((float)i);
                msg.Append(0.25f);
                msg.Append(0.5f);
                msg.Append(1f);
                var bytes = msg.BinaryData;
            }
            Report("Pack, 4 floats", collectionsBefore);
        }

        /// <summary>
        /// Collects everything outstanding, then returns the collection count to measure against.
        /// </summary>
        private static int Settle()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            return GC.CollectionCount(0);
        }

        private static void Report(string label, int collectionsBefore)
        {
            int collections = GC.CollectionCount(0) - collectionsBefore;

            string warning = collections == 0
                ? "  [NO SIGNAL: raise Iterations until this is well above zero]"
                : "";

            Debug.Log(string.Format(
                "[OSC alloc] {0}: {1} gen0 collection(s) over {2:N0} messages" +
                " — lower is better, compare against the other build{3}",
                label, collections, Iterations, warning));
        }
    }
}
