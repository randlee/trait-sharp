using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TraitSharp.Runtime.Tests
{
    /// <summary>
    /// Runtime tests for Phase 8: Method Trait dispatch.
    /// Validates that generated method dispatch code actually compiles and runs correctly.
    /// </summary>
    [TestClass]
    public class MethodTraitTests
    {
        // ─────────────────────────────────────────────────────────────────
        // ILabeled: single method + property trait
        // ─────────────────────────────────────────────────────────────────

        [TestMethod]
        public void LabeledItem_Describe_ReturnsExpectedFormat()
        {
            var item = new LabeledItem { Tag = 42, Value = 3.14f };
            Assert.AreEqual("Item(Tag=42, Value=3.1)", item.Describe());
        }

        [TestMethod]
        public void LabeledItem_Describe_ZeroValues()
        {
            var item = new LabeledItem { Tag = 0, Value = 0f };
            Assert.AreEqual("Item(Tag=0, Value=0.0)", item.Describe());
        }

        [TestMethod]
        public void LabeledItem_Describe_NegativeValues()
        {
            var item = new LabeledItem { Tag = -1, Value = -99.9f };
            Assert.AreEqual("Item(Tag=-1, Value=-99.9)", item.Describe());
        }

        [TestMethod]
        public void LabeledItem_Tag_PropertyAccess()
        {
            var item = new LabeledItem { Tag = 7 };
            // Tag should be accessible via trait property accessor
            Assert.AreEqual(7, item.GetTag());
        }

        [TestMethod]
        public void LabeledItem_GenericAlgorithm_MatchesDirectCall()
        {
            var item = new LabeledItem { Tag = 10, Value = 2.5f };
            string direct = item.Describe();
            string generic = MethodTraitAlgorithms.DescribeItem(ref item);
            Assert.AreEqual(direct, generic);
        }

        // ─────────────────────────────────────────────────────────────────
        // IMultiMethod: multiple methods with diverse return types
        // ─────────────────────────────────────────────────────────────────

        [TestMethod]
        public void MultiMethodItem_Name_ReturnsFormatted()
        {
            var item = new MultiMethodItem { Id = 5 };
            Assert.AreEqual("MMI-5", item.Name());
        }

        [TestMethod]
        public void MultiMethodItem_DoubleId_ReturnsCorrectValue()
        {
            var item = new MultiMethodItem { Id = 7 };
            Assert.AreEqual(14, item.DoubleId());
        }

        [TestMethod]
        public void MultiMethodItem_DoubleId_ZeroCase()
        {
            var item = new MultiMethodItem { Id = 0 };
            Assert.AreEqual(0, item.DoubleId());
        }

        [TestMethod]
        public void MultiMethodItem_DoubleId_NegativeCase()
        {
            var item = new MultiMethodItem { Id = -3 };
            Assert.AreEqual(-6, item.DoubleId());
        }

        [TestMethod]
        public void MultiMethodItem_Reset_DoesNotThrow()
        {
            var item = new MultiMethodItem { Id = 42 };
            // Void method dispatch — just ensure it runs without error
            item.Reset();
            // Id unchanged (readonly dispatch)
            Assert.AreEqual(42, item.Id);
        }

        [TestMethod]
        public void MultiMethodItem_GenericAlgorithm_CombinesNameAndDoubleId()
        {
            var item = new MultiMethodItem { Id = 3 };
            string result = MethodTraitAlgorithms.SummarizeMulti(ref item);
            Assert.AreEqual("MMI-3 doubled=6", result);
        }

        // ─────────────────────────────────────────────────────────────────
        // IGreeter: method with parameter, pure method (no properties)
        // ─────────────────────────────────────────────────────────────────

        [TestMethod]
        public void FormalGreeter_Greet_ReturnsFormally()
        {
            var g = new FormalGreeter { Dummy = 0 };
            Assert.AreEqual("Good day, Alice!", g.Greet("Alice"));
        }

        [TestMethod]
        public void CasualGreeter_Greet_ReturnsCasually()
        {
            var g = new CasualGreeter { Dummy = 0 };
            Assert.AreEqual("Hey Bob!", g.Greet("Bob"));
        }

        [TestMethod]
        public void FormalGreeter_Greet_EmptyName()
        {
            var g = new FormalGreeter { Dummy = 0 };
            Assert.AreEqual("Good day, !", g.Greet(""));
        }

        [TestMethod]
        public void Greeter_GenericAlgorithm_FormalDispatch()
        {
            var g = new FormalGreeter { Dummy = 0 };
            string result = MethodTraitAlgorithms.GreetPerson(ref g, "World");
            Assert.AreEqual("Good day, World!", result);
        }

        [TestMethod]
        public void Greeter_GenericAlgorithm_CasualDispatch()
        {
            var g = new CasualGreeter { Dummy = 0 };
            string result = MethodTraitAlgorithms.GreetPerson(ref g, "World");
            Assert.AreEqual("Hey World!", result);
        }

        [TestMethod]
        public void Greeter_DifferentTypes_DispatchDifferently()
        {
            var formal = new FormalGreeter { Dummy = 0 };
            var casual = new CasualGreeter { Dummy = 0 };

            string fResult = MethodTraitAlgorithms.GreetPerson(ref formal, "Test");
            string cResult = MethodTraitAlgorithms.GreetPerson(ref casual, "Test");

            Assert.AreNotEqual(fResult, cResult);
            Assert.IsTrue(fResult.Contains("Good day"));
            Assert.IsTrue(cResult.Contains("Hey"));
        }

        // ─────────────────────────────────────────────────────────────────
        // Zero-allocation: method dispatch should be inlined
        // ─────────────────────────────────────────────────────────────────

        [TestMethod]
        public void MethodTrait_LayoutCast_ZeroAllocation()
        {
            var item = new LabeledItem { Tag = 1, Value = 1.0f };
            long before = GC.GetAllocatedBytesForCurrentThread();

            // Layout cast portion (property access) should be zero-alloc
            for (int i = 0; i < 10_000; i++)
            {
                _ = item.GetTag();
            }

            long after = GC.GetAllocatedBytesForCurrentThread();
            Assert.AreEqual(0, after - before,
                "Property access via trait layout cast should allocate 0 bytes");
        }
    }
}
