using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TraitSharp.Runtime.Tests
{
    // ─────────────────────────────────────────────────────────────────────
    // Phase 10: Parameterized Default Methods & Chained Dispatch Tests
    // ─────────────────────────────────────────────────────────────────────

    [TestClass]
    public class ParameterizedDefaultTests
    {
        // ── IScalable: parameterized default calling required method ──

        [TestMethod]
        public void ScalableRect_Area_ReturnsCorrectValue()
        {
            var rect = new ScalableRect { Width = 4f, Height = 5f };
            Assert.AreEqual(20f, rect.Area());
        }

        [TestMethod]
        public void ScalableRect_ScaledArea_UsesDefault()
        {
            var rect = new ScalableRect { Width = 4f, Height = 5f };
            // Default: Area() * factor = 20 * 2.5 = 50
            Assert.AreEqual(50f, rect.ScaledArea(2.5f));
        }

        [TestMethod]
        public void ScalableRect_ScaledArea_FactorOne_EqualsArea()
        {
            var rect = new ScalableRect { Width = 3f, Height = 7f };
            Assert.AreEqual(rect.Area(), rect.ScaledArea(1f));
        }

        [TestMethod]
        public void ScalableRect_ScaledArea_FactorZero_ReturnsZero()
        {
            var rect = new ScalableRect { Width = 10f, Height = 10f };
            Assert.AreEqual(0f, rect.ScaledArea(0f));
        }

        [TestMethod]
        public void ScalableRectOverride_ScaledArea_UsesOverride()
        {
            var rect = new ScalableRectOverride { Width = 4f, Height = 5f };
            // Override: Area * factor + 1 = 20 * 2.5 + 1 = 51
            Assert.AreEqual(51f, rect.ScaledArea(2.5f));
        }

        [TestMethod]
        public void ScalableRect_GenericAlgorithm_UsesDefault()
        {
            var rect = new ScalableRect { Width = 4f, Height = 5f };
            float result = ParameterizedDefaultAlgorithms.ScaleArea(ref rect, 3f);
            Assert.AreEqual(60f, result); // 20 * 3
        }

        [TestMethod]
        public void ScalableRectOverride_GenericAlgorithm_UsesOverride()
        {
            var rect = new ScalableRectOverride { Width = 4f, Height = 5f };
            float result = ParameterizedDefaultAlgorithms.ScaleArea(ref rect, 3f);
            Assert.AreEqual(61f, result); // 20 * 3 + 1
        }

        // ── IChainable: default calling another default (chained dispatch) ──

        [TestMethod]
        public void ChainItem_BaseValue_ReturnsCorrectValue()
        {
            var item = new ChainItem { Seed = 5 };
            Assert.AreEqual(5, item.BaseValue());
        }

        [TestMethod]
        public void ChainItem_Doubled_UsesDefault()
        {
            var item = new ChainItem { Seed = 5 };
            // Default: BaseValue() * 2 = 5 * 2 = 10
            Assert.AreEqual(10, item.Doubled());
        }

        [TestMethod]
        public void ChainItem_Quadrupled_UsesChainedDefault()
        {
            var item = new ChainItem { Seed = 5 };
            // Default: Doubled() * 2 = 10 * 2 = 20
            Assert.AreEqual(20, item.Quadrupled());
        }

        [TestMethod]
        public void ChainItem_GenericAlgorithm_ChainsSummary()
        {
            var item = new ChainItem { Seed = 3 };
            string result = ParameterizedDefaultAlgorithms.ChainSummary(ref item);
            Assert.AreEqual("base=3, x2=6, x4=12", result);
        }

        [TestMethod]
        public void ChainItemOverrideMiddle_Doubled_UsesOverride()
        {
            var item = new ChainItemOverrideMiddle { Seed = 5 };
            // Override: Seed * 3 = 15 (not default 10)
            Assert.AreEqual(15, item.Doubled());
        }

        [TestMethod]
        public void ChainItemOverrideMiddle_Quadrupled_CallsOverriddenDoubled()
        {
            var item = new ChainItemOverrideMiddle { Seed = 5 };
            // Quadrupled default calls Doubled() which is overridden to Seed * 3
            // So: 15 * 2 = 30 (not 20)
            Assert.AreEqual(30, item.Quadrupled());
        }

        [TestMethod]
        public void ChainItemOverrideMiddle_GenericAlgorithm_UsesOverriddenChain()
        {
            var item = new ChainItemOverrideMiddle { Seed = 4 };
            // base=4, x2=4*3=12, x4=12*2=24
            string result = ParameterizedDefaultAlgorithms.ChainSummary(ref item);
            Assert.AreEqual("base=4, x2=12, x4=24", result);
        }

        // ── IFormattable: default with property + single/multi parameter ──

        [TestMethod]
        public void FormattableItem_Format_UsesDefault()
        {
            var item = new FormattableItem { Id = 42 };
            Assert.AreEqual("PRE-42", item.Format("PRE"));
        }

        [TestMethod]
        public void FormattableItem_FormatFull_UsesDefault()
        {
            var item = new FormattableItem { Id = 7 };
            Assert.AreEqual("A-7-Z", item.FormatFull("A", "Z"));
        }

        [TestMethod]
        public void FormattableItem_GenericAlgorithm_UsesDefault()
        {
            var item = new FormattableItem { Id = 99 };
            string result = ParameterizedDefaultAlgorithms.FormatWith(ref item, "X");
            Assert.AreEqual("X-99", result);
        }

        [TestMethod]
        public void FormattableItemOverride_Format_UsesOverride()
        {
            var item = new FormattableItemOverride { Id = 42 };
            Assert.AreEqual("[PRE:42]", item.Format("PRE"));
        }

        [TestMethod]
        public void FormattableItemOverride_FormatFull_KeepsDefault()
        {
            var item = new FormattableItemOverride { Id = 7 };
            // FormatFull is NOT overridden — uses default
            Assert.AreEqual("A-7-Z", item.FormatFull("A", "Z"));
        }

        [TestMethod]
        public void FormattableItemOverride_GenericAlgorithm_UsesOverride()
        {
            var item = new FormattableItemOverride { Id = 42 };
            string result = ParameterizedDefaultAlgorithms.FormatWith(ref item, "X");
            Assert.AreEqual("[X:42]", result);
        }

        // ── IComputable: parameter forwarding in chained defaults ──

        [TestMethod]
        public void ComputableItem_Compute_ReturnsCorrectValue()
        {
            var item = new ComputableItem { Base = 10 };
            Assert.AreEqual(15, item.Compute(5)); // Base + x = 10 + 5
        }

        [TestMethod]
        public void ComputableItem_ComputeDouble_UsesDefault()
        {
            var item = new ComputableItem { Base = 10 };
            // Default: Compute(x) * 2 = (10 + 5) * 2 = 30
            Assert.AreEqual(30, item.ComputeDouble(5));
        }

        [TestMethod]
        public void ComputableItem_ComputePlusBase_UsesDefault()
        {
            var item = new ComputableItem { Base = 10 };
            // Default: Compute(x) + Base = (10 + 5) + 10 = 25
            Assert.AreEqual(25, item.ComputePlusBase(5));
        }

        [TestMethod]
        public void ComputableItem_GenericAlgorithm_Summary()
        {
            var item = new ComputableItem { Base = 10 };
            string result = ParameterizedDefaultAlgorithms.ComputeSummary(ref item, 5);
            // compute(5)=15, double=30, plusBase=25
            Assert.AreEqual("compute(5)=15, double=30, plusBase=25", result);
        }

        [TestMethod]
        public void ComputableItem_DifferentInputValues()
        {
            var item = new ComputableItem { Base = 0 };
            Assert.AreEqual(7, item.Compute(7));
            Assert.AreEqual(14, item.ComputeDouble(7));
            Assert.AreEqual(7, item.ComputePlusBase(7)); // Compute(7) + Base(0) = 7
        }

        // ── Cross-type comparisons ──

        [TestMethod]
        public void Scalable_DefaultVsOverride_DifferentResults()
        {
            var def = new ScalableRect { Width = 4f, Height = 5f };
            var ovr = new ScalableRectOverride { Width = 4f, Height = 5f };
            float defResult = def.ScaledArea(2f);
            float ovrResult = ovr.ScaledArea(2f);
            // Default: 20 * 2 = 40, Override: 20 * 2 + 1 = 41
            Assert.AreNotEqual(defResult, ovrResult);
            Assert.AreEqual(40f, defResult);
            Assert.AreEqual(41f, ovrResult);
        }

        [TestMethod]
        public void Chainable_DefaultVsOverrideMiddle_DifferentResults()
        {
            var def = new ChainItem { Seed = 10 };
            var ovr = new ChainItemOverrideMiddle { Seed = 10 };
            // Default chain: 10 → 20 → 40
            Assert.AreEqual(40, def.Quadrupled());
            // Override chain: 10 → 30 → 60
            Assert.AreEqual(60, ovr.Quadrupled());
        }

        [TestMethod]
        public void Formattable_DefaultVsOverride_FormatDiffers()
        {
            var def = new FormattableItem { Id = 1 };
            var ovr = new FormattableItemOverride { Id = 1 };
            Assert.AreEqual("X-1", def.Format("X"));
            Assert.AreEqual("[X:1]", ovr.Format("X"));
        }
    }
}
