using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TraitSharp.Runtime.Tests
{
    /// <summary>
    /// Phase 11: Tests for inherited method dispatch through trait hierarchies.
    /// Verifies that methods inherited from base traits dispatch correctly at runtime,
    /// including default method bodies that call inherited methods.
    /// </summary>
    [TestClass]
    public class InheritedMethodTests
    {
        // ── Scenario A: Base trait with property + method, derived adds default ──

        [TestMethod]
        public void BaseDescribable_DirectDispatch_Works()
        {
            var item = new SimpleItem { Id = 42 };
            Assert.AreEqual("Simple(42)", item.Describe());
        }

        [TestMethod]
        public void DerivedDescribable_InheritedDescribe_Works()
        {
            var item = new DetailedItem { Id = 7, Priority = 3 };
            Assert.AreEqual("Detailed(7)", item.Describe());
        }

        [TestMethod]
        public void DerivedDescribable_DefaultDetailedDescribe_CallsInherited()
        {
            var item = new DetailedItem { Id = 7, Priority = 3 };
            Assert.AreEqual("Detailed(7) [pri=3]", item.DetailedDescribe());
        }

        [TestMethod]
        public void DerivedDescribable_OverrideDetailedDescribe_UsesCustom()
        {
            var item = new DetailedItemOverride { Id = 5, Priority = 9 };
            Assert.AreEqual("[CUSTOM: id=5, pri=9]", item.DetailedDescribe());
        }

        [TestMethod]
        public void DerivedDescribable_OverrideStillHasInheritedDescribe()
        {
            var item = new DetailedItemOverride { Id = 5, Priority = 9 };
            Assert.AreEqual("Override(5)", item.Describe());
        }

        [TestMethod]
        public void Describable_GenericDispatch_BaseConstraint()
        {
            var item = new SimpleItem { Id = 99 };
            Assert.AreEqual("Simple(99)", InheritedMethodAlgorithms.DescribeAny(ref item));
        }

        [TestMethod]
        public void Describable_GenericDispatch_DerivedAsBase()
        {
            var item = new DetailedItem { Id = 10, Priority = 1 };
            Assert.AreEqual("Detailed(10)", InheritedMethodAlgorithms.DescribeAny(ref item));
        }

        [TestMethod]
        public void Describable_GenericDispatch_DetailedDescribe()
        {
            var item = new DetailedItem { Id = 10, Priority = 1 };
            Assert.AreEqual("Detailed(10) [pri=1]", InheritedMethodAlgorithms.DetailedDescribeAny(ref item));
        }

        // ── Scenario B: Pure method inheritance (method-only base) ──

        [TestMethod]
        public void Greetable_InheritedName_Works()
        {
            var entity = new NamedEntity { NameCode = 42 };
            Assert.AreEqual("Entity-42", entity.Name());
        }

        [TestMethod]
        public void Greetable_DefaultGreet_CallsInheritedName()
        {
            var entity = new NamedEntity { NameCode = 42 };
            Assert.AreEqual("Hello, Entity-42!", entity.Greet());
        }

        [TestMethod]
        public void Greetable_GenericDispatch_Works()
        {
            var entity = new NamedEntity { NameCode = 7 };
            Assert.AreEqual("Hello, Entity-7!", InheritedMethodAlgorithms.GreetAny(ref entity));
        }

        // ── Scenario C: Three-level chain with methods ──

        [TestMethod]
        public void ThreeLevel_ComputeValue_DispatchesCorrectly()
        {
            var item = new QuadItem { Value = 5 };
            Assert.AreEqual(5, item.ComputeValue());
        }

        [TestMethod]
        public void ThreeLevel_DefaultDoubleValue_CallsInheritedComputeValue()
        {
            var item = new QuadItem { Value = 5 };
            Assert.AreEqual(10, item.DoubleValue());
        }

        [TestMethod]
        public void ThreeLevel_DefaultQuadValue_ChainsThrough()
        {
            var item = new QuadItem { Value = 5 };
            Assert.AreEqual(20, item.QuadValue());
        }

        [TestMethod]
        public void ThreeLevel_GenericSummary_AllLevelsWork()
        {
            var item = new QuadItem { Value = 3 };
            Assert.AreEqual("val=3, x2=6, x4=12", InheritedMethodAlgorithms.QuadSummary(ref item));
        }

        [TestMethod]
        public void ThreeLevel_OverrideMiddle_QuadUsesOverridden()
        {
            // QuadItemOverrideMiddle overrides DoubleValue to use *3 instead of *2
            var item = new QuadItemOverrideMiddle { Value = 5 };
            Assert.AreEqual(5, item.ComputeValue());
            Assert.AreEqual(15, item.DoubleValue());  // 5 * 3 (overridden)
            Assert.AreEqual(30, item.QuadValue());     // 15 * 2 (default calls overridden DoubleValue)
        }

        [TestMethod]
        public void ThreeLevel_OverrideMiddle_GenericSummary()
        {
            var item = new QuadItemOverrideMiddle { Value = 4 };
            Assert.AreEqual("val=4, x2=12, x4=24", InheritedMethodAlgorithms.QuadSummary(ref item));
        }

        // ── Scenario D: Inherited default without new methods on derived ──

        [TestMethod]
        public void InheritedDefault_FormatTag_WorksThroughDerived()
        {
            var tag = new PriorityTag { TagCode = 42, Level = 5 };
            Assert.AreEqual("TAG-42", tag.FormatTag());
        }

        [TestMethod]
        public void InheritedDefault_GenericDispatch_BaseConstraint()
        {
            var tag = new PriorityTag { TagCode = 99, Level = 1 };
            Assert.AreEqual("TAG-99", InheritedMethodAlgorithms.FormatAny(ref tag));
        }

        // ── Layout cast verification ──

        [TestMethod]
        public void DetailedItem_AsDescribable_ReturnsCorrectId()
        {
            var item = new DetailedItem { Id = 77, Priority = 2 };
            ref readonly var layout = ref item.AsDescribable();
            Assert.AreEqual(77, layout.Id);
        }

        [TestMethod]
        public void DetailedItem_AsDetailedDescribable_ReturnsCorrectFields()
        {
            var item = new DetailedItem { Id = 77, Priority = 2 };
            ref readonly var layout = ref item.AsDetailedDescribable();
            Assert.AreEqual(77, layout.Id);
            Assert.AreEqual(2, layout.Priority);
        }

        [TestMethod]
        public void QuadItem_AsValueProvider_ReturnsCorrectValue()
        {
            var item = new QuadItem { Value = 42 };
            ref readonly var layout = ref item.AsValueProvider();
            Assert.AreEqual(42, layout.Value);
        }

        [TestMethod]
        public void PriorityTag_AsTaggable_ReturnsCorrectTagCode()
        {
            var tag = new PriorityTag { TagCode = 10, Level = 3 };
            ref readonly var layout = ref tag.AsTaggable();
            Assert.AreEqual(10, layout.TagCode);
        }

        [TestMethod]
        public void PriorityTag_AsPriorityTaggable_ReturnsAllFields()
        {
            var tag = new PriorityTag { TagCode = 10, Level = 3 };
            ref readonly var layout = ref tag.AsPriorityTaggable();
            Assert.AreEqual(10, layout.TagCode);
            Assert.AreEqual(3, layout.Level);
        }
    }
}
