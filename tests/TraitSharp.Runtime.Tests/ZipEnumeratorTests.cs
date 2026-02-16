using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TraitSharp.Runtime;

namespace TraitSharp.Runtime.Tests
{
    [TestClass]
    public class ZipEnumeratorTests
    {
        // Simple test structs that simulate trait layouts at different offsets
        [StructLayout(LayoutKind.Sequential)]
        private struct TestSource
        {
            public int A;
            public float B;
            public short C;
            public short _pad;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LayoutA
        {
            public int A;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LayoutB
        {
            public float B;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LayoutC
        {
            public short C;
        }

        private static ReadOnlyTraitSpan<TLayout> CreateReadOnlySpan<TLayout>(TestSource[] array, int byteOffset)
            where TLayout : unmanaged
        {
            ref byte baseRef = ref Unsafe.As<TestSource, byte>(ref array[0]);
            ref byte offsetRef = ref Unsafe.AddByteOffset(ref baseRef, (nint)byteOffset);
            return new ReadOnlyTraitSpan<TLayout>(ref offsetRef, Unsafe.SizeOf<TestSource>(), array.Length);
        }

        private static TraitSpan<TLayout> CreateMutableSpan<TLayout>(TestSource[] array, int byteOffset)
            where TLayout : unmanaged
        {
            ref byte baseRef = ref Unsafe.As<TestSource, byte>(ref array[0]);
            ref byte offsetRef = ref Unsafe.AddByteOffset(ref baseRef, (nint)byteOffset);
            return new TraitSpan<TLayout>(ref offsetRef, Unsafe.SizeOf<TestSource>(), array.Length);
        }

        private TestSource[] CreateTestArray(int count)
        {
            var array = new TestSource[count];
            for (int i = 0; i < count; i++)
            {
                array[i] = new TestSource
                {
                    A = i * 10,
                    B = i * 1.5f,
                    C = (short)(i * 3)
                };
            }
            return array;
        }

        // ======== ReadOnly Zip2 Tests ========

        [TestMethod]
        public void ReadOnlyZipPairs_Foreach_YieldsCorrectPairs()
        {
            var array = CreateTestArray(5);
            var spanA = CreateReadOnlySpan<LayoutA>(array, 0);  // offset 0 for A
            var spanB = CreateReadOnlySpan<LayoutB>(array, 4);  // offset 4 for B (after int A)

            int index = 0;
            foreach (var pair in spanA.Zip(spanB))
            {
                Assert.AreEqual(array[index].A, pair.First.A);
                Assert.AreEqual(array[index].B, pair.Second.B);
                index++;
            }
            Assert.AreEqual(5, index);
        }

        [TestMethod]
        public void ReadOnlyZipPairs_EmptySpans_NoIterations()
        {
            var array = Array.Empty<TestSource>();
            var spanA = new ReadOnlyTraitSpan<LayoutA>(ref Unsafe.NullRef<byte>(), Unsafe.SizeOf<TestSource>(), 0);
            var spanB = new ReadOnlyTraitSpan<LayoutB>(ref Unsafe.NullRef<byte>(), Unsafe.SizeOf<TestSource>(), 0);

            int count = 0;
            foreach (var pair in spanA.Zip(spanB))
            {
                count++;
            }
            Assert.AreEqual(0, count);
        }

        [TestMethod]
        public void ReadOnlyZipPairs_SingleElement()
        {
            var array = CreateTestArray(1);
            var spanA = CreateReadOnlySpan<LayoutA>(array, 0);
            var spanB = CreateReadOnlySpan<LayoutB>(array, 4);

            int count = 0;
            foreach (var pair in spanA.Zip(spanB))
            {
                Assert.AreEqual(0, pair.First.A);
                Assert.AreEqual(0f, pair.Second.B);
                count++;
            }
            Assert.AreEqual(1, count);
        }

        [TestMethod]
        public void ReadOnlyZipPairs_LengthMismatch_Throws()
        {
            var array = CreateTestArray(5);
            var spanA = CreateReadOnlySpan<LayoutA>(array, 0);
            var spanBShort = CreateReadOnlySpan<LayoutB>(array, 4).Slice(0, 3);

            try
            {
                spanA.Zip(spanBShort);
                Assert.Fail("Expected ArgumentException");
            }
            catch (ArgumentException) { }
        }

        [TestMethod]
        public void ReadOnlyZipPairs_StrideMismatch_Throws()
        {
            var array = CreateTestArray(5);
            var spanA = CreateReadOnlySpan<LayoutA>(array, 0);

            // Create a span with different stride (sizeof(LayoutB) instead of sizeof(TestSource))
            var bArray = new LayoutB[5];
            ref byte bRef = ref Unsafe.As<LayoutB, byte>(ref bArray[0]);
            var spanBDifferentStride = new ReadOnlyTraitSpan<LayoutB>(ref bRef, Unsafe.SizeOf<LayoutB>(), 5);

            try
            {
                spanA.Zip(spanBDifferentStride);
                Assert.Fail("Expected ArgumentException");
            }
            catch (ArgumentException) { }
        }

        [TestMethod]
        public void ReadOnlyZipPairs_Length_ReturnsCorrectCount()
        {
            var array = CreateTestArray(7);
            var spanA = CreateReadOnlySpan<LayoutA>(array, 0);
            var spanB = CreateReadOnlySpan<LayoutB>(array, 4);

            var zip = spanA.Zip(spanB);
            Assert.AreEqual(7, zip.Length);
            Assert.IsFalse(zip.IsEmpty);
        }

        // ======== Mutable Zip2 Tests ========

        [TestMethod]
        public void MutableZipPairs_Foreach_CanModifyElements()
        {
            var array = CreateTestArray(3);
            var spanA = CreateMutableSpan<LayoutA>(array, 0);
            var spanB = CreateMutableSpan<LayoutB>(array, 4);

            foreach (var pair in spanA.Zip(spanB))
            {
                pair.First.A += 100;
                pair.Second.B += 100f;
            }

            // Verify modifications applied to underlying array
            Assert.AreEqual(100, array[0].A);
            Assert.AreEqual(110, array[1].A);
            Assert.AreEqual(120, array[2].A);
            Assert.AreEqual(100f, array[0].B);
            Assert.AreEqual(101.5f, array[1].B);
            Assert.AreEqual(103f, array[2].B);
        }

        [TestMethod]
        public void MutableZipPairs_LengthMismatch_Throws()
        {
            var array = CreateTestArray(5);
            var spanA = CreateMutableSpan<LayoutA>(array, 0);
            var spanBShort = CreateMutableSpan<LayoutB>(array, 4).Slice(0, 3);

            try
            {
                spanA.Zip(spanBShort);
                Assert.Fail("Expected ArgumentException");
            }
            catch (ArgumentException) { }
        }

        // ======== ReadOnly Zip3 Tests ========

        [TestMethod]
        public void ReadOnlyZipTriples_Foreach_YieldsCorrectTriples()
        {
            var array = CreateTestArray(4);
            var spanA = CreateReadOnlySpan<LayoutA>(array, 0);   // offset 0
            var spanB = CreateReadOnlySpan<LayoutB>(array, 4);   // offset 4
            var spanC = CreateReadOnlySpan<LayoutC>(array, 8);   // offset 8

            int index = 0;
            foreach (var triple in spanA.Zip(spanB, spanC))
            {
                Assert.AreEqual(array[index].A, triple.First.A);
                Assert.AreEqual(array[index].B, triple.Second.B);
                Assert.AreEqual(array[index].C, triple.Third.C);
                index++;
            }
            Assert.AreEqual(4, index);
        }

        [TestMethod]
        public void ReadOnlyZipTriples_EmptySpans_NoIterations()
        {
            var spanA = new ReadOnlyTraitSpan<LayoutA>(ref Unsafe.NullRef<byte>(), Unsafe.SizeOf<TestSource>(), 0);
            var spanB = new ReadOnlyTraitSpan<LayoutB>(ref Unsafe.NullRef<byte>(), Unsafe.SizeOf<TestSource>(), 0);
            var spanC = new ReadOnlyTraitSpan<LayoutC>(ref Unsafe.NullRef<byte>(), Unsafe.SizeOf<TestSource>(), 0);

            int count = 0;
            foreach (var triple in spanA.Zip(spanB, spanC))
            {
                count++;
            }
            Assert.AreEqual(0, count);
        }

        [TestMethod]
        public void ReadOnlyZipTriples_LengthMismatch_Throws()
        {
            var array = CreateTestArray(5);
            var spanA = CreateReadOnlySpan<LayoutA>(array, 0);
            var spanB = CreateReadOnlySpan<LayoutB>(array, 4);
            var spanCShort = CreateReadOnlySpan<LayoutC>(array, 8).Slice(0, 3);

            try
            {
                spanA.Zip(spanB, spanCShort);
                Assert.Fail("Expected ArgumentException");
            }
            catch (ArgumentException) { }
        }

        [TestMethod]
        public void ReadOnlyZipTriples_Length_ReturnsCorrectCount()
        {
            var array = CreateTestArray(6);
            var spanA = CreateReadOnlySpan<LayoutA>(array, 0);
            var spanB = CreateReadOnlySpan<LayoutB>(array, 4);
            var spanC = CreateReadOnlySpan<LayoutC>(array, 8);

            var zip = spanA.Zip(spanB, spanC);
            Assert.AreEqual(6, zip.Length);
            Assert.IsFalse(zip.IsEmpty);
        }

        // ======== Mutable Zip3 Tests ========

        [TestMethod]
        public void MutableZipTriples_Foreach_CanModifyElements()
        {
            var array = CreateTestArray(3);
            var spanA = CreateMutableSpan<LayoutA>(array, 0);
            var spanB = CreateMutableSpan<LayoutB>(array, 4);
            var spanC = CreateMutableSpan<LayoutC>(array, 8);

            foreach (var triple in spanA.Zip(spanB, spanC))
            {
                triple.First.A += 1000;
                triple.Second.B += 1000f;
                triple.Third.C += 1000;
            }

            Assert.AreEqual(1000, array[0].A);
            Assert.AreEqual(1010, array[1].A);
            Assert.AreEqual(1020, array[2].A);
            Assert.AreEqual(1000f, array[0].B);
            Assert.AreEqual(1000, array[0].C);  // C starts at 0*3=0, +1000=1000
            Assert.AreEqual(1003, array[1].C);  // C starts at 1*3=3, +1000=1003
        }

        [TestMethod]
        public void MutableZipTriples_LengthMismatch_Throws()
        {
            var array = CreateTestArray(5);
            var spanA = CreateMutableSpan<LayoutA>(array, 0);
            var spanB = CreateMutableSpan<LayoutB>(array, 4);
            var spanCShort = CreateMutableSpan<LayoutC>(array, 8).Slice(0, 2);

            try
            {
                spanA.Zip(spanB, spanCShort);
                Assert.Fail("Expected ArgumentException");
            }
            catch (ArgumentException) { }
        }

        // ======== QA: Mutable Zip2 Stride/Empty/Single/Length Tests ========

        [TestMethod]
        public void MutableZipPairs_StrideMismatch_Throws()
        {
            var array = CreateTestArray(5);
            var spanA = CreateMutableSpan<LayoutA>(array, 0);

            var bArray = new LayoutB[5];
            ref byte bRef = ref Unsafe.As<LayoutB, byte>(ref bArray[0]);
            var spanBDifferentStride = new TraitSpan<LayoutB>(ref bRef, Unsafe.SizeOf<LayoutB>(), 5);

            try
            {
                spanA.Zip(spanBDifferentStride);
                Assert.Fail("Expected ArgumentException");
            }
            catch (ArgumentException) { }
        }

        [TestMethod]
        public void MutableZipPairs_EmptySpans_NoIterations()
        {
            var spanA = new TraitSpan<LayoutA>(ref Unsafe.NullRef<byte>(), Unsafe.SizeOf<TestSource>(), 0);
            var spanB = new TraitSpan<LayoutB>(ref Unsafe.NullRef<byte>(), Unsafe.SizeOf<TestSource>(), 0);

            int count = 0;
            foreach (var pair in spanA.Zip(spanB))
            {
                count++;
            }
            Assert.AreEqual(0, count);
        }

        [TestMethod]
        public void MutableZipPairs_SingleElement()
        {
            var array = CreateTestArray(1);
            var spanA = CreateMutableSpan<LayoutA>(array, 0);
            var spanB = CreateMutableSpan<LayoutB>(array, 4);

            int count = 0;
            foreach (var pair in spanA.Zip(spanB))
            {
                pair.First.A = 999;
                pair.Second.B = 99.9f;
                count++;
            }
            Assert.AreEqual(1, count);
            Assert.AreEqual(999, array[0].A);
            Assert.AreEqual(99.9f, array[0].B);
        }

        [TestMethod]
        public void MutableZipPairs_Length_ReturnsCorrectCount()
        {
            var array = CreateTestArray(7);
            var spanA = CreateMutableSpan<LayoutA>(array, 0);
            var spanB = CreateMutableSpan<LayoutB>(array, 4);

            var zip = spanA.Zip(spanB);
            Assert.AreEqual(7, zip.Length);
            Assert.IsFalse(zip.IsEmpty);
        }

        // ======== QA: Mutable Zip3 Stride/Empty/Single/Length Tests ========

        [TestMethod]
        public void MutableZipTriples_StrideMismatch_Throws()
        {
            var array = CreateTestArray(5);
            var spanA = CreateMutableSpan<LayoutA>(array, 0);
            var spanB = CreateMutableSpan<LayoutB>(array, 4);

            var cArray = new LayoutC[5];
            ref byte cRef = ref Unsafe.As<LayoutC, byte>(ref cArray[0]);
            var spanCDifferentStride = new TraitSpan<LayoutC>(ref cRef, Unsafe.SizeOf<LayoutC>(), 5);

            try
            {
                spanA.Zip(spanB, spanCDifferentStride);
                Assert.Fail("Expected ArgumentException");
            }
            catch (ArgumentException) { }
        }

        [TestMethod]
        public void MutableZipTriples_EmptySpans_NoIterations()
        {
            var spanA = new TraitSpan<LayoutA>(ref Unsafe.NullRef<byte>(), Unsafe.SizeOf<TestSource>(), 0);
            var spanB = new TraitSpan<LayoutB>(ref Unsafe.NullRef<byte>(), Unsafe.SizeOf<TestSource>(), 0);
            var spanC = new TraitSpan<LayoutC>(ref Unsafe.NullRef<byte>(), Unsafe.SizeOf<TestSource>(), 0);

            int count = 0;
            foreach (var triple in spanA.Zip(spanB, spanC))
            {
                count++;
            }
            Assert.AreEqual(0, count);
        }

        [TestMethod]
        public void MutableZipTriples_SingleElement()
        {
            var array = CreateTestArray(1);
            var spanA = CreateMutableSpan<LayoutA>(array, 0);
            var spanB = CreateMutableSpan<LayoutB>(array, 4);
            var spanC = CreateMutableSpan<LayoutC>(array, 8);

            int count = 0;
            foreach (var triple in spanA.Zip(spanB, spanC))
            {
                triple.First.A = 777;
                triple.Second.B = 77.7f;
                triple.Third.C = 77;
                count++;
            }
            Assert.AreEqual(1, count);
            Assert.AreEqual(777, array[0].A);
            Assert.AreEqual(77.7f, array[0].B);
            Assert.AreEqual(77, array[0].C);
        }

        [TestMethod]
        public void MutableZipTriples_Length_ReturnsCorrectCount()
        {
            var array = CreateTestArray(6);
            var spanA = CreateMutableSpan<LayoutA>(array, 0);
            var spanB = CreateMutableSpan<LayoutB>(array, 4);
            var spanC = CreateMutableSpan<LayoutC>(array, 8);

            var zip = spanA.Zip(spanB, spanC);
            Assert.AreEqual(6, zip.Length);
            Assert.IsFalse(zip.IsEmpty);
        }

        // ======== QA: ReadOnly Zip3 Stride/Single/SecondMismatch Tests ========

        [TestMethod]
        public void ReadOnlyZipTriples_StrideMismatch_Throws()
        {
            var array = CreateTestArray(5);
            var spanA = CreateReadOnlySpan<LayoutA>(array, 0);
            var spanB = CreateReadOnlySpan<LayoutB>(array, 4);

            var cArray = new LayoutC[5];
            ref byte cRef = ref Unsafe.As<LayoutC, byte>(ref cArray[0]);
            var spanCDifferentStride = new ReadOnlyTraitSpan<LayoutC>(ref cRef, Unsafe.SizeOf<LayoutC>(), 5);

            try
            {
                spanA.Zip(spanB, spanCDifferentStride);
                Assert.Fail("Expected ArgumentException");
            }
            catch (ArgumentException) { }
        }

        [TestMethod]
        public void ReadOnlyZipTriples_SingleElement()
        {
            var array = CreateTestArray(1);
            var spanA = CreateReadOnlySpan<LayoutA>(array, 0);
            var spanB = CreateReadOnlySpan<LayoutB>(array, 4);
            var spanC = CreateReadOnlySpan<LayoutC>(array, 8);

            int count = 0;
            foreach (var triple in spanA.Zip(spanB, spanC))
            {
                Assert.AreEqual(0, triple.First.A);
                Assert.AreEqual(0f, triple.Second.B);
                Assert.AreEqual((short)0, triple.Third.C);
                count++;
            }
            Assert.AreEqual(1, count);
        }

        [TestMethod]
        public void ReadOnlyZipTriples_OnlySecondLengthMismatch_Throws()
        {
            var array = CreateTestArray(5);
            var spanA = CreateReadOnlySpan<LayoutA>(array, 0);
            var spanBShort = CreateReadOnlySpan<LayoutB>(array, 4).Slice(0, 3);
            var spanC = CreateReadOnlySpan<LayoutC>(array, 8);

            try
            {
                spanA.Zip(spanBShort, spanC);
                Assert.Fail("Expected ArgumentException");
            }
            catch (ArgumentException) { }
        }

        [TestMethod]
        public void MutableZipTriples_OnlySecondLengthMismatch_Throws()
        {
            var array = CreateTestArray(5);
            var spanA = CreateMutableSpan<LayoutA>(array, 0);
            var spanBShort = CreateMutableSpan<LayoutB>(array, 4).Slice(0, 3);
            var spanC = CreateMutableSpan<LayoutC>(array, 8);

            try
            {
                spanA.Zip(spanBShort, spanC);
                Assert.Fail("Expected ArgumentException");
            }
            catch (ArgumentException) { }
        }

        // ======== QA: Large Array and IsEmpty Tests ========

        [TestMethod]
        public void ReadOnlyZipPairs_LargeArray_CorrectPointerArithmetic()
        {
            var array = CreateTestArray(128);
            var spanA = CreateReadOnlySpan<LayoutA>(array, 0);
            var spanB = CreateReadOnlySpan<LayoutB>(array, 4);

            int index = 0;
            foreach (var pair in spanA.Zip(spanB))
            {
                Assert.AreEqual(array[index].A, pair.First.A);
                Assert.AreEqual(array[index].B, pair.Second.B);
                index++;
            }
            Assert.AreEqual(128, index);
        }

        [TestMethod]
        public void MutableZipTriples_LargeArray_CorrectPointerArithmetic()
        {
            var array = CreateTestArray(128);
            var spanA = CreateMutableSpan<LayoutA>(array, 0);
            var spanB = CreateMutableSpan<LayoutB>(array, 4);
            var spanC = CreateMutableSpan<LayoutC>(array, 8);

            int index = 0;
            foreach (var triple in spanA.Zip(spanB, spanC))
            {
                Assert.AreEqual(array[index].A, triple.First.A);
                Assert.AreEqual(array[index].B, triple.Second.B);
                Assert.AreEqual(array[index].C, triple.Third.C);
                triple.First.A += 1;
                index++;
            }
            Assert.AreEqual(128, index);
            // Verify last element was modified
            Assert.AreEqual(127 * 10 + 1, array[127].A);
        }

        [TestMethod]
        public void ReadOnlyZipPairs_EmptySpan_IsEmpty_ReturnsTrue()
        {
            var spanA = new ReadOnlyTraitSpan<LayoutA>(ref Unsafe.NullRef<byte>(), Unsafe.SizeOf<TestSource>(), 0);
            var spanB = new ReadOnlyTraitSpan<LayoutB>(ref Unsafe.NullRef<byte>(), Unsafe.SizeOf<TestSource>(), 0);

            var zip = spanA.Zip(spanB);
            Assert.IsTrue(zip.IsEmpty);
            Assert.AreEqual(0, zip.Length);
        }
    }
}
